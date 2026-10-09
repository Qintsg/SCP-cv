// 原/适配布局的真实预览HTTP合同；只用Simulation、正式准备服务和自有制品，不启动Office。
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using ScpCv.Domain.Model;
using ScpCv.Infrastructure.Media;

namespace ScpCv.ControlHost.Tests;

public sealed partial class PresentationPreviewEndpointTests
{
    [Fact]
    public async Task FittedOnlyPublishedArtifactProvidesResourcesAndMatchingPngWithoutOriginalFallback()
    {
        using var factory = new ControlHostApplicationFactory();
        using var client = factory.CreateHttpsClient();
        var csrf = await AuthenticateAsync(client);
        using var upload = new MultipartFormDataContent();
        upload.Add(new ByteArrayContent("preview-contract-original"u8.ToArray()), "file", "预览文稿.pptx");
        using var uploadRequest = new HttpRequestMessage(HttpMethod.Post, "/api/sources/upload/") { Content = upload };
        uploadRequest.Headers.Add("X-CSRFToken", csrf);
        using var uploaded = await client.SendAsync(uploadRequest);
        Assert.Equal(HttpStatusCode.Created, uploaded.StatusCode);
        using var uploadBody = await JsonDocument.ParseAsync(await uploaded.Content.ReadAsStreamAsync());
        var sourceId = uploadBody.RootElement.GetProperty("source").GetProperty("id").GetInt64();
        var preparation = factory.Services.GetRequiredService<MediaPreparationService>();
        var original = await preparation.ClaimNextAsync(PreparationJobKind.PptImages);
        Assert.NotNull(original);
        Assert.Equal(sourceId, original.SourceId);
        await preparation.FailSlideImagesAsync(original.JobId, "测试夹具仅准备适配版，不执行转换。");
        await preparation.RetryPptImagesAsync(sourceId, fitText: true);
        var fitted = await preparation.ClaimNextAsync(PreparationJobKind.PptImages);
        Assert.NotNull(fitted);
        Assert.Equal(sourceId, fitted.SourceId);
        Assert.StartsWith("ppt-images-fit-", fitted.RecipeVersion);
        var staging = Path.Combine(factory.TemporaryRoot, "cache", "staging", fitted.JobId.ToString("N"));
        Directory.CreateDirectory(staging);
        var png = Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+aUGsAAAAASUVORK5CYII=");
        await File.WriteAllBytesAsync(Path.Combine(staging, "page-0001.png"), png);
        await File.WriteAllBytesAsync(Path.Combine(staging, "page-0002.png"), png);
        await preparation.PublishSlideImagesAsync(fitted.JobId, staging, 2);

        using var resourcesResponse = await client.GetAsync($"/api/sources/{sourceId}/ppt-resources/?fit_text=true");
        resourcesResponse.EnsureSuccessStatusCode();
        using var resourcesBody = await JsonDocument.ParseAsync(await resourcesResponse.Content.ReadAsStreamAsync());
        var resources = resourcesBody.RootElement.GetProperty("resources").EnumerateArray().ToArray();
        Assert.Equal(2, resources.Length);
        Assert.Equal(1, resources[0].GetProperty("page_index").GetInt32());
        Assert.Equal(2, resources[1].GetProperty("page_index").GetInt32());
        var firstImage = resources[0].GetProperty("slide_image").GetString()!;
        var secondImage = resources[1].GetProperty("slide_image").GetString()!;
        Assert.Equal($"/api/sources/{sourceId}/slides/1/?fit_text=true", firstImage);
        Assert.Equal(secondImage, resources[0].GetProperty("next_slide_image").GetString());
        Assert.Equal(string.Empty, resources[1].GetProperty("next_slide_image").GetString());
        using var fittedPng = await client.GetAsync(firstImage);
        Assert.Equal(HttpStatusCode.OK, fittedPng.StatusCode);
        Assert.Equal("image/png", fittedPng.Content.Headers.ContentType?.MediaType);
        Assert.Equal(png, await fittedPng.Content.ReadAsByteArrayAsync());
        using var originalPng = await client.GetAsync($"/api/sources/{sourceId}/slides/1/");
        Assert.Equal(HttpStatusCode.NotFound, originalPng.StatusCode);
    }

    [Fact]
    public async Task MissingPublishedPageMakesAllPreviewImagesUnavailableButPreservesNotesAndMedia()
    {
        using var factory = new ControlHostApplicationFactory();
        using var client = factory.CreateHttpsClient();
        var csrf = await AuthenticateAsync(client);
        var artifact = await PrepareOriginalAsync(factory);
        using var noteRequest = new HttpRequestMessage(HttpMethod.Put, $"/api/sources/{artifact.SourceId}/ppt-resources/")
        {
            Content = JsonContent.Create(new
            {
                resources = new[] { new
                {
                    page_index = 1, slide_image = "/historical-preview.png", speaker_notes = "保留页一备注",
                    media_items = new[] { new { id = "clip-1", media_index = 1, media_type = "video", name = "保留媒体登记" } },
                } },
            }),
        };
        noteRequest.Headers.Add("X-CSRFToken", csrf);
        using var saved = await client.SendAsync(noteRequest);
        saved.EnsureSuccessStatusCode();
        // 只删除本用例正式发布目录中的自有第二页，模拟制品发布后的丢页。
        File.Delete(Path.Combine(artifact.Directory, "page-0002.png"));

        using var firstPng = await client.GetAsync($"/api/sources/{artifact.SourceId}/slides/1/");
        Assert.Equal(HttpStatusCode.NotFound, firstPng.StatusCode);
        using var resourcesResponse = await client.GetAsync($"/api/sources/{artifact.SourceId}/ppt-resources/");
        resourcesResponse.EnsureSuccessStatusCode();
        using var body = await JsonDocument.ParseAsync(await resourcesResponse.Content.ReadAsStreamAsync());
        var resources = body.RootElement.GetProperty("resources").EnumerateArray().ToArray();
        var pageOne = Assert.Single(resources, page => page.GetProperty("page_index").GetInt32() == 1);
        Assert.Equal("保留页一备注", pageOne.GetProperty("speaker_notes").GetString());
        Assert.True(pageOne.GetProperty("has_media").GetBoolean());
        Assert.Equal("clip-1", Assert.Single(pageOne.GetProperty("media_items").EnumerateArray()).GetProperty("id").GetString());
        Assert.All(resources, page =>
        {
            Assert.Equal(string.Empty, page.GetProperty("slide_image").GetString());
            Assert.Equal(string.Empty, page.GetProperty("next_slide_image").GetString());
        });
    }

    /// <summary>:param factory: 独立Simulation主机。:returns: 正式发布的自有两页制品，不执行转换器。</summary>
    private static async Task<(long SourceId, string Directory, byte[] Png)> PrepareOriginalAsync(ControlHostApplicationFactory factory)
    {
        var media = factory.Services.GetRequiredService<MediaSourceService>();
        var preparation = factory.Services.GetRequiredService<MediaPreparationService>();
        await using var originalBytes = new MemoryStream("preview-pages-owned"u8.ToArray());
        var source = await media.AddUploadedAsync(originalBytes, "pages.pptx", null, null, null, null, false, false);
        var job = await preparation.ClaimNextAsync(PreparationJobKind.PptImages);
        Assert.NotNull(job);
        var staging = Path.Combine(factory.TemporaryRoot, "cache", "staging", job.JobId.ToString("N"));
        Directory.CreateDirectory(staging);
        var png = Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+aUGsAAAAASUVORK5CYII=");
        await File.WriteAllBytesAsync(Path.Combine(staging, "page-0001.png"), png);
        await File.WriteAllBytesAsync(Path.Combine(staging, "page-0002.png"), png);
        var published = await preparation.PublishSlideImagesAsync(job.JobId, staging, 2);
        return (source.Id, Path.Combine(factory.TemporaryRoot, "cache", "artifacts", published.RelativePath), png);
    }

    /// <summary>
    /// :param client: 拥有Cookie容器的Simulation客户端。
    /// :returns: 真实认证会话的CSRF token。
    /// </summary>
    private static async Task<string> AuthenticateAsync(HttpClient client)
    {
        using var csrfResponse = await client.GetAsync("/api/auth/csrf/");
        csrfResponse.EnsureSuccessStatusCode();
        using var csrfBody = await JsonDocument.ParseAsync(await csrfResponse.Content.ReadAsStreamAsync());
        using var login = await client.PostAsJsonAsync("/api/auth/login/",
            new { username = "operator", password = "Old-password-123" });
        login.EnsureSuccessStatusCode();
        return csrfBody.RootElement.GetProperty("csrfToken").GetString()!;
    }
}
