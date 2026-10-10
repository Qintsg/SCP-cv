// 分享专用音频受理：短事务重核原Worker、当前OPEN与已提交实际帧，不修改普通媒体音量或源代次。
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ScpCv.Domain.Model;

namespace ScpCv.Infrastructure.Playback;

public sealed record ScreenShareAudioCommandReceipt(Guid CommandId, int WindowId, Guid ShareId, long SourceGeneration);

public sealed partial class RuntimeStateService
{
    /// <summary>:param target: 原预约的Worker/组。:param shareId: 本人活动分享。:param generation: 原OPEN代次。:returns: 仅持久受理，不代表音频实际生效。</summary>
    public async Task<ScreenShareAudioCommandReceipt> SetScreenShareAudioAsync(ScreenShareTargetIdentity target,
        Guid shareId, long generation, int volume, bool muted, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(target);
        ValidateWindow(target.WindowId);
        if (shareId == Guid.Empty || generation <= 0 || volume is < 0 or > 100)
            throw new PlaybackServiceException("分享音频参数无效。", "share_request_invalid");
        var sessions = await EnqueueDisplayAsync(target.WindowId, "SCREEN_SHARE_AUDIO", "{}", async (database, session, command, token) =>
        {
            var current = await ReadScreenShareTargetAsync(database, session, token).ConfigureAwait(false);
            if (current != target)
                throw new PlaybackServiceException("原分享播放器或运行组已改变。", "share_player_offline");
            var args = session.MediaSourceId is not null || session.DesiredGeneration != generation ? null :
                await database.CommandRecords.AsNoTracking().Where(item => item.TargetKind == CommandTargetKind.Display &&
                    item.TargetId == target.WindowId && item.Command == "OPEN" && item.SourceGeneration == generation)
                    .OrderByDescending(item => item.TargetSequence).Select(item => item.ArgsJson).FirstOrDefaultAsync(token).ConfigureAwait(false);
            var actual = ToSessionDto(session, _timeProvider.GetUtcNow(), _screenSharePlayback);
            if (!MatchesScreenShare(args, shareId) || actual.ActualShareId != shareId || actual.ActualFrames <= 0 ||
                session.ObservedGeneration != generation || session.PlaybackState != PlaybackState.Playing || session.CleanupPending)
                throw new PlaybackServiceException("分享尚未实际接收或已不是当前内容。", "share_audio_unavailable");
            command.ArgsJson = JsonSerializer.Serialize(new { share_id = shareId, window_id = target.WindowId, volume, muted });
            command.SourceGeneration = generation;
            command.SourceRevision = 0;
            command.Deadline = command.CreatedAt.AddSeconds(3);
        }, cancellationToken).ConfigureAwait(false);
        return new(sessions.Single(item => item.WindowId == target.WindowId).SubmittedCommandId!.Value, target.WindowId, shareId, generation);
    }
}
