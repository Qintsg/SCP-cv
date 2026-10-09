// Windows.Data.Pdf真实文件回归：缓存命中也必须更新当前页，不启动Office或显示用户窗口。
using System.Security.Cryptography;
using ScpCv.PlayerWorker.Adapters;

namespace ScpCv.Windows.Tests;

[Trait("Category", "Physical")]
public sealed class PdfPlaybackAdapterPhysicalTests
{
    /// <summary>:returns: 相邻预渲染页和回退缓存均按实际请求更新当前页，原样文件保持。</summary>
    [Fact]
    public async Task CachedAdjacentPageAndRewindUpdateCurrentPage()
    {
        var source = PdfFixture();
        var digest = SHA256.HashData(await File.ReadAllBytesAsync(source));
        await using var adapter = new PdfPlaybackAdapter();
        await adapter.OpenAsync(source, 1);
        Assert.True(adapter.PageCount >= 2, "PDF实测夹具至少两页");
        var second = await adapter.RenderPageAsync(2);
        Assert.NotEmpty(second);
        Assert.Equal(2, adapter.CurrentPage);
        Assert.Equal(second, await adapter.RenderPageAsync(2));
        var first = await adapter.RenderPageAsync(1);
        Assert.NotEmpty(first);
        Assert.Equal(1, adapter.CurrentPage);
        Assert.NotEqual(Convert.ToHexString(SHA256.HashData(first)), Convert.ToHexString(SHA256.HashData(second)));
        Assert.Equal(digest, SHA256.HashData(await File.ReadAllBytesAsync(source)));
    }

    /// <summary>:returns: 坏页号不改变当前页，释放后命中缓存也不得返回旧资源。</summary>
    [Fact]
    public async Task InvalidOrDisposedNavigationDoesNotPublishCachedResource()
    {
        await using var adapter = new PdfPlaybackAdapter();
        await adapter.OpenAsync(PdfFixture(), 1);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => adapter.RenderPageAsync(0));
        Assert.Equal(1, adapter.CurrentPage);
        await adapter.DisposeAsync();
        await Assert.ThrowsAsync<ObjectDisposedException>(() => adapter.RenderPageAsync(1));
    }

    /// <summary>:returns: 本轮明确提供的只读QA PDF；缺失即失败，不以跳过冒充实测。</summary>
    private static string PdfFixture()
    {
        var path = Environment.GetEnvironmentVariable("SCP_CV_QA_PDF");
        Assert.True(!string.IsNullOrWhiteSpace(path) && File.Exists(path), "请提供SCP_CV_QA_PDF只读实测夹具");
        return Path.GetFullPath(path!);
    }
}
