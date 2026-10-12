// 现场目录幂等与精确音量帧合同；未知绑定必须零写出。
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using ScpCv.Infrastructure.Configuration;
using ScpCv.Infrastructure.Devices;
using ScpCv.Infrastructure.Media;
using ScpCv.Infrastructure.Persistence;

namespace ScpCv.Infrastructure.Tests;

public sealed class VenueDeviceTests
{
    [Fact]
    public void VolumeFramesAreExactAndRepliesValidated()
    {
        Assert.Equal("FBFC619000000001640000FDFE", Convert.ToHexString(VenueProtocol.ScreenVolume(100)));
        Assert.Equal("FBFC6191000000000000FDFE", Convert.ToHexString(VenueProtocol.ScreenVolumeQuery()));
        Assert.Equal(100, VenueProtocol.ReadScreenVolume(Convert.FromHexString("FCFD619100000000640000FEFF")));
        Assert.Throws<ArgumentOutOfRangeException>(() => VenueProtocol.ScreenVolume(101));
        Assert.Throws<VenueDeviceException>(() => VenueProtocol.ReadScreenVolume(new byte[13]));
    }

    [Fact]
    public async Task CameraCatalogIsIdempotentAndProtectedThroughEveryWriteEntry()
    {
        var root = Path.Combine(Path.GetTempPath(), $"scp-cv-venue-{Guid.NewGuid():N}");
        try
        {
            var factory = new ControlDbContextFactory(new DataRootOptions { RootPath = root }, root);
            await new DatabaseInitializer(factory).InitializeAsync();
            using var writes = new WriteCoordinator(factory);
            var catalog = new CameraCatalog(factory, writes, new VenueOptions());
            await catalog.SynchronizeAsync();
            await catalog.SynchronizeAsync();
            var media = new MediaSourceService(factory, writes, factory, new MediaStorageOptions());
            var folder = Assert.Single(await media.ListFoldersAsync());
            Assert.Equal("/Camera", folder.RelativePath);
            Assert.True(folder.IsProtected);
            await Assert.ThrowsAsync<MediaServiceException>(() => media.CreateFolderAsync("bad", folder.Id));
            await Assert.ThrowsAsync<MediaServiceException>(() => media.UpdateFolderAsync(folder.Id, "bad", null, false));
            await Assert.ThrowsAsync<MediaServiceException>(() => media.DeleteFolderAsync(folder.Id, true));
            await using var bytes = new MemoryStream("one"u8.ToArray());
            await Assert.ThrowsAsync<MediaServiceException>(() => media.AddUploadedAsync(bytes, "bad.png", null, null, null, folder.Id, false, false));
            await using var db = factory.CreateDbContext();
            var sources = await db.MediaSources.ToListAsync();
            Assert.Equal(6, sources.Count);
            Assert.Equal(5, sources.Count(source => source.Uri.StartsWith("rtsp://192.168.5.8", StringComparison.Ordinal)));
            Assert.False(sources.Single(source => source.SystemKey == "camera:fixed").IsAvailable);
            foreach (var source in sources)
            {
                await Assert.ThrowsAsync<MediaServiceException>(() => media.MoveSourceAsync(source.Id, null));
                await Assert.ThrowsAsync<MediaServiceException>(() => media.DeleteSourceAsync(source.Id));
            }
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }
}
