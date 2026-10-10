// 周期清理的私有原身份与实际Dispose回执；不向公开快照暴露Worker、cap或信令。
using System.Text.Json;
using ScpCv.Infrastructure.Playback;

namespace ScpCv.ControlHost.ScreenSharing;

internal sealed record ScreenShareCleanupTarget(ScreenShareSnapshot Share, ScreenShareTargetIdentity Target, bool NativeReleased);

public sealed partial class ScreenShareRegistry
{
    /// <summary>:param id: 原会话。:param target: 原OS观察对应的Worker归属。:returns: 原Player确证死亡释放其全部原生对象，但不能终止后来Worker或分享。</summary>
    internal void ObserveProcessExit(Guid id, ScreenShareTargetIdentity target)
    {
        lock (_sync)
        {
            if (!_entries.TryGetValue(id, out var entry) || entry.IsTerminal || entry.Target != target) return;
            StopLocked(entry, "player_exited");
            entry.NativeReleased = true;
        }
    }

    /// <summary>:param desired: 持久当前意图。:returns: 内存授权仍属于同分享/窗口/代次；终态不恢复接收资格。</summary>
    internal bool OwnsDesiredShare(ScreenShareDesiredIdentity desired)
    {
        lock (_sync)
        {
            ExpireLocked();
            return _entries.TryGetValue(desired.ShareId, out var entry) && !entry.IsTerminal &&
                entry.WindowId == desired.WindowId && entry.SourceGeneration == desired.SourceGeneration;
        }
    }

    /// <summary>:returns: 最多两条原身份；读取主动过期，不依赖发布者继续轮询。</summary>
    internal IReadOnlyList<ScreenShareCleanupTarget> GetCleanupTargets()
    {
        lock (_sync)
        {
            ExpireLocked();
            return _entries.Values.Where(entry => !entry.IsTerminal)
                .Select(entry => new ScreenShareCleanupTarget(entry.Snapshot(), entry.Target, entry.NativeReleased)).ToArray();
        }
    }

    /// <summary>:param id: 原会话。:returns: 生命周期可能已变化的新纯值，不创建或恢复条目。</summary>
    internal ScreenShareCleanupTarget? GetCleanupTarget(Guid id)
    {
        lock (_sync)
        {
            ExpireLocked();
            return _entries.TryGetValue(id, out var entry) && !entry.IsTerminal
                ? new(entry.Snapshot(), entry.Target, entry.NativeReleased) : null;
        }
    }

    /// <summary>:param id: 自有原会话。:param reason: 固定原因。:returns: 先撤销cap，不以Worker离线或换源宣布释放。</summary>
    internal void BeginSystemStop(Guid id, string reason)
    {
        lock (_sync)
        {
            if (_entries.TryGetValue(id, out var entry)) StopLocked(entry, reason);
        }
    }

    /// <summary>:param state: 已通过原Worker管道与持久归属门禁的报告。:returns: 仅记录同窗口/原实例/owner/分享/代次的实际Dispose证据。</summary>
    internal void ObserveNativeRelease(int windowId, Guid instanceId, long ownerEpoch, JsonElement state)
    {
        if (state.ValueKind != JsonValueKind.Object || !state.TryGetProperty("released_screen_shares", out var items) ||
            items.ValueKind != JsonValueKind.Array || items.GetArrayLength() > 32) return;
        var proofs = new List<(Guid Id, long Generation)>();
        foreach (var item in items.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object || item.EnumerateObject().Count() != 2 ||
                !item.TryGetProperty("share_id", out var id) || id.ValueKind != JsonValueKind.String || !id.TryGetGuid(out var shareId) || shareId == Guid.Empty ||
                !item.TryGetProperty("source_generation", out var generation) || generation.ValueKind != JsonValueKind.Number ||
                !generation.TryGetInt64(out var value) || value <= 0) return;
            proofs.Add((shareId, value));
        }
        lock (_sync)
        {
            ExpireLocked();
            foreach (var proof in proofs)
            {
                if (!_entries.TryGetValue(proof.Id, out var entry) || entry.IsTerminal || entry.WindowId != windowId ||
                    entry.Target.WorkerInstanceId != instanceId || entry.Target.OwnerEpoch != ownerEpoch || entry.SourceGeneration != proof.Generation) continue;
                StopLocked(entry, "receiver_released");
                entry.NativeReleased = true;
            }
        }
    }
}
