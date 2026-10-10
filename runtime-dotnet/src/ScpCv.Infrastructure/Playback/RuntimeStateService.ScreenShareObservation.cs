// 清理只读核原OPEN审计、当前意图和原Worker；这些事实不代替原生资源释放。
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using ScpCv.Domain.Model;

namespace ScpCv.Infrastructure.Playback;

public sealed record ScreenShareRuntimeObservation(bool TargetCurrent, bool StillDesired, bool OpenCommitted);
public sealed record ScreenShareDesiredIdentity(int WindowId, Guid ShareId, long SourceGeneration);

public sealed partial class RuntimeStateService
{
    /// <summary>:param windowId: 两逻辑窗口之一。:param cancellationToken: 宿主寿命。:returns: 当前意图的非秘密动态身份，不能凭最早pending投影或旧实际画面恢复授权。</summary>
    public async Task<ScreenShareDesiredIdentity?> GetDesiredScreenShareAsync(int windowId, CancellationToken cancellationToken = default)
    {
        ValidateWindow(windowId);
        await using var database = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var session = await database.PlaybackSessions.AsNoTracking().SingleAsync(item => item.WindowId == windowId, cancellationToken).ConfigureAwait(false);
        if (session.MediaSourceId is not null || session.DesiredGeneration <= 0) return null;
        var args = await database.CommandRecords.AsNoTracking().Where(item => item.TargetKind == CommandTargetKind.Display &&
                item.TargetId == windowId && item.Command == "OPEN" && item.SourceGeneration == session.DesiredGeneration)
            .OrderByDescending(item => item.TargetSequence).Select(item => item.ArgsJson).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        if (args is null) return null;
        try
        {
            using var json = JsonDocument.Parse(args);
            return json.RootElement.ValueKind == JsonValueKind.Object && json.RootElement.TryGetProperty("source_type", out var kind) &&
                kind.ValueKind == JsonValueKind.String && kind.GetString() == "screen_share" &&
                json.RootElement.TryGetProperty("share_id", out var value) && value.ValueKind == JsonValueKind.String &&
                value.TryGetGuid(out var id) && id != Guid.Empty ? new(windowId, id, session.DesiredGeneration) : null;
        }
        catch (JsonException) { return null; }
    }

    /// <summary>:param target: 原预约身份。:param id: 原分享。:param generation: 原OPEN代次。:returns: 短只读查询，条件CLOSE仍须在写事务再次核对。</summary>
    public async Task<ScreenShareRuntimeObservation> ObserveScreenShareAsync(ScreenShareTargetIdentity target, Guid id,
        long? generation, CancellationToken cancellationToken = default)
    {
        await using var database = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var session = await database.PlaybackSessions.AsNoTracking().SingleAsync(item => item.WindowId == target.WindowId, cancellationToken).ConfigureAwait(false);
        var group = await database.RuntimeGroupControls.AsNoTracking().SingleAsync(cancellationToken).ConfigureAwait(false);
        var worker = await database.WorkerOwnerships.AsNoTracking().SingleOrDefaultAsync(item => item.TargetKind == CommandTargetKind.Display &&
            item.TargetId == target.WindowId, cancellationToken).ConfigureAwait(false);
        var now = _timeProvider.GetUtcNow();
        var current = group.State == RuntimeGroupState.Armed && group.GroupEpoch == target.GroupEpoch &&
            worker is not null && worker.WorkerInstanceId == target.WorkerInstanceId && worker.OwnerEpoch == target.OwnerEpoch &&
            worker.Status == WorkerOwnershipState.Online && now - worker.LastTransportHeartbeat <= TimeSpan.FromSeconds(5);
        var args = generation is null ? null : await database.CommandRecords.AsNoTracking()
            .Where(item => item.TargetKind == CommandTargetKind.Display && item.TargetId == target.WindowId &&
                item.Command == "OPEN" && item.SourceGeneration == generation)
            .OrderByDescending(item => item.TargetSequence).Select(item => item.ArgsJson).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        var committed = MatchesScreenShare(args, id);
        return new(current, committed && session.MediaSourceId is null && session.DesiredGeneration == generation, committed);
    }
}
