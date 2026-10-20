// 直播凭据仅使用虚构资料；测试创建/重读/修改与默认名称，不访问设备。
using Microsoft.Data.Sqlite;
using ScpCv.Infrastructure.Configuration;
using ScpCv.Infrastructure.Media;
using ScpCv.Infrastructure.Persistence;

namespace ScpCv.Infrastructure.Tests;

public sealed class StreamCredentialServiceTests
{
    [Theory]
    [InlineData("rtsp_stream", "rtsp://operator:example-pass@192.0.2.188:554/streaming/channels/501")]
    [InlineData("rtsp_stream", "rtsp://name%40site:p%3Aa%23ss%25@192.0.2.188:8554/live?channel=5")]
    [InlineData("rtsp_stream", "rtsp://operator:example-pass@[::1]:8554/live")]
    [InlineData("custom_stream", "http://operator:example-pass@192.0.2.188:8080/live.ts")]
    [InlineData("custom_stream", "https://operator:example-pass@192.0.2.188/live.m3u8")]
    [InlineData("custom_stream", "rtmp://operator:example-pass@192.0.2.188/live/channel")]
    public Task CredentialedStreamCanBeCreatedReadAndChanged(string type, string uri) => WithMediaAsync(async media =>
    {
        var created = await media.AddStreamAsync(type, uri, "认证直播", null, false);
        var listed = Assert.Single(await media.ListSourcesAsync(type, null));
        Assert.Equal(new Uri(uri).AbsoluteUri, listed.Uri);
        Assert.Equal("unverified", listed.Metadata["stream_status"].GetString());
        var replacement = uri.Replace("example-pass", "new-example-pass", StringComparison.Ordinal)
            .Replace("p%3Aa%23ss%25", "new%40pass", StringComparison.Ordinal);
        var changed = await media.UpdateSourceAsync(created.Id, null, replacement, null);
        Assert.Equal(new Uri(replacement).AbsoluteUri, changed.Uri);
        Assert.Equal(changed.Uri, Assert.Single(await media.ListSourcesAsync(type, null)).Uri);
    });

    [Fact]
    public Task AutomaticNameAndPathDoNotContainCredentials() => WithMediaAsync(async media =>
    {
        var source = await media.AddStreamAsync("rtsp_stream",
            "rtsp://operator:example-pass@192.0.2.188/live?token=query-secret", null, null, false);
        foreach (var value in new[] { source.Name, source.Path })
        {
            Assert.DoesNotContain("operator", value, StringComparison.Ordinal);
            Assert.DoesNotContain("example-pass", value, StringComparison.Ordinal);
            Assert.DoesNotContain("query-secret", value, StringComparison.Ordinal);
        }
        Assert.Contains("example-pass", source.Uri, StringComparison.Ordinal);
    });

    [Theory]
    [InlineData("rtsp_stream", "rtsp://:hidden-test-value@192.0.2.1/live")]
    [InlineData("rtsp_stream", "rtsp://name:hidden-test-value@192.0.2.1/li\nve")]
    [InlineData("rtsp_stream", "https://name:hidden-test-value@192.0.2.1/live")]
    [InlineData("srt_stream", "srt://name:hidden-test-value@192.0.2.1/live")]
    [InlineData("custom_stream", "udp://name:hidden-test-value@239.1.2.3:1234")]
    [InlineData("custom_stream", "rtp://name:hidden-test-value@239.1.2.3:1234")]
    public Task InvalidCredentialsDoNotAddOrModifySource(string type, string uri) => WithMediaAsync(async media =>
    {
        var error = await Assert.ThrowsAsync<MediaServiceException>(() => media.AddStreamAsync(type, uri, null, null, false));
        Assert.DoesNotContain("hidden-test-value", error.Message, StringComparison.Ordinal);
        Assert.Empty(await media.ListSourcesAsync(null, null));
        var original = await media.AddStreamAsync(type, type == "rtsp_stream" ? "rtsp://192.0.2.1/live"
            : type == "srt_stream" ? "srt://192.0.2.1:8890/live" : "udp://@239.1.2.3:1234", "原源", null, false);
        await Assert.ThrowsAsync<MediaServiceException>(() => media.UpdateSourceAsync(original.Id, null, uri, null));
        Assert.Equal(original.Uri, Assert.Single(await media.ListSourcesAsync(null, null)).Uri);
    });

    /// <summary>:param verify: 仅公共媒体接口。:returns: 独立临时库，结束只清理本轮精确目录。</summary>
    private static async Task WithMediaAsync(Func<MediaSourceService, Task> verify)
    {
        var root = Path.Combine(Path.GetTempPath(), $"scp-cv-credential-test-{Guid.NewGuid():N}");
        try
        {
            var factory = new ControlDbContextFactory(new DataRootOptions { RootPath = root }, root);
            await new DatabaseInitializer(factory).InitializeAsync();
            using var writes = new WriteCoordinator(factory);
            await verify(new MediaSourceService(factory, writes, factory, new MediaStorageOptions()));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            var full = Path.GetFullPath(root);
            var parent = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar);
            if (full.StartsWith(parent + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                && Path.GetFileName(full).StartsWith("scp-cv-credential-test-", StringComparison.Ordinal) && Directory.Exists(full))
                Directory.Delete(full, recursive: true);
        }
    }
}
