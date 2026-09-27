// 直播源模型的公共登记接口不做假探测，支持现场常见的 UDP 组播地址。
using Microsoft.Data.Sqlite;
using ScpCv.Infrastructure.Configuration;
using ScpCv.Infrastructure.Media;
using ScpCv.Infrastructure.Persistence;

namespace ScpCv.Infrastructure.Tests;

public sealed class StreamSourceServiceTests
{
    [Fact]
    public async Task UdpMulticastCanBeRegisteredWithoutClaimingConnectivity()
    {
        var root = Path.Combine(Path.GetTempPath(), $"scp-cv-stream-test-{Guid.NewGuid():N}");
        try
        {
            var factory = new ControlDbContextFactory(new DataRootOptions { RootPath = root }, root);
            await new DatabaseInitializer(factory).InitializeAsync();
            using var writes = new WriteCoordinator(factory);
            var media = new MediaSourceService(factory, writes, factory, new MediaStorageOptions());

            var created = await media.AddStreamAsync(
                "custom_stream", "udp://@239.1.2.3:1234", "组播测试卡", null, false);
            var listed = await media.ListSourcesAsync("custom_stream", null);

            Assert.Equal("custom_stream", created.SourceType);
            Assert.Equal("unverified", created.Metadata["stream_status"].GetString());
            Assert.Equal(created.Id, Assert.Single(listed).Id);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            var full = Path.GetFullPath(root);
            var temp = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar);
            if (full.StartsWith(temp + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) &&
                Path.GetFileName(full).StartsWith("scp-cv-stream-test-", StringComparison.Ordinal) &&
                Directory.Exists(full)) Directory.Delete(full, recursive: true);
        }
    }
}
