// 墙面应用与持久发送状态；中途失败仅标不确定，不宣称物理回滚。
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using ScpCv.Contracts.Http;
using ScpCv.Domain.Model;
using ScpCv.Domain.Rules;
using ScpCv.Infrastructure.VideoWall;

namespace ScpCv.Infrastructure.Playback;

public sealed partial class RuntimeStateService
{
    public Task<RuntimeStateDto> SetRuntimeModeAsync(string value, CancellationToken cancellationToken = default)
    {
        var mode = value.Trim().ToLowerInvariant();
        if (mode is not ("single" or "double")) throw new PlaybackServiceException($"无效的大屏模式：{value}");
        return ApplyWallLayoutAsync(VideoWallLayoutPlanner.Preset(mode), 0, cancellationToken);
    }

    /// <summary>先预检、再标发送中、下发、提交；修订只绑定实际应用的草稿快照。</summary>
    public async Task<RuntimeStateDto> ApplyWallLayoutAsync(WallLayout layout, long revision, CancellationToken cancellationToken = default)
    {
        try { _ = VideoWallLayoutPlanner.Build(layout, _wallProtocol); }
        catch (VideoWallException exception) { throw new PlaybackServiceException(exception.Message, "protocol_unavailable"); }
        using var operation = await _wallGate.EnterAsync(cancellationToken).ConfigureAwait(false);
        var dispatchStarted = false;
        try
        {
            await writes.ExecuteAsync(async (database, token) =>
            {
                var state = await database.RuntimeStates.SingleAsync(token).ConfigureAwait(false);
                if (revision > 0 && state.WallLayoutDraftRevision != revision)
                    throw new VideoWallLayoutException("布局草稿已更新，请重新读取后应用。", "layout_revision_conflict");
                state.WallLayoutDispatchStatus = "dispatching";
                state.WallLayoutDispatchDetail = "正在发送控制帧；尚未确认实体画面。";
            }, cancellationToken).ConfigureAwait(false);
            var preset = WallLayoutPolicy.CapturedPreset(layout);
            var hardware = _videoWall.IsHardware;
            dispatchStarted = true;
            if (preset is not null)
                await _videoWall.DispatchAsync(preset == BigScreenMode.Single ? "single" : "double", cancellationToken).ConfigureAwait(false);
            else
                hardware = (await _videoWall.DispatchLayoutAsync(layout, cancellationToken).ConfigureAwait(false)).IsHardware;
            var muted = VideoWallStateProjection.MutedWindows(layout).ToHashSet();
            var changed = await writes.ExecuteAsync(async (database, token) =>
            {
                var state = await database.RuntimeStates.SingleAsync(token).ConfigureAwait(false);
                VideoWallStateProjection.Record(state, layout, revision, hardware);
                state.UpdatedAt = _timeProvider.GetUtcNow();
                var sessions = await database.PlaybackSessions.Where(item => item.WindowId <= WindowId.Maximum)
                    .ToListAsync(token).ConfigureAwait(false);
                return sessions.Where(item => item.IsMuted != muted.Contains(item.WindowId))
                    .Select(item => (item.WindowId, Muted: muted.Contains(item.WindowId))).ToArray();
            }, cancellationToken).ConfigureAwait(false);
            foreach (var item in changed) await SetMuteAsync(item.WindowId, item.Muted, cancellationToken).ConfigureAwait(false);
            return await GetRuntimeAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            await MarkWallUncertainAsync(dispatchStarted ? "请求取消；设备可能已收到部分控制帧，请核对墙面。"
                : "请求在发送前取消，未发送控制帧；操作状态未完整确认。", CancellationToken.None).ConfigureAwait(false);
            throw;
        }
        catch (VideoWallException exception)
        {
            await MarkWallUncertainAsync("发送未完成；墙面可能已部分变化。" + exception.Message, CancellationToken.None).ConfigureAwait(false);
            throw new PlaybackServiceException("墙面状态不确定。" + exception.Message, "video_wall_error");
        }
        catch (PlaybackServiceException exception)
        {
            await MarkWallUncertainAsync("映射已写出，但播放器静音协调未确认。" + exception.Message, CancellationToken.None).ConfigureAwait(false);
            throw new PlaybackServiceException("墙面切换后的静音协调未确认。" + exception.Message, "wall_mute_failed");
        }
        catch (Exception exception) when (exception is DbUpdateException or DbException or InvalidOperationException)
        {
            await MarkWallUncertainAsync(dispatchStarted ? "控制帧可能已写出，但状态提交或静音协调未确认。"
                : "发送前操作状态提交未确认，未发送控制帧。", CancellationToken.None).ConfigureAwait(false);
            throw new PlaybackServiceException("墙面状态提交未确认，请核对实际画面。" + exception.Message, "wall_state_persistence_failed");
        }
    }

    private async Task MarkWallUncertainAsync(string detail, CancellationToken cancellationToken)
    {
        try
        {
            await writes.ExecuteAsync(async (database, token) =>
            {
                var state = await database.RuntimeStates.SingleAsync(token).ConfigureAwait(false);
                state.WallLayoutDispatchStatus = "uncertain";
                state.WallLayoutDispatchDetail = detail;
                state.UpdatedAt = _timeProvider.GetUtcNow();
            }, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is DbUpdateException or DbException or InvalidOperationException)
        {
            // 不确定标记也无法写入时，保留外层原始失败；启动恢复仍会识别dispatching。
            // 不宣称已经落库、已回滚或设备未变化，也不重放设备命令。
        }
    }
}
