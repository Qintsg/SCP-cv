// 音频列表快照公开服务回归：实际修改须推进观测时间，重复加入保持幂等且不假称播放完成。
using System.Globalization;
using ScpCv.Infrastructure.Audio;
using ScpCv.Infrastructure.Commands;
using ScpCv.Integration.Tests.Fixtures;

namespace ScpCv.Integration.Tests;

public sealed partial class BackgroundAudioPersistenceTests
{
    /// <summary>:returns: 列表新增改变同一快照的单调时间，重复加入不产生额外改变。</summary>
    [Fact]
    public async Task AddingPlaylistSourceAdvancesObservationTimeOnlyWhenChanged()
    {
        await using var fixture = await ControlHostFixture.CreateAsync();
        var (first, _) = await SeedAudioSourcesAsync(fixture);
        var service = new BackgroundAudioService(fixture.Database, fixture.Writes,
            new CommandCoordinator(fixture.Commands, new NullCommandWakeNotifier()), fixture.TimeProvider);
        var before = await service.GetAsync();
        var added = await service.AddToPlaylistAsync(first);
        Assert.True(DateTimeOffset.Parse(added.State.UpdatedAt, CultureInfo.InvariantCulture)
            > DateTimeOffset.Parse(before.State.UpdatedAt, CultureInfo.InvariantCulture), "实际列表修改须推进快照时间");
        Assert.Single(added.Playlist);
        var duplicate = await service.AddToPlaylistAsync(first);
        Assert.Equal(added.State.UpdatedAt, duplicate.State.UpdatedAt);
        Assert.Single(duplicate.Playlist);
    }
}
