// TV单路应用、逐目标持久状态及预案同源跳过；手动强制重发，部分失败不沿用旧可信记录。
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using ScpCv.Contracts.Http;
using ScpCv.Domain.Model;
using ScpCv.Infrastructure.Devices;
using ScpCv.Infrastructure.Persistence;
using ScpCv.Infrastructure.VideoWall;

namespace ScpCv.Infrastructure.DisplayRouting;

public sealed class DisplayRoutingService(
    IDbContextFactory<ControlDbContext> contextFactory, WriteCoordinator writes,
    ITelevisionController televisions, VideoWallOperationGate gate,
    VideoWallProtocolOptions protocol, TimeProvider? timeProvider = null)
{
    private readonly TimeProvider _time = timeProvider ?? TimeProvider.System;

    /// <summary>:param token: 请求取消。:returns: 各输出最后写出意图；不将历史库推断成已确认画面。</summary>
    public async Task<IReadOnlyDictionary<string, DisplayRoutingTargetDto>> GetAsync(CancellationToken token = default)
    {
        await using var database = await contextFactory.CreateDbContextAsync(token).ConfigureAwait(false);
        var state = await database.RuntimeStates.AsNoTracking().SingleAsync(token).ConfigureAwait(false);
        return DisplayRoutingState.Targets.ToDictionary(target => target, target => Map(state, target));
    }

    /// <summary>:param target: 左右电视。:param input: 固定来源描述符。:param force: 手动同源重发。:param token: 请求取消。</summary>
    public async Task<(DisplayRoutingTargetDto Output, string Result)> ApplyAsync(
        string target, VideoWallInputDto input, bool force, CancellationToken token = default)
    {
        Preflight(target, input);
        using var operation = await gate.EnterAsync(token).ConfigureAwait(false);
        return await ApplyLockedAsync(target, input, force, token).ConfigureAwait(false);
    }

    /// <summary>:param target: 固定目标。:param input: 请求源。:returns: 预检完整序列，失败发生在任何首包之前。</summary>
    public IReadOnlyList<byte[]> Preflight(string target, VideoWallInputDto input)
    {
        _ = TelevisionTargets.Address(target);
        var descriptor = SiteInputCatalog.ResolveDescriptor(input.Kind, input.IpAddress, protocol)
            ?? throw new DisplayRoutingException("此输入未登记电视组播参数。", "protocol_unavailable");
        return TelevisionPacketCodec.Build(descriptor, protocol);
    }

    /// <summary>:param target: 固定目标。:param input: 已预检来源。:param force: false时使用持久指纹。:param token: 取消。:remarks: 调用方必须持有同一VideoWallOperationGate，避免重入锁。</summary>
    internal async Task<(DisplayRoutingTargetDto Output, string Result)> ApplyLockedAsync(
        string target, VideoWallInputDto input, bool force, CancellationToken token)
    {
        var frames = Preflight(target, input);
        var fingerprint = DisplayRoutingState.TelevisionFingerprint(target, frames);
        await using (var snapshot = await contextFactory.CreateDbContextAsync(token).ConfigureAwait(false))
        {
            var state = await snapshot.RuntimeStates.AsNoTracking().SingleAsync(token).ConfigureAwait(false);
            if (!force && DisplayRoutingState.Matches(state, target, fingerprint, televisions.IsHardware))
                return (Map(state, target), "unchanged");
        }
        var started = false;
        try
        {
            await MarkLockedAsync(target, "dispatching", token).ConfigureAwait(false);
            started = true;
            await televisions.DispatchAsync(target, frames, token).ConfigureAwait(false);
            var result = televisions.IsHardware ? "sent_unconfirmed" : "simulated";
            var output = await writes.ExecuteAsync(async (database, cancellation) =>
            {
                var state = await database.RuntimeStates.SingleAsync(cancellation).ConfigureAwait(false);
                var now = _time.GetUtcNow();
                DisplayRoutingState.Write(state, target, new(fingerprint, result, new() { Input = input }, now));
                state.UpdatedAt = now;
                return Map(state, target);
            }, token).ConfigureAwait(false);
            return (output, result);
        }
        catch (OperationCanceledException)
        { if (started) await MarkUncertainAsync(target).ConfigureAwait(false); throw; }
        catch (Exception exception) when (exception is DisplayRoutingException or DbUpdateException or DbException or InvalidOperationException)
        {
            await MarkUncertainAsync(target).ConfigureAwait(false);
            throw new DisplayRoutingException(started ? "电视切换未完整确认，请核对实际画面。" : "发送前状态保存失败，未发送电视控制帧。",
                started ? "display_routing_uncertain" : "display_routing_persistence_failed", exception);
        }
    }

    /// <summary>:param target: 受信输出。:param action: 电源动作。:param token: 取消。:returns: 锁内先使旧路由失效，再执行电源，防止交错写成功记录。</summary>
    public async Task WithInvalidatedOutputAsync(string target, Func<Task> action, CancellationToken token)
    {
        if (!DisplayRoutingState.Targets.Contains(target)) throw new DisplayRoutingException("未知输出，未发送设备命令。");
        using var operation = await gate.EnterAsync(token).ConfigureAwait(false);
        try { await MarkLockedAsync(target, "unknown", token).ConfigureAwait(false); }
        catch (Exception exception) when (exception is DbUpdateException or DbException or InvalidOperationException)
        { throw new DisplayRoutingException("输出状态保存未确认，未发送电源指令。", "display_routing_persistence_failed", exception); }
        await action().ConfigureAwait(false);
    }

    /// <summary>:param target: 输出。:param status: 非成功状态。:param token: 取消。:remarks: 调用方持有输出门禁。</summary>
    private Task MarkLockedAsync(string target, string status, CancellationToken token) => writes.ExecuteAsync(async (database, cancellation) =>
    {
        var state = await database.RuntimeStates.SingleAsync(cancellation).ConfigureAwait(false);
        DisplayRoutingState.Invalidate(state, target, status, _time.GetUtcNow());
        state.UpdatedAt = _time.GetUtcNow();
        if (target == "wall") { state.WallLayoutDispatchStatus = status; state.WallLayoutDispatchDetail = string.Empty; }
    }, token);

    /// <summary>:param target: 输出。:returns: 尽力失效；库写失败仍保留原始错误，不自动重发。</summary>
    private async Task MarkUncertainAsync(string target)
    {
        try { await MarkLockedAsync(target, "uncertain", CancellationToken.None).ConfigureAwait(false); }
        catch (Exception exception) when (exception is DbUpdateException or DbException or InvalidOperationException) { }
    }

    /// <summary>:param state: 当前库。:param target: 固定输出。:returns: 不暴露内部指纹的状态合同。</summary>
    private static DisplayRoutingTargetDto Map(RuntimeState state, string target)
    {
        var record = DisplayRoutingState.Read(state, target);
        return new() { Target = target, Status = record?.Status ?? "unknown", LastSent = record?.LastSent, UpdatedAt = record?.UpdatedAt };
    }
}
