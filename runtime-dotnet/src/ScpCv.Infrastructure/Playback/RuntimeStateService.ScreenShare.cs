// 临时共享的动态OPEN及原子条件CLOSE，不创建永久媒体源。SCP-cv 2026-10-10。
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ScpCv.Domain.Model;
using ScpCv.Infrastructure.Persistence;

namespace ScpCv.Infrastructure.Playback;

public sealed partial class RuntimeStateService
{
    /// <summary>预约前读取当前在线Worker身份；OPEN还会在事务中重新核对。</summary>
    public async Task<ScreenShareTargetIdentity> GetScreenShareTargetAsync(int windowId, CancellationToken cancellationToken = default)
    {
        ValidateWindow(windowId);
        await using var database = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var session = await database.PlaybackSessions.AsNoTracking().SingleAsync(item => item.WindowId == windowId, cancellationToken)
            .ConfigureAwait(false);
        return await ReadScreenShareTargetAsync(database, session, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// :param target: 预约时的Worker身份；不允许旧账号流程跨运行组重启后继续。
    /// :param shareId: 已认证发布者的内存分享身份。
    /// :param bindBeforeCommit: 同步绑定源代次，提交后Wake前已经就绪；调用者须在提交/唤醒异常后撤销cap。
    /// :returns: 持久受理回执，不代表出画；凭据仅在后续已认证Worker领取时注入。
    /// </summary>
    public async Task<ScreenShareOpenReceipt> OpenScreenShareAsync(
        ScreenShareTargetIdentity target,
        Guid shareId,
        Action<long> bindBeforeCommit,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(bindBeforeCommit);
        ValidateWindow(target.WindowId);
        if (shareId == Guid.Empty) throw new PlaybackServiceException("分享身份无效。", "share_invalid");
        long generation = 0;
        var sessions = await EnqueueDisplayAsync(target.WindowId, "OPEN", "{}", async (database, session, command, token) =>
        {
            var current = await ReadScreenShareTargetAsync(database, session, token).ConfigureAwait(false);
            if (current != target) throw new PlaybackServiceException("播放器或运行组已改变，请重新发起分享。", "share_player_offline");
            session.MediaSourceId = null;
            session.PlaybackMode = PlaybackMode.None;
            session.PlaybackState = PlaybackState.Loading;
            session.ErrorMessage = string.Empty;
            session.CurrentSlide = 0;
            session.TotalSlides = 0;
            session.PositionMs = 0;
            session.DurationMs = 0;
            session.DesiredGeneration = checked(session.DesiredGeneration + 1);
            generation = session.DesiredGeneration;
            command.ArgsJson = JsonSerializer.Serialize(new { source_id = 0, source_type = "screen_share", share_id = shareId });
            command.SourceGeneration = generation;
            command.SourceRevision = 0;
            command.Deadline = _timeProvider.GetUtcNow().AddSeconds(20);
            bindBeforeCommit(generation);
        }, cancellationToken).ConfigureAwait(false);
        return new(sessions.Single(session => session.WindowId == target.WindowId).SubmittedCommandId!.Value, target.WindowId, generation);
    }

    /// <summary>同一短事务内核分享ID和源代次再排CLOSE；不先读后无条件关窗。</summary>
    public async Task<ScreenShareCloseReceipt> CloseScreenShareAsync(
        int windowId,
        Guid shareId,
        long sourceGeneration,
        CancellationToken cancellationToken = default)
    {
        ValidateWindow(windowId);
        if (shareId == Guid.Empty || sourceGeneration <= 0)
            throw new PlaybackServiceException("分享停止身份无效。", "share_invalid");
        long closeGeneration = 0;
        try
        {
            var sessions = await EnqueueDisplayAsync(windowId, "CLOSE", "{}", async (database, session, command, token) =>
            {
                // CommandArgsJson是最早pending投影，不能当作当前源身份；核对本代次真正的OPEN审计。
                var desiredArgs = session.DesiredGeneration != sourceGeneration || session.MediaSourceId is not null
                    ? null
                    : await database.CommandRecords.Where(item => item.TargetKind == CommandTargetKind.Display &&
                            item.TargetId == windowId && item.Command == "OPEN" && item.SourceGeneration == sourceGeneration)
                        .OrderByDescending(item => item.TargetSequence).Select(item => item.ArgsJson).FirstOrDefaultAsync(token)
                        .ConfigureAwait(false);
                if (!MatchesScreenShare(desiredArgs, shareId))
                    throw new PlaybackServiceException("旧分享已不是当前源意图。", "share_no_longer_desired");
                session.MediaSourceId = null;
                session.PlaybackMode = PlaybackMode.None;
                session.PlaybackState = PlaybackState.Idle;
                session.ErrorMessage = string.Empty;
                session.DesiredGeneration = checked(session.DesiredGeneration + 1);
                closeGeneration = session.DesiredGeneration;
                command.SourceGeneration = closeGeneration;
                command.SourceRevision = 0;
                command.ArgsJson = JsonSerializer.Serialize(new { source_type = "screen_share", share_id = shareId, close_source_generation = sourceGeneration });
            }, cancellationToken).ConfigureAwait(false);
            return new(ScreenShareCloseDisposition.Queued,
                sessions.Single(session => session.WindowId == windowId).SubmittedCommandId, closeGeneration);
        }
        catch (PlaybackServiceException error) when (error.Code == "share_no_longer_desired")
        {
            // mutation抛出后整个事务回滚，包括Repository在mutation前做的旧pending supersede。
            return new(ScreenShareCloseDisposition.NoLongerDesired, null, null);
        }
    }

    private async Task<ScreenShareTargetIdentity> ReadScreenShareTargetAsync(
        ControlDbContext database, PlaybackSession session, CancellationToken cancellationToken)
    {
        var group = await database.RuntimeGroupControls.AsNoTracking().SingleAsync(cancellationToken).ConfigureAwait(false);
        var worker = await database.WorkerOwnerships.AsNoTracking().SingleOrDefaultAsync(
            item => item.TargetKind == CommandTargetKind.Display && item.TargetId == session.WindowId, cancellationToken).ConfigureAwait(false);
        var now = _timeProvider.GetUtcNow();
        if (group.State != RuntimeGroupState.Armed || worker is null || worker.Status != WorkerOwnershipState.Online ||
            worker.WorkerInstanceId == Guid.Empty || worker.OwnerEpoch <= 0 || group.GroupEpoch <= 0 ||
            now - worker.LastTransportHeartbeat > TimeSpan.FromSeconds(5) ||
            session.PlayerLastSeenAt is null || now - session.PlayerLastSeenAt > TimeSpan.FromSeconds(5) ||
            string.IsNullOrEmpty(session.TargetDisplayLabel))
            throw new PlaybackServiceException("目标播放器未就绪或已离线，不能开始分享。", "share_player_offline");
        return new(session.WindowId, worker.WorkerInstanceId, worker.OwnerEpoch, group.GroupEpoch);
    }

    private static bool MatchesScreenShare(string? argsJson, Guid id)
    {
        if (argsJson is null) return false;
        try
        {
            using var args = JsonDocument.Parse(argsJson);
            return args.RootElement.TryGetProperty("source_type", out var kind) && kind.ValueKind == JsonValueKind.String &&
                kind.GetString() == "screen_share" && args.RootElement.TryGetProperty("share_id", out var value) &&
                value.ValueKind == JsonValueKind.String && value.TryGetGuid(out var parsed) && parsed == id;
        }
        catch (JsonException) { return false; }
    }
}
