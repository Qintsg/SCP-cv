// 工作站目录的真实SQLite回归：探测为内存快照，不连接设备或启动媒体。
using Microsoft.EntityFrameworkCore;
using ScpCv.Domain.Model;
using ScpCv.Infrastructure.Devices;
using ScpCv.Infrastructure.Media;
using ScpCv.Integration.Tests.Fixtures;

namespace ScpCv.Integration.Tests;

public sealed class WorkstationCatalogTests
{
    /// <summary>:returns: 系统目录受保护；离线隐藏但稳定ID、预案引用及协议偏好保留。</summary>
    [Fact]
    public async Task ReachabilityFiltersDirectoryWithoutDeletingSourcesOrChangingIds()
    {
        await using var fixture = await ControlHostFixture.CreateAsync();
        var catalog = new WorkstationCatalog(fixture.Writes);
        var media = new MediaSourceService(fixture.Database, fixture.Writes, fixture.Database, new MediaStorageOptions());
        var snapshot = new Dictionary<string, InputReachability>();
        await catalog.SynchronizeAsync(snapshot);
        var folder = Assert.Single(await media.ListFoldersAsync()); Assert.Equal("工作站", folder.Name); Assert.True(folder.IsProtected);
        Assert.Empty(await media.ListSourcesAsync(null, folder.Id));
        await using var db = fixture.Database.CreateDbContext();
        var ids = await db.MediaSources.AsNoTracking().ToDictionaryAsync(source => source.SystemKey, source => source.Id);
        Assert.Equal(24, ids.Count);
        snapshot["workstation:D1-1"] = new(true, false, "reachable", fixture.TimeProvider.GetUtcNow());
        snapshot["workstation:D6-4"] = new(true, true, "reachable", fixture.TimeProvider.GetUtcNow());
        await catalog.SynchronizeAsync(snapshot);
        var visible = await media.ListSourcesAsync(null, folder.Id);
        Assert.Equal(2, visible.Count); Assert.All(visible, source => Assert.True(source.IsProtected));
        Assert.Equal("rtsp://192.168.5.51:554/live/chn0", visible.Single(source => source.Name == "D1-1").Uri);
        Assert.Equal("rtmp://192.168.5.74:1935/live/chn0", visible.Single(source => source.Name == "D6-4").Uri);
        var sourceId = visible[0].Id;
        await Assert.ThrowsAsync<MediaServiceException>(() => media.DeleteSourceAsync(sourceId));
        await Assert.ThrowsAsync<MediaServiceException>(() => media.MoveSourceAsync(sourceId, null));
        await Assert.ThrowsAsync<MediaServiceException>(() => media.AddWebAsync("https://example.test/", "不得写入", folder.Id, false));
        await Assert.ThrowsAsync<MediaServiceException>(() => media.UpdateFolderAsync(folder.Id, "改名", null, false));
        snapshot.Clear(); await catalog.SynchronizeAsync(snapshot);
        Assert.Empty(await media.ListSourcesAsync(null, folder.Id));
        var reloaded = await db.MediaSources.AsNoTracking().ToDictionaryAsync(source => source.SystemKey, source => source.Id);
        Assert.Equal(ids.OrderBy(item => item.Key), reloaded.OrderBy(item => item.Key));
        snapshot["workstation:D1-1"] = new(true, true, "reachable", fixture.TimeProvider.GetUtcNow());
        await catalog.SynchronizeAsync(snapshot);
        Assert.Equal("rtsp://192.168.5.51:554/live/chn0", Assert.Single(await media.ListSourcesAsync(null, folder.Id)).Uri);
    }

    /// <summary>:returns: 同名用户目录不被自动接管，也不插入部分内置媒体。</summary>
    [Fact]
    public async Task UserOwnedSameNameDirectoryIsNeverAdopted()
    {
        await using var fixture = await ControlHostFixture.CreateAsync();
        var media = new MediaSourceService(fixture.Database, fixture.Writes, fixture.Database, new MediaStorageOptions());
        var folder = await media.CreateFolderAsync("工作站", null);
        var catalog = new WorkstationCatalog(fixture.Writes);
        await Assert.ThrowsAsync<VenueDeviceException>(() => catalog.SynchronizeAsync(new Dictionary<string, InputReachability>()));
        Assert.False(Assert.Single(await media.ListFoldersAsync()).IsProtected);
        await using var db = fixture.Database.CreateDbContext(); Assert.Empty(await db.MediaSources.ToListAsync());
        Assert.Equal(folder.Id, Assert.Single(await media.ListFoldersAsync()).Id);
    }
}
