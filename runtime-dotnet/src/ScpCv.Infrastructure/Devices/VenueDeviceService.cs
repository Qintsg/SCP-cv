// 现场音频独立持久化与串行发送；缺少来源/话筒绑定先拒绝，避免部分写出。
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ScpCv.Contracts.Http;
using ScpCv.Domain.Model;
using ScpCv.Infrastructure.Persistence;

namespace ScpCv.Infrastructure.Devices;

public sealed class VenueDeviceService(IDbContextFactory<ControlDbContext> contexts, WriteCoordinator writes,
    IVenueAudioTransport transport, CameraCatalog cameras) : IDisposable
{
    private readonly SemaphoreSlim _audioGate = new(1, 1);

    /// <summary>:returns: 只读能力与最后确认配置，不自动访问现场网络。</summary>
    public async Task<object> GetAsync(CancellationToken cancellationToken = default)
    {
        var ids = await cameras.SourceIdsAsync(cancellationToken).ConfigureAwait(false);
        return new
        {
            cameras = cameras.Definitions.Select(camera => new
            {
                id = camera.Id, name = camera.Name, path = "/Camera/" + camera.Name,
                source_id = ids.GetValueOrDefault(camera.Id), stream_configured = camera.Uri.Length > 0,
                movable = camera.Movable, control_available = false,
                unavailable_reason = camera.Movable ? "运动端点和协议绑定尚未核实，未下发任何运动帧。" : "固定监控不支持转动。",
            }),
            audio = await SettingsAsync(cancellationToken).ConfigureAwait(false),
            capabilities = new { screen_volume = true, handheld_volume = false, hanging_volume = false, source = false,
                reason = "155屏幕音量已核实；话筒通道、IP81/SCP-cv路由及软件输出端点尚未绑定。" },
            simulation = transport.IsSimulation,
        };
    }

    /// <summary>:param id: 内置摄像机。:returns: 缺绑定时明确拒绝，且没有网络写出。</summary>
    public void ValidateCameraControl(string id)
    {
        var camera = cameras.Definitions.SingleOrDefault(camera => camera.Id == id)
            ?? throw new VenueDeviceException("摄像机不存在。");
        throw new VenueDeviceException(camera.Movable ? "该摄像机运动协议尚未绑定，操作未执行。" : "固定监控不支持运动或位置预设。");
    }

    /// <summary>:param settings: 仅提交要改动的字段。:returns: 全部字段预检，不允许先音量后发现来源失败。</summary>
    public static void Validate(VenueAudioSettings settings)
    {
        foreach (var volume in new[] { settings.ScreenVolume, settings.HandheldVolume, settings.HangingVolume })
            if (volume is < 0 or > 100) throw new VenueDeviceException("音量必须是0–100的整数。");
        if (settings.Source is not (null or "ip81" or "scp_cv")) throw new VenueDeviceException("音量来源只能是IP81或SCP-cv。");
        if (settings.Source is not null || settings.HandheldVolume is not null || settings.HangingVolume is not null)
            throw new VenueDeviceException("来源或话筒输出绑定尚未确认，未发送任何配置。");
    }

    public async Task<VenueAudioSettings> SettingsAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await contexts.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var json = await db.RuntimeStates.Where(state => state.Id == RuntimeState.SingletonId)
            .Select(state => state.VenueAudioJson).SingleAsync(cancellationToken).ConfigureAwait(false);
        try { return JsonSerializer.Deserialize<VenueAudioSettings>(json) ?? new(); }
        catch (JsonException) { throw new VenueDeviceException("现场音频持久配置无效，未按默认值覆盖设备。"); }
    }

    public async Task<VenueAudioResult> RefreshAsync(CancellationToken cancellationToken = default)
    {
        await _audioGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var actual = await transport.ReadAsync(cancellationToken).ConfigureAwait(false);
            await RememberAsync(actual, cancellationToken).ConfigureAwait(false);
            return Result(actual, "屏幕音量已读回；未修改设备。");
        }
        finally { _audioGate.Release(); }
    }

    public async Task<VenueAudioResult> ApplyAsync(VenueAudioSettings settings, CancellationToken cancellationToken = default)
    {
        Validate(settings);
        await _audioGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (settings.ScreenVolume is null) return new() { Status = "unchanged", Detail = "未配置的音量项保持原状。" };
            return await SendAndConfirmAsync(settings.ScreenVolume.Value, cancellationToken).ConfigureAwait(false);
        }
        finally { _audioGate.Release(); }
    }

    /// <summary>:returns: 切墙后重发当前音量恰好一次；首次未配置先读取实际值，不强行设为100。</summary>
    public async Task<VenueAudioResult> ReapplyAsync(CancellationToken cancellationToken = default)
    {
        await _audioGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var settings = await SettingsAsync(cancellationToken).ConfigureAwait(false);
            Validate(settings);
            var value = settings.ScreenVolume ?? await transport.ReadAsync(cancellationToken).ConfigureAwait(false);
            return await SendAndConfirmAsync(value, cancellationToken).ConfigureAwait(false);
        }
        catch (VenueDeviceException exception) { return new() { Status = "uncertain", Detail = "画面指令已发送；音频阶段失败：" + exception.Message }; }
        finally { _audioGate.Release(); }
    }

    private async Task<VenueAudioResult> SendAndConfirmAsync(int value, CancellationToken cancellationToken)
    {
        await transport.WriteAsync(value, cancellationToken).ConfigureAwait(false);
        int actual;
        try { actual = await transport.ReadAsync(cancellationToken).ConfigureAwait(false); }
        catch (VenueDeviceException exception) { return new() { Status = "sent_unconfirmed", Detail = "配置已发送但未确认：" + exception.Message }; }
        await RememberAsync(actual, cancellationToken).ConfigureAwait(false);
        if (actual != value) return new() { Status = "mismatch", ScreenVolume = actual, Detail = $"请求{value}，读回{actual}；未自动重试。" };
        return Result(actual, "屏幕音量配置已发送且读回一致。");
    }

    private Task RememberAsync(int actual, CancellationToken cancellationToken) => writes.ExecuteAsync(async (db, token) =>
    {
        var state = await db.RuntimeStates.SingleAsync(token).ConfigureAwait(false);
        state.VenueAudioJson = JsonSerializer.Serialize(new VenueAudioSettings { ScreenVolume = actual });
    }, cancellationToken);

    private VenueAudioResult Result(int value, string detail) => new()
    { Status = transport.IsSimulation ? "simulated" : "confirmed", ScreenVolume = value,
        Detail = transport.IsSimulation ? "仿真结果，未访问实体设备。" : detail };

    public void Dispose() { _audioGate.Dispose(); GC.SuppressFinalize(this); }
}
