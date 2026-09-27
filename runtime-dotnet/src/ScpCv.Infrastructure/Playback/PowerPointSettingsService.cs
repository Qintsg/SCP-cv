// 原生 PowerPoint 放映实验开关由 ControlHost 唯一持久化。
using Microsoft.EntityFrameworkCore;
using ScpCv.Infrastructure.Persistence;

namespace ScpCv.Infrastructure.Playback;

public sealed class PowerPointSettingsService(
    IDbContextFactory<ControlDbContext> contextFactory,
    WriteCoordinator writes,
    TimeProvider? timeProvider = null)
{
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;

    public async Task<bool> GetAsync(CancellationToken cancellationToken = default)
    {
        await using var database = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        return await database.RuntimeStates.AsNoTracking()
            .Select(state => state.ExperimentalPowerPointEnabled)
            .SingleAsync(cancellationToken).ConfigureAwait(false);
    }

    public Task SetAsync(bool enabled, CancellationToken cancellationToken = default) =>
        writes.ExecuteAsync(async (database, token) =>
        {
            var state = await database.RuntimeStates.SingleAsync(token).ConfigureAwait(false);
            state.ExperimentalPowerPointEnabled = enabled;
            state.UpdatedAt = _timeProvider.GetUtcNow();
        }, cancellationToken);
}
