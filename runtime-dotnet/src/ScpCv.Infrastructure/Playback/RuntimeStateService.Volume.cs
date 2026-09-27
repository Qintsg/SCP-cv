// 系统音量的运行态意图和 Windows Core Audio 实际状态。
using Microsoft.EntityFrameworkCore;
using ScpCv.Contracts.Http;
using ScpCv.Domain.Model;

namespace ScpCv.Infrastructure.Playback;

public sealed partial class RuntimeStateService
{
    public async Task<SystemVolumeDto> GetSystemVolumeAsync(CancellationToken cancellationToken = default)
    {
        await using var database = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var runtime = await database.RuntimeStates.AsNoTracking().SingleAsync(cancellationToken).ConfigureAwait(false);
        var persisted = ToVolumeDto(runtime);
        if (!_systemAudio.IsHardware) return persisted;

        var current = _systemAudio.GetCurrent();
        return current.Available
            ? ToVolumeDto(current)
            : persisted with { Backend = current.Backend, SystemSynced = false };
    }

    public Task<SystemVolumeDto> SetSystemVolumeAsync(
        int? level,
        bool? muted,
        CancellationToken cancellationToken = default)
    {
        if (level is < 0 or > 100)
            throw new PlaybackServiceException("系统音量必须在 0 到 100 之间", "volume_error");

        if (_systemAudio.IsHardware)
        {
            var applied = _systemAudio.Apply(level, muted);
            if (!applied.Available)
                throw new PlaybackServiceException(applied.Detail, "system_audio_unavailable");
            return writes.ExecuteAsync(async (database, token) =>
            {
                var runtime = await database.RuntimeStates.SingleAsync(token).ConfigureAwait(false);
                runtime.VolumeLevel = applied.Level;
                runtime.VolumeMuted = applied.Muted;
                runtime.UpdatedAt = _timeProvider.GetUtcNow();
                return ToVolumeDto(applied);
            }, cancellationToken);
        }

        return writes.ExecuteAsync(async (database, token) =>
        {
            var runtime = await database.RuntimeStates.SingleAsync(token).ConfigureAwait(false);
            if (level is not null) runtime.VolumeLevel = level.Value;
            runtime.VolumeMuted = muted ?? (runtime.VolumeLevel == 0 || runtime.VolumeMuted);
            runtime.UpdatedAt = _timeProvider.GetUtcNow();
            return ToVolumeDto(runtime);
        }, cancellationToken);
    }

    private static SystemVolumeDto ToVolumeDto(RuntimeState runtime) => new()
    {
        Level = runtime.VolumeLevel,
        Muted = runtime.VolumeMuted,
        SystemSynced = false,
        Backend = "runtime_state",
    };

    private static SystemVolumeDto ToVolumeDto(SystemAudioSnapshot audio) => new()
    {
        Level = audio.Level,
        Muted = audio.Muted,
        SystemSynced = audio.Available,
        Backend = audio.Backend,
    };
}
