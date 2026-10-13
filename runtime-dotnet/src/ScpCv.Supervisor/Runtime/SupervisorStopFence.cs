// 当前认证停机准备围栏；仅正常成员退出可被停机接纳，异常退出始终保持失败。
using System.Text.Json;
using ScpCv.Contracts.Ipc;

namespace ScpCv.Supervisor.Runtime;

public sealed class SupervisorStopFence(long ownerEpoch, long groupEpoch)
{
    private readonly object _gate = new();
    private Guid _shutdownId;

    /// <summary>:param frame: 当前ControlHost的认证通知。:returns: 只有同Owner/Group、有效nonce与固定schema可准备停机。</summary>
    public bool TryPrepare(IpcFrameDto frame)
    {
        var payload = frame.Payload;
        if (frame.MessageType != "shutdown_request" || frame.OwnerEpoch != ownerEpoch || frame.Target is not null ||
            groupEpoch <= 0 || payload.ValueKind != JsonValueKind.Object ||
            !payload.TryGetProperty("group_epoch", out var epoch) || epoch.ValueKind != JsonValueKind.Number || !epoch.TryGetInt64(out var current) || current != groupEpoch ||
            !payload.TryGetProperty("reason", out var reason) || reason.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(reason.GetString()) ||
            !payload.TryGetProperty("shutdown_id", out var id) || id.ValueKind != JsonValueKind.String ||
            !id.TryGetGuid(out var shutdownId) || shutdownId == Guid.Empty) return false;
        lock (_gate)
        {
            if (_shutdownId != Guid.Empty && _shutdownId != shutdownId) return false;
            _shutdownId = shutdownId;
            return true;
        }
    }

    /// <summary>:param exitCode: 原成员真实退出码。:returns: 无准备的退出（包括0）及一切非零退出均失败。</summary>
    public bool IsUnexpectedExit(int exitCode)
    { lock (_gate) return exitCode != 0 || _shutdownId == Guid.Empty; }

    /// <summary>:param payload: 当前认证Host的心跳回执。:returns: BeginDrain的恰下一代可暂候准备；不据此允许成员退出。</summary>
    public bool AllowsDrainingHeartbeat(JsonElement payload) => groupEpoch is > 0 and < long.MaxValue &&
        payload.ValueKind == JsonValueKind.Object && payload.TryGetProperty("group_epoch", out var epoch) &&
        epoch.ValueKind == JsonValueKind.Number && epoch.TryGetInt64(out var current) && current == groupEpoch + 1 &&
        payload.TryGetProperty("group_state", out var state) && state.ValueKind == JsonValueKind.String &&
        state.GetString() is "draining" or "stopped";
}
