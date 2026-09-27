// 大屏窗口的重置、编号提示和会话投影更新。
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ScpCv.Contracts.Http;
using ScpCv.Domain.Model;
using ScpCv.Infrastructure.Persistence;

namespace ScpCv.Infrastructure.Playback;

public sealed partial class RuntimeStateService
{
    private async Task<IReadOnlyList<PlaybackSessionDto>> ResetPowerPointQueuedAsync(CancellationToken cancellationToken)
    {
        int[] windows;
        await using (var database = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false))
        {
            windows = await database.PlaybackSessions
                .Where(session => session.WindowId <= WindowId.Maximum && session.PlaybackMode == PlaybackMode.PowerPoint)
                .Select(session => session.WindowId)
                .ToArrayAsync(cancellationToken).ConfigureAwait(false);
        }

        foreach (var windowId in windows)
        {
            await EnqueueDisplayAsync(windowId, "RESET_PPT", "{}", async (database, session, command, token) =>
            {
                var sourceId = session.MediaSourceId;
                var sourceRevision = sourceId is null ? 0 : await database.MediaSources
                    .Where(source => source.Id == sourceId.Value)
                    .Select(source => source.SourceRevision).SingleOrDefaultAsync(token).ConfigureAwait(false);
                session.MediaSourceId = null;
                session.PlaybackMode = PlaybackMode.None;
                session.PlaybackState = PlaybackState.Idle;
                session.ErrorMessage = string.Empty;
                session.PendingCommand = command.Command;
                session.DesiredGeneration = checked(session.DesiredGeneration + 1);
                session.CommandArgsJson = JsonSerializer.Serialize(new { source_id = sourceId });
                command.ArgsJson = session.CommandArgsJson;
                command.SourceGeneration = session.DesiredGeneration;
                command.SourceRevision = sourceRevision;
            }, cancellationToken).ConfigureAwait(false);
        }

        return await GetSessionsAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<IReadOnlyList<PlaybackSessionDto>> ResetAllQueuedAsync(CancellationToken cancellationToken)
    {
        foreach (var windowId in Windows)
        {
            await CloseAsync(windowId, cancellationToken).ConfigureAwait(false);
        }

        return await GetSessionsAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<IReadOnlyList<PlaybackSessionDto>> ShowIdsQueuedAsync(CancellationToken cancellationToken)
    {
        foreach (var windowId in Windows)
        {
            await EnqueueDisplayAsync(windowId, "SHOW_ID", "{}", (session, command) =>
            {
                session.PendingCommand = command.Command;
            }, cancellationToken).ConfigureAwait(false);
        }

        return await GetSessionsAsync(cancellationToken).ConfigureAwait(false);
    }

    private Task<IReadOnlyList<PlaybackSessionDto>> MutateSessionAsync(
        int windowId,
        Action<PlaybackSession> mutation,
        CancellationToken cancellationToken)
    {
        ValidateWindow(windowId);
        return writes.ExecuteAsync(
            async (database, token) =>
            {
                var session = await database.PlaybackSessions.SingleAsync(
                    item => item.WindowId == windowId,
                    token).ConfigureAwait(false);
                mutation(session);
                session.LastUpdatedAt = NextTimestamp(session.LastUpdatedAt);
                return await LoadSessionsAsync(database, token).ConfigureAwait(false);
            },
            cancellationToken);
    }

    private async Task<IReadOnlyList<PlaybackSessionDto>> LoadSessionsAsync(
        ControlDbContext database,
        CancellationToken cancellationToken)
    {
        await database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        var sessions = await database.PlaybackSessions.Include(session => session.MediaSource)
            .Where(session => session.WindowId <= WindowId.Maximum)
            .OrderBy(session => session.WindowId)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        return sessions.Select(session => ToSessionDto(session, _timeProvider.GetUtcNow())).ToArray();
    }

    private DateTimeOffset NextTimestamp(DateTimeOffset previous)
    {
        var now = _timeProvider.GetUtcNow();
        return now.ToUnixTimeMilliseconds() > previous.ToUnixTimeMilliseconds()
            ? now
            : previous.AddMilliseconds(1);
    }
}
