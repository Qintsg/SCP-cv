// 列表缩略图必须由当前制品生成，不信任历史PptResource图地址；只用正式发布与自有Simulation根。
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using ScpCv.Domain.Model;
using ScpCv.Infrastructure.Media;
using ScpCv.Infrastructure.Persistence;

namespace ScpCv.ControlHost.Tests;

public sealed class PresentationLibraryPreviewTests
{
    /// <summary>:param historicalImage: 历史外链或错源图地址。:returns: 备注仍保存，列表仅广告受验证本源原版第一页。</summary>
    [Theory]
    [InlineData("https://example.invalid/untrusted-preview.png")]
    [InlineData("/api/sources/999999/slides/1/")]
    public async Task PublishedPresentationThumbnailIgnoresLegacySlideImageRows(string historicalImage)
    {
        using var factory = new ControlHostApplicationFactory();
        using var client = factory.CreateHttpsClient();
        var csrf = await AuthenticateAsync(client);
        var artifact = await PublishOriginalAsync(factory);
        using var save = JsonRequest(HttpMethod.Put, $"/api/sources/{artifact.SourceId}/ppt-resources/", csrf, new
        {
            resources = new[] { new
            { page_index = 1, slide_image = historicalImage, speaker_notes = "列表回归保留备注", media_items = Array.Empty<object>() } },
        });
        using var saved = await client.SendAsync(save);
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);

        using var listing = await client.GetAsync("/api/sources/?source_type=ppt");
        Assert.Equal(HttpStatusCode.OK, listing.StatusCode);
        using var body = await JsonDocument.ParseAsync(await listing.Content.ReadAsStreamAsync());
        var source = Assert.Single(body.RootElement.GetProperty("sources").EnumerateArray(),
            item => item.GetProperty("id").GetInt64() == artifact.SourceId);
        var firstPage = $"/api/sources/{artifact.SourceId}/slides/1/";
        Assert.Equal(firstPage, source.GetProperty("preview_url").GetString());
        Assert.Equal(firstPage, source.GetProperty("thumbnail_url").GetString());
        Assert.Equal("image", source.GetProperty("preview_kind").GetString());
        Assert.Equal(2, source.GetProperty("page_count").GetInt32());
        Assert.Equal("ready", source.GetProperty("preparation_state").GetString());
        using var image = await client.GetAsync(firstPage);
        Assert.Equal(HttpStatusCode.OK, image.StatusCode);
        Assert.Equal(artifact.Png, await image.Content.ReadAsByteArrayAsync());
        using var resources = await client.GetAsync($"/api/sources/{artifact.SourceId}/ppt-resources/");
        resources.EnsureSuccessStatusCode();
        using var resourceBody = await JsonDocument.ParseAsync(await resources.Content.ReadAsStreamAsync());
        var pageOne = Assert.Single(resourceBody.RootElement.GetProperty("resources").EnumerateArray(),
            item => item.GetProperty("page_index").GetInt32() == 1);
        Assert.Equal("列表回归保留备注", pageOne.GetProperty("speaker_notes").GetString());
    }

    /// <summary>:param invalidation: 丢页、原件新修订或仅适配制品。:returns: 默认列表不借历史行/其它布局广告原版图，原件能力独立。</summary>
    [Theory]
    [InlineData("missing-page")]
    [InlineData("stale-revision")]
    [InlineData("fit-only")]
    public async Task UnavailableOriginalArtifactDoesNotAdvertiseLibraryPreviewOrPreparedPages(string invalidation)
    {
        using var factory = new ControlHostApplicationFactory();
        using var client = factory.CreateHttpsClient();
        var csrf = await AuthenticateAsync(client);
        var artifact = await PublishOriginalAsync(factory, fitOnly: invalidation == "fit-only");
        using var save = JsonRequest(HttpMethod.Put, $"/api/sources/{artifact.SourceId}/ppt-resources/", csrf, new
        { resources = new[] { new { page_index = 1, slide_image = "/historical-preview.png", speaker_notes = "失效后备注仍保留" } } });
        using var saved = await client.SendAsync(save);
        saved.EnsureSuccessStatusCode();
        if (invalidation == "missing-page")
        {
            var missing = Path.GetFullPath(Path.Combine(artifact.Directory, "page-0002.png"));
            var root = Path.GetFullPath(Path.Combine(factory.TemporaryRoot, "cache", "artifacts"));
            Assert.StartsWith(root + Path.DirectorySeparatorChar, missing, StringComparison.OrdinalIgnoreCase);
            File.Delete(missing);
        }
        else if (invalidation == "stale-revision")
        {
            // 仅构造自有库中的新原件修订，仍由正式写门禁提交；行为只通过HTTP投影断言。
            var writes = factory.Services.GetRequiredService<WriteCoordinator>();
            await writes.ExecuteAsync(async (database, token) =>
            {
                var source = await database.MediaSources.SingleAsync(item => item.Id == artifact.SourceId, token);
                source.SourceRevision = checked(source.SourceRevision + 1);
            });
        }
        using var listing = await client.GetAsync("/api/sources/?source_type=ppt");
        listing.EnsureSuccessStatusCode();
        using var body = await JsonDocument.ParseAsync(await listing.Content.ReadAsStreamAsync());
        var result = Assert.Single(body.RootElement.GetProperty("sources").EnumerateArray(),
            item => item.GetProperty("id").GetInt64() == artifact.SourceId);
        Assert.Equal(string.Empty, result.GetProperty("preview_url").GetString());
        Assert.Equal(string.Empty, result.GetProperty("thumbnail_url").GetString());
        Assert.Equal("icon", result.GetProperty("preview_kind").GetString());
        Assert.Equal(0, result.GetProperty("page_count").GetInt32());
        Assert.NotEqual("ready", result.GetProperty("preparation_state").GetString());
        Assert.True(result.GetProperty("is_available").GetBoolean(), "页图失效不能冒充原件缺失。");
    }

    /// <summary>:returns: 没有主机根的兼容投影仅解释元数据，不广告路径或历史图地址为物理预览。</summary>
    [Fact]
    public void StaticMetadataOnlyProjectionDoesNotAdvertisePresentationImages()
    {
        var source = new MediaSource
        {
            Id = 101, SourceType = MediaSourceType.Presentation, Uri = "metadata-only-original.pptx",
            SourceRevision = 1, ContentDigest = "fixture-digest",
            MetadataJson = JsonSerializer.Serialize(new { slide_images = new
            { status = "ready", source_digest = "fixture-digest", source_revision = 1, directory = @"C:\unverified-cache\slides", page_count = 2 } }),
        };
        source.PptResources.Add(new PptResource { PageIndex = 1, SlideImage = "https://example.invalid/historical.png" });
        var result = MediaSourceService.ToSourceDto(source);
        Assert.Equal(2, result.PageCount);
        Assert.Equal("ready", result.PreparationState);
        Assert.Equal(string.Empty, result.PreviewUrl);
        Assert.Equal(string.Empty, result.ThumbnailUrl);
        Assert.Equal("icon", result.PreviewKind);
    }

    /// <summary>:returns: 正式制品丢页后，用户明确prepare实际排新job，不能只沿旧Succeeded返回空投影。</summary>
    [Fact]
    public async Task MissingPublishedPageCanBeExplicitlyPreparedAgainThroughHttp()
    {
        using var factory = new ControlHostApplicationFactory();
        using var client = factory.CreateHttpsClient();
        var csrf = await AuthenticateAsync(client);
        var artifact = await PublishOriginalAsync(factory);
        var missing = Path.GetFullPath(Path.Combine(artifact.Directory, "page-0002.png"));
        var root = Path.GetFullPath(Path.Combine(factory.TemporaryRoot, "cache", "artifacts"));
        Assert.StartsWith(root + Path.DirectorySeparatorChar, missing, StringComparison.OrdinalIgnoreCase);
        File.Delete(missing);
        using var prepare = JsonRequest(HttpMethod.Post, $"/api/sources/{artifact.SourceId}/prepare/", csrf, new { fit_text = false });
        using var response = await client.SendAsync(prepare);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        var projected = body.RootElement.GetProperty("source");
        Assert.Equal("queued", projected.GetProperty("preparation_state").GetString());
        Assert.Equal(string.Empty, projected.GetProperty("preview_url").GetString());
        var preparation = factory.Services.GetRequiredService<MediaPreparationService>();
        var newJob = await preparation.ClaimNextAsync(PreparationJobKind.PptImages);
        Assert.NotNull(newJob);
        Assert.Equal(artifact.SourceId, newJob.SourceId);
        Assert.NotEqual(artifact.JobId, newJob.JobId);
        Assert.False(newJob.RecipeVersion.StartsWith("ppt-images-fit-", StringComparison.Ordinal));
    }

    /// <summary>:param factory: 自有主机根。:param fitOnly: 仅发布适配版。:returns: 正式准备服务发布的自有两页，不启动转换。</summary>
    private static async Task<(long SourceId, string Directory, byte[] Png, Guid JobId)> PublishOriginalAsync(ControlHostApplicationFactory factory, bool fitOnly = false)
    {
        var media = factory.Services.GetRequiredService<MediaSourceService>();
        var preparation = factory.Services.GetRequiredService<MediaPreparationService>();
        await using var upload = new MemoryStream("library-preview-owned-original"u8.ToArray());
        var source = await media.AddUploadedAsync(upload, "library-preview.pptx", null, null, null, null, false, false);
        var job = await preparation.ClaimNextAsync(PreparationJobKind.PptImages);
        Assert.NotNull(job);
        Assert.Equal(source.Id, job.SourceId);
        if (fitOnly)
        {
            await preparation.FailSlideImagesAsync(job.JobId, "夹具仅准备适配版，不执行转换。");
            await preparation.RetryPptImagesAsync(source.Id, fitText: true);
            job = await preparation.ClaimNextAsync(PreparationJobKind.PptImages);
            Assert.NotNull(job);
            Assert.Equal(source.Id, job.SourceId);
            Assert.StartsWith("ppt-images-fit-", job.RecipeVersion);
        }
        var staging = Path.Combine(factory.TemporaryRoot, "cache", "staging", job.JobId.ToString("N"));
        Directory.CreateDirectory(staging);
        var png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+aUGsAAAAASUVORK5CYII=");
        await File.WriteAllBytesAsync(Path.Combine(staging, "page-0001.png"), png);
        await File.WriteAllBytesAsync(Path.Combine(staging, "page-0002.png"), png);
        var manifest = await preparation.PublishSlideImagesAsync(job.JobId, staging, 2);
        return (source.Id, Path.Combine(factory.TemporaryRoot, "cache", "artifacts", manifest.RelativePath), png, job.JobId);
    }

    /// <summary>:param method: HTTP方法。:param path: 同主机API。:param csrf: 本会话token。:param body: 合同字段。:returns: 受保护写请求。</summary>
    private static HttpRequestMessage JsonRequest(HttpMethod method, string path, string csrf, object body)
    {
        var request = new HttpRequestMessage(method, path) { Content = JsonContent.Create(body) };
        request.Headers.Add("X-CSRFToken", csrf);
        return request;
    }

    /// <summary>:param client: 自有Simulation客户端。:returns: 真认证会话CSRF，不使用现场账户。</summary>
    private static async Task<string> AuthenticateAsync(HttpClient client)
    {
        using var csrf = await client.GetAsync("/api/auth/csrf/");
        csrf.EnsureSuccessStatusCode();
        using var body = await JsonDocument.ParseAsync(await csrf.Content.ReadAsStreamAsync());
        using var login = await client.PostAsJsonAsync("/api/auth/login/", new
        { username = "operator", password = "Old-password-123" });
        login.EnsureSuccessStatusCode();
        return body.RootElement.GetProperty("csrfToken").GetString()!;
    }
}
