// 文件描述：为两个输出窗口提交绝对置顶意图并返回各自回执。项目：SCP-cv；文件：RuntimeStateService.WindowForeground.cs；作者：Qintsg；日期：2026-10-29。
using System.Text.Json;
using ScpCv.Contracts.Http;

namespace ScpCv.Infrastructure.Playback;

public sealed partial class RuntimeStateService
{
    /// <summary>:param foreground: true恢复置顶，false移到后方。:param cancellationToken: 请求寿命。:returns: 入队后的会话及命令回执，保留实际窗口状态。</summary>
    public async Task<IReadOnlyList<PlaybackSessionDto>> SetWindowsForegroundAsync(bool foreground, CancellationToken cancellationToken = default)
    {
        var before = await GetSessionsAsync(cancellationToken).ConfigureAwait(false);
        if (before.Count != Windows.Length || before.Any(session => !session.PlayerOnline || session.WindowForeground is null))
            throw new PlaybackServiceException("播放器离线或窗口状态尚未确认。", "window_state_unavailable");
        var receipts = new Dictionary<int, Guid?>();
        foreach (var windowId in Windows)
        {
            var result = await EnqueueDisplayAsync(windowId, "SET_WINDOW_FOREGROUND", JsonSerializer.Serialize(new { foreground }),
                (session, command) => command.SourceGeneration = session.DesiredGeneration, cancellationToken).ConfigureAwait(false);
            receipts.Add(windowId, result.Single(session => session.WindowId == windowId).SubmittedCommandId);
        }
        var sessions = await GetSessionsAsync(cancellationToken).ConfigureAwait(false);
        return sessions.Select(session => session with { SubmittedCommandId = receipts[session.WindowId] }).ToArray();
    }
}
