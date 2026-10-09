// 背景音乐列表循环公开服务回归；真实独立SQLite，结束通知是外部Worker事件，不伪造播放器出画。
using Microsoft.EntityFrameworkCore;
using ScpCv.Domain.Model;
using ScpCv.Domain.Rules;
using ScpCv.Infrastructure.Audio;
using ScpCv.Infrastructure.Commands;
using ScpCv.Integration.Tests.Fixtures;

namespace ScpCv.Integration.Tests;

public sealed partial class BackgroundAudioPersistenceTests
{
    /// <summary>:param loop: 列表循环。:returns: 单曲列表自然结束也区分停止和重新打开，不重复接受事件。</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SingleTrackUsesListLoopPolicy(bool loop)
    {
        await using var fixture = await ControlHostFixture.CreateAsync();
        var (source, _) = await SeedAudioSourcesAsync(fixture);
        var service = new BackgroundAudioService(fixture.Database, fixture.Writes,
            new CommandCoordinator(fixture.Commands, new NullCommandWakeNotifier()), fixture.TimeProvider);
        await service.SetPlaylistAsync([source]); await service.SetLoopAsync(loop); await service.PlaySourceAsync(source);
        await MarkAudioPlayingAsync(fixture);
        var finished = new AudioFinishedEvent(Guid.NewGuid(), source, 1);
        var snapshot = await service.HandleFinishedAsync(finished);
        Assert.Equal(source, snapshot.State.SourceId);
        Assert.Equal(loop ? "loading" : "stopped", snapshot.State.PlaybackState);
        await service.HandleFinishedAsync(finished);
        await using var database = fixture.Database.CreateDbContext();
        var command = Assert.Single(await database.CommandRecords.Where(item => item.TriggerEventId == finished.EventId).ToArrayAsync());
        Assert.Equal(loop ? "OPEN" : "STOP", command.Command);
    }

    /// <summary>:param previous: 显式方向。:returns: 手动上一首/下一首保持原有回环，不被自动末尾停止规则误锁。</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExplicitNavigationStillWrapsWithLoopOff(bool previous)
    {
        await using var fixture = await ControlHostFixture.CreateAsync();
        var (first, last) = await SeedAudioSourcesAsync(fixture);
        var service = new BackgroundAudioService(fixture.Database, fixture.Writes,
            new CommandCoordinator(fixture.Commands, new NullCommandWakeNotifier()), fixture.TimeProvider);
        await service.SetPlaylistAsync([first, last]); await service.SetLoopAsync(false);
        await service.PlaySourceAsync(previous ? first : last);
        var result = await service.AdvanceAsync(previous);
        Assert.Equal(previous ? last : first, result.State.SourceId);
        Assert.Equal("loading", result.State.PlaybackState);
    }

    /// <summary>:param loop: 列表循环。:param fromLast: 当前曲目位置。:returns: 自然结束只按列表边界选择下一曲，并保持事件幂等。</summary>
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task NaturalEndAdvancesOnceAccordingToListBoundary(bool loop, bool fromLast)
    {
        await using var fixture = await ControlHostFixture.CreateAsync();
        var (first, last) = await SeedAudioSourcesAsync(fixture);
        var service = new BackgroundAudioService(fixture.Database, fixture.Writes,
            new CommandCoordinator(fixture.Commands, new NullCommandWakeNotifier()), fixture.TimeProvider);
        await service.SetPlaylistAsync([first, last]);
        await service.SetLoopAsync(loop);
        var current = fromLast ? last : first;
        await service.PlaySourceAsync(current);
        await MarkAudioPlayingAsync(fixture);
        var finished = new AudioFinishedEvent(Guid.NewGuid(), current, 1);
        var advanced = await service.HandleFinishedAsync(finished);
        Assert.Equal(fromLast ? first : last, advanced.State.SourceId);
        Assert.Equal("loading", advanced.State.PlaybackState);
        var duplicate = await service.HandleFinishedAsync(finished);
        Assert.Equal(advanced.State.SourceId, duplicate.State.SourceId);
        await using var database = fixture.Database.CreateDbContext();
        Assert.Single(await database.CommandRecords.Where(item => item.TriggerEventId == finished.EventId).ToArrayAsync());
    }

    /// <summary>:returns: 末曲自然结束STOP也持久记录事件，不因重复、旧源或旧代次结束通知再开首曲。</summary>
    [Fact]
    public async Task LastTrackStopIsDurableAndLateFinishedDoesNotReopen()
    {
        await using var fixture = await ControlHostFixture.CreateAsync();
        var (first, last) = await SeedAudioSourcesAsync(fixture);
        var service = new BackgroundAudioService(fixture.Database, fixture.Writes,
            new CommandCoordinator(fixture.Commands, new NullCommandWakeNotifier()), fixture.TimeProvider);
        await service.SetPlaylistAsync([first, last]);
        await service.SetLoopAsync(false);
        await service.PlaySourceAsync(last);
        await MarkAudioPlayingAsync(fixture);
        var ended = new AudioFinishedEvent(Guid.NewGuid(), last, 1);
        await service.HandleFinishedAsync(ended);
        await service.HandleFinishedAsync(ended);
        await service.HandleFinishedAsync(new AudioFinishedEvent(Guid.NewGuid(), last, 0));
        await service.HandleFinishedAsync(new AudioFinishedEvent(Guid.NewGuid(), first, 1));
        Assert.Equal("stopped", (await service.GetAsync()).State.PlaybackState);
        await using var database = fixture.Database.CreateDbContext();
        var stop = Assert.Single(await database.CommandRecords.Where(item => item.TriggerEventId == ended.EventId).ToArrayAsync());
        Assert.Equal("STOP", stop.Command);
        Assert.Equal(1, stop.SourceGeneration);
    }

    /// <summary>:returns: 关闭列表循环时最后一首自然结束应停在最后一首，不自动打开首曲。</summary>
    [Fact]
    public async Task NaturalEndAtLastTrackWithoutLoopStopsInsteadOfWrapping()
    {
        await using var fixture = await ControlHostFixture.CreateAsync();
        var (first, last) = await SeedAudioSourcesAsync(fixture);
        var service = new BackgroundAudioService(fixture.Database, fixture.Writes,
            new CommandCoordinator(fixture.Commands, new NullCommandWakeNotifier()), fixture.TimeProvider);
        await service.SetPlaylistAsync([first, last]);
        await service.SetLoopAsync(false);
        await service.PlaySourceAsync(last);
        await MarkAudioPlayingAsync(fixture);
        var earlierPending = (await service.GetAsync()).State.PendingCommand;
        var ended = await service.HandleFinishedAsync(new AudioFinishedEvent(Guid.NewGuid(), last, 1));
        Assert.Equal(last, ended.State.SourceId);
        Assert.Equal("stopped", ended.State.PlaybackState);
        Assert.Equal(earlierPending, ended.State.PendingCommand);
    }
}
