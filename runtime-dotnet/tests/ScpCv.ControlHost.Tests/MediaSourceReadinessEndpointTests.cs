// 旧 PPT 可用性投影与显式页图重试的非物理 REST 回归，仅使用临时 SQLite 和媒体。
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ScpCv.Domain.Model;
using ScpCv.Infrastructure.Media;
using ScpCv.Infrastructure.Persistence;
using ScpCv.Infrastructure.Playback;
using ScpCv.Infrastructure.Presentations;

namespace ScpCv.ControlHost.Tests;

public sealed partial class MediaSourceReadinessEndpointTests
{
    [Fact]
    public async Task LegacyPptWithoutSlideImagesIsUnavailableBeforeDefaultOpening()
    {
        using var factory = new ControlHostApplicationFactory();
        using var client = factory.CreateHttpsClient();
        var csrf = await AuthenticateAsync(client);
        var source = await AddLegacyPptAsync(factory);

        using var response = await client.GetAsync("/api/sources/?source_type=ppt");
        using var body = await ReadJsonAsync(response);
        var listed = Assert.Single(body.RootElement.GetProperty("sources").EnumerateArray());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(listed.GetProperty("is_available").GetBoolean());
        Assert.Equal("slide_images", listed.GetProperty("playback_mode").GetString());
        Assert.Equal(string.Empty, listed.GetProperty("preparation_state").GetString());
        Assert.Equal(0, listed.GetProperty("page_count").GetInt32());
        Assert.False(MediaSourceService.ToSourceDto(source).IsAvailable);
        Assert.True(source.IsAvailable);
        using var open = CreateJsonRequest(HttpMethod.Post, "/api/playback/1/open/", csrf,
            new { source_id = source.Id, autoplay = true });
        using var rejected = await client.SendAsync(open);
        using var error = await ReadJsonAsync(rejected);
        Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
        Assert.Equal("presentation_not_prepared", error.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task ExplicitNativeSettingKeepsAllPptResponsesAvailableForAnOpeningAttempt()
    {
        using var factory = new ControlHostApplicationFactory();
        using var client = factory.CreateHttpsClient();
        var csrf = await AuthenticateAsync(client);
        var source = await AddLegacyPptAsync(factory);
        var settings = factory.Services.GetRequiredService<PowerPointSettingsService>();
        // Simulation 的设置 HTTP 门禁仍不允许开启；只在临时库通过公共服务注入持久设置，
        // 验证生产投影和 OPEN 选择器，不会运行 Office 或把受理冒充实际放映。
        await settings.SetAsync(true);

        Assert.True((await ReadListedSourceAsync(client, source.Id)).GetProperty("is_available").GetBoolean());
        using var edit = CreateJsonRequest(HttpMethod.Patch, $"/api/sources/{source.Id}/", csrf,
            new { name = "显式原生尝试" });
        using var edited = await client.SendAsync(edit);
        using var editBody = await ReadJsonAsync(edited);
        Assert.Equal(HttpStatusCode.OK, edited.StatusCode);
        Assert.True(editBody.RootElement.GetProperty("source").GetProperty("is_available").GetBoolean());

        var media = factory.Services.GetRequiredService<MediaSourceService>();
        var folder = await media.CreateFolderAsync("原件目标", null);
        using var move = CreateJsonRequest(HttpMethod.Patch, $"/api/sources/{source.Id}/move/", csrf,
            new { folder_id = folder.Id });
        using var moved = await client.SendAsync(move);
        using var moveBody = await ReadJsonAsync(moved);
        Assert.Equal(HttpStatusCode.OK, moved.StatusCode);
        Assert.True(moveBody.RootElement.GetProperty("source").GetProperty("is_available").GetBoolean());

        using var retry = CreateJsonRequest(HttpMethod.Post, $"/api/sources/{source.Id}/prepare/", csrf, new { });
        using var retried = await client.SendAsync(retry);
        using var retryBody = await ReadJsonAsync(retried);
        Assert.Equal(HttpStatusCode.OK, retried.StatusCode);
        var queued = retryBody.RootElement.GetProperty("source");
        Assert.True(queued.GetProperty("is_available").GetBoolean());
        Assert.Equal("queued", queued.GetProperty("preparation_state").GetString());
        Assert.Equal("slide_images", queued.GetProperty("playback_mode").GetString());
        await using var upload = new MemoryStream("new-native-attempt"u8.ToArray());
        var uploaded = await media.AddUploadedAsync(upload, "new.pptx", null, null, null, null, false, false);
        Assert.True(uploaded.IsAvailable);
        var local = await media.AddLocalAsync(
            moveBody.RootElement.GetProperty("source").GetProperty("uri").GetString()!, null, null, null, false);
        Assert.True(local.IsAvailable);

        using var open = CreateJsonRequest(HttpMethod.Post, "/api/playback/1/open/", csrf,
            new { source_id = source.Id, autoplay = true });
        using var accepted = await client.SendAsync(open);
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        await settings.SetAsync(false);
        Assert.False((await ReadListedSourceAsync(client, source.Id)).GetProperty("is_available").GetBoolean());
    }

    [Theory]
    [InlineData("ready", "stale", 3, "slides", "")]
    [InlineData("ready", "current", 0, "slides", "")]
    [InlineData("ready", "current", 501, "slides", "")]
    [InlineData("ready", "current", 3, "", "")]
    [InlineData("failed", "current", 3, "slides", "failed")]
    [InlineData("queued", "current", 3, "slides", "queued")]
    [InlineData("uncertain", "current", 3, "slides", "uncertain")]
    public async Task UnusableSlideImagesDoNotClaimPreparedPages(
        string status, string digestKind, int pageCount, string directory, string expectedState)
    {
        using var factory = new ControlHostApplicationFactory();
        using var client = factory.CreateHttpsClient();
        await AuthenticateAsync(client);
        var source = await AddLegacyPptAsync(factory);
        var metadata = JsonSerializer.Serialize(new
        {
            slide_images = new
            {
                status,
                source_digest = digestKind == "current" ? source.ContentDigest : "old-digest",
                page_count = pageCount,
                directory,
            },
        });
        await factory.Services.GetRequiredService<WriteCoordinator>().ExecuteAsync(async (database, token) =>
        {
            var stored = await database.MediaSources.SingleAsync(item => item.Id == source.Id, token);
            stored.MetadataJson = metadata;
            stored.IsAvailable = status == "ready";
        });

        var listed = await ReadListedSourceAsync(client, source.Id);

        Assert.False(listed.GetProperty("is_available").GetBoolean());
        Assert.Equal(0, listed.GetProperty("page_count").GetInt32());
        Assert.Equal(expectedState, listed.GetProperty("preparation_state").GetString());
        Assert.Equal(metadata, listed.GetProperty("metadata").GetRawText());
        await factory.Services.GetRequiredService<PowerPointSettingsService>().SetAsync(true);
        var native = await ReadListedSourceAsync(client, source.Id);
        Assert.True(native.GetProperty("is_available").GetBoolean());
        Assert.Equal(0, native.GetProperty("page_count").GetInt32());
        Assert.Equal(expectedState, native.GetProperty("preparation_state").GetString());
    }

    [Fact]
    public async Task PublishedPagesAndPdfRemainAvailableInBothSettings()
    {
        using var factory = new ControlHostApplicationFactory();
        using var client = factory.CreateHttpsClient();
        await AuthenticateAsync(client);
        var media = factory.Services.GetRequiredService<MediaSourceService>();
        var preparation = factory.Services.GetRequiredService<MediaPreparationService>();
        await using var pptBytes = new MemoryStream("prepared-original"u8.ToArray());
        var ppt = await media.AddUploadedAsync(pptBytes, "prepared.pptx", null, null, null, null, false, false);
        var job = await preparation.ClaimNextAsync(PreparationJobKind.PptImages);
        Assert.NotNull(job);
        var staging = Path.Combine(factory.TemporaryRoot, "cache", "staging", job.JobId.ToString("N"));
        Directory.CreateDirectory(staging);
        byte[] png = [137, 80, 78, 71, 13, 10, 26, 10, 1, 2, 3, 4];
        await File.WriteAllBytesAsync(Path.Combine(staging, "page-0001.png"), png);
        await File.WriteAllBytesAsync(Path.Combine(staging, "page-0002.png"), png);
        await preparation.PublishSlideImagesAsync(job.JobId, staging, 2);
        await using var pdfBytes = new MemoryStream("pdf-original"u8.ToArray());
        var pdf = await media.AddUploadedAsync(pdfBytes, "deck.pdf", null, null, null, null, false, false);

        foreach (var experimental in new[] { false, true })
        {
            await factory.Services.GetRequiredService<PowerPointSettingsService>().SetAsync(experimental);
            var ready = await ReadListedSourceAsync(client, ppt.Id);
            Assert.True(ready.GetProperty("is_available").GetBoolean());
            Assert.Equal("ready", ready.GetProperty("preparation_state").GetString());
            Assert.Equal(2, ready.GetProperty("page_count").GetInt32());
            using var page = await client.GetAsync($"/api/sources/{ppt.Id}/slides/2/");
            Assert.Equal(HttpStatusCode.OK, page.StatusCode);
            Assert.Equal(png, await page.Content.ReadAsByteArrayAsync());
            var pdfSource = await ReadListedSourceAsync(client, pdf.Id);
            Assert.True(pdfSource.GetProperty("is_available").GetBoolean());
            Assert.Equal("pdf", pdfSource.GetProperty("playback_mode").GetString());
            Assert.Equal(string.Empty, pdfSource.GetProperty("preparation_state").GetString());
            Assert.Equal(0, pdfSource.GetProperty("page_count").GetInt32());
        }
    }

    [Fact]
    public async Task LegacyPptCanBeExplicitlyQueuedWithoutChangingItsOriginal()
    {
        using var factory = new ControlHostApplicationFactory();
        using var client = factory.CreateHttpsClient();
        var csrf = await AuthenticateAsync(client);
        var source = await AddLegacyPptAsync(factory);
        using var request = CreateJsonRequest(HttpMethod.Post, $"/api/sources/{source.Id}/prepare/", csrf, new { });

        using var response = await client.SendAsync(request);
        using var body = await ReadJsonAsync(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var queued = body.RootElement.GetProperty("source");
        Assert.False(queued.GetProperty("is_available").GetBoolean());
        Assert.Equal("queued", queued.GetProperty("preparation_state").GetString());
        var listed = await ReadListedSourceAsync(client, source.Id);
        Assert.False(listed.GetProperty("is_available").GetBoolean());
        Assert.Equal("queued", listed.GetProperty("preparation_state").GetString());
        using var download = await client.GetAsync($"/api/sources/{source.Id}/download/");
        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        var bytes = await download.Content.ReadAsByteArrayAsync();
        Assert.Equal("legacy-original-preserved"u8.ToArray(), bytes);
        Assert.Equal(source.ContentDigest, Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant());
    }

    [Fact]
    public async Task ExplicitRetryRebuildsAnUnusableManifestAfterASucceededJob()
    {
        using var factory = new ControlHostApplicationFactory();
        using var client = factory.CreateHttpsClient();
        var csrf = await AuthenticateAsync(client);
        var source = await AddLegacyPptAsync(factory);
        var preparation = factory.Services.GetRequiredService<MediaPreparationService>();
        await preparation.RetryPptImagesAsync(source.Id);
        var first = await preparation.ClaimNextAsync(PreparationJobKind.PptImages);
        Assert.NotNull(first);
        var staging = Path.Combine(factory.TemporaryRoot, "cache", "staging", first.JobId.ToString("N"));
        Directory.CreateDirectory(staging);
        byte[] png = [137, 80, 78, 71, 13, 10, 26, 10, 1, 2, 3, 4];
        await File.WriteAllBytesAsync(Path.Combine(staging, "page-0001.png"), png);
        await preparation.PublishSlideImagesAsync(first.JobId, staging, 1);
        var ready = await ReadListedSourceAsync(client, source.Id);
        var metadata = JsonNode.Parse(ready.GetProperty("metadata").GetRawText())!;
        metadata["slide_images"]!["directory"] = string.Empty;
        await factory.Services.GetRequiredService<WriteCoordinator>().ExecuteAsync(async (database, token) =>
        {
            var stored = await database.MediaSources.SingleAsync(item => item.Id == source.Id, token);
            stored.MetadataJson = metadata.ToJsonString();
        });
        var damaged = await ReadListedSourceAsync(client, source.Id);
        Assert.False(damaged.GetProperty("is_available").GetBoolean());
        Assert.Equal(string.Empty, damaged.GetProperty("preparation_state").GetString());
        using var retry = CreateJsonRequest(HttpMethod.Post, $"/api/sources/{source.Id}/prepare/", csrf, new { });

        using var response = await client.SendAsync(retry);
        using var body = await ReadJsonAsync(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("queued", body.RootElement.GetProperty("source").GetProperty("preparation_state").GetString());
        Assert.False(body.RootElement.GetProperty("source").GetProperty("is_available").GetBoolean());
        var next = await preparation.ClaimNextAsync(PreparationJobKind.PptImages);
        Assert.NotNull(next);
        Assert.NotEqual(first.JobId, next.JobId);
        using var download = await client.GetAsync($"/api/sources/{source.Id}/download/");
        Assert.Equal(source.ContentDigest,
            Convert.ToHexString(SHA256.HashData(await download.Content.ReadAsByteArrayAsync())).ToLowerInvariant());
    }

    [Fact]
    public async Task StreamAvailabilityContinuesToMeanAnUnverifiedOpeningAttempt()
    {
        using var factory = new ControlHostApplicationFactory();
        using var client = factory.CreateHttpsClient();
        await AuthenticateAsync(client);
        var media = factory.Services.GetRequiredService<MediaSourceService>();
        var stream = await media.AddStreamAsync("rtsp_stream", "rtsp://192.0.2.10:8554/live", "未验证流", null, false);
        await factory.Services.GetRequiredService<PowerPointSettingsService>().SetAsync(true);

        var listed = await ReadListedSourceAsync(client, stream.Id);

        Assert.True(listed.GetProperty("is_available").GetBoolean());
        Assert.Equal("unverified", listed.GetProperty("metadata").GetProperty("stream_status").GetString());
        Assert.Equal(string.Empty, listed.GetProperty("playback_mode").GetString());
    }

    [Theory]
    [InlineData(MediaSourceType.Image, true)]
    [InlineData(MediaSourceType.Video, false)]
    [InlineData(MediaSourceType.Audio, true)]
    [InlineData(MediaSourceType.Web, false)]
    public void OtherMediaPreservesItsPersistedAvailability(MediaSourceType sourceType, bool isAvailable)
    {
        var source = new MediaSource { SourceType = sourceType, Uri = "media", IsAvailable = isAvailable };
        Assert.Equal(isAvailable, MediaSourceService.ToSourceDto(source, experimentalEnabled: false).IsAvailable);
        Assert.Equal(isAvailable, MediaSourceService.ToSourceDto(source, experimentalEnabled: true).IsAvailable);
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("{broken")]
    [InlineData("{\"slide_images\":{\"status\":\"ready\",\"source_digest\":\"test-digest\",\"page_count\":\"3\",\"directory\":\"slides\"}}")]
    public async Task MalformedLegacyMetadataProjectsUnpreparedInsteadOfFailingTheLibrary(string metadata)
    {
        using var factory = new ControlHostApplicationFactory();
        using var client = factory.CreateHttpsClient();
        var csrf = await AuthenticateAsync(client);
        var source = await AddLegacyPptAsync(factory);
        source.MetadataJson = metadata.Replace("test-digest", source.ContentDigest, StringComparison.Ordinal);
        await factory.Services.GetRequiredService<WriteCoordinator>().ExecuteAsync(async (database, token) =>
        {
            var stored = await database.MediaSources.SingleAsync(item => item.Id == source.Id, token);
            stored.MetadataJson = source.MetadataJson;
        });

        Assert.Throws<PresentationPreparationException>(() =>
            PresentationPlaybackSelector.Select(source, experimentalEnabled: false));
        var projected = MediaSourceService.ToSourceDto(source);
        Assert.False(projected.IsAvailable);
        Assert.Equal(0, projected.PageCount);
        Assert.Equal(string.Empty, projected.PreparationState);
        Assert.Equal(metadata.Replace("test-digest", source.ContentDigest, StringComparison.Ordinal), source.MetadataJson);
        Assert.False((await ReadListedSourceAsync(client, source.Id)).GetProperty("is_available").GetBoolean());
        using var open = CreateJsonRequest(HttpMethod.Post, "/api/playback/1/open/", csrf,
            new { source_id = source.Id, autoplay = true });
        using var rejected = await client.SendAsync(open);
        using var error = await ReadJsonAsync(rejected);
        Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
        Assert.Equal("presentation_not_prepared", error.RootElement.GetProperty("code").GetString());
        Assert.True(MediaSourceService.ToSourceDto(source, experimentalEnabled: true).IsAvailable);
        using var retry = CreateJsonRequest(HttpMethod.Post, $"/api/sources/{source.Id}/prepare/", csrf, new { });
        using var retried = await client.SendAsync(retry);
        using var retryBody = await ReadJsonAsync(retried);
        Assert.Equal(HttpStatusCode.OK, retried.StatusCode);
        Assert.Equal("queued", retryBody.RootElement.GetProperty("source").GetProperty("preparation_state").GetString());
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void ReadyManifestWithoutAnOriginalDigestCannotAuthorizeDefaultOpening(string digest)
    {
        var source = new MediaSource
        {
            SourceType = MediaSourceType.Presentation,
            Uri = "legacy.pptx",
            ContentDigest = digest,
            MetadataJson = JsonSerializer.Serialize(new
            {
                slide_images = new { status = "ready", source_digest = digest, page_count = 1, directory = "slides" },
            }),
        };

        Assert.Throws<PresentationPreparationException>(() =>
            PresentationPlaybackSelector.Select(source, experimentalEnabled: false));
        Assert.False(MediaSourceService.ToSourceDto(source).IsAvailable);
        Assert.Equal(0, MediaSourceService.ToSourceDto(source).PageCount);
        Assert.True(MediaSourceService.ToSourceDto(source, experimentalEnabled: true).IsAvailable);
    }

    /// <summary>
    /// 从真实列表端点读取指定媒体，避免从数据库内部状态推断对外可用性。
    /// :param client: 已登录客户端。
    /// :param sourceId: 媒体标识。
    /// :returns: 已复制的媒体响应元素。
    /// </summary>
    private static async Task<JsonElement> ReadListedSourceAsync(HttpClient client, long sourceId)
    {
        using var response = await client.GetAsync("/api/sources/");
        response.EnsureSuccessStatusCode();
        using var body = await ReadJsonAsync(response);
        return Assert.Single(body.RootElement.GetProperty("sources").EnumerateArray(),
            item => item.GetProperty("id").GetInt64() == sourceId).Clone();
    }

    /// <summary>
    /// 登记升级前可用标记为 true、但没有页图元数据的测试文稿。
    /// :param factory: 拥有独立临时数据库的主机。
    /// :returns: 原件摘要已登记的旧文稿源。
    /// </summary>
    private static async Task<MediaSource> AddLegacyPptAsync(ControlHostApplicationFactory factory)
    {
        var directory = Path.Combine(factory.TemporaryRoot, "media");
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "legacy.pptx");
        var bytes = "legacy-original-preserved"u8.ToArray();
        await File.WriteAllBytesAsync(path, bytes);
        var source = new MediaSource
        {
            SourceType = MediaSourceType.Presentation,
            Name = "旧文稿",
            Uri = path,
            UploadedFile = path,
            OriginalFilename = "legacy.pptx",
            FileSize = bytes.Length,
            ContentDigest = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(),
            IsAvailable = true,
            SourceRevision = 1,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        await factory.Services.GetRequiredService<WriteCoordinator>().ExecuteAsync((database, token) =>
        {
            database.MediaSources.Add(source);
            return Task.CompletedTask;
        });
        return source;
    }

    /// <summary>
    /// 通过真实认证端点取得受保护写入所需 CSRF。
    /// :param client: 使用 Cookie 容器的测试客户端。
    /// :returns: 当前会话的 CSRF 令牌。
    /// </summary>
    private static async Task<string> AuthenticateAsync(HttpClient client)
    {
        using var response = await client.GetAsync("/api/auth/csrf/");
        using var body = await ReadJsonAsync(response);
        var csrf = body.RootElement.GetProperty("csrfToken").GetString()!;
        using var login = await client.PostAsJsonAsync("/api/auth/login/", new
        {
            username = "operator",
            password = "Old-password-123",
        });
        login.EnsureSuccessStatusCode();
        return csrf;
    }

    /// <summary>
    /// 构造受保护的 JSON 请求。
    /// :param method: HTTP 动词。
    /// :param path: 端点路径。
    /// :param csrf: 会话 CSRF。
    /// :param body: 业务参数。
    /// :returns: 带 CSRF 的请求。
    /// </summary>
    private static HttpRequestMessage CreateJsonRequest(HttpMethod method, string path, string csrf, object body)
    {
        var request = new HttpRequestMessage(method, path) { Content = JsonContent.Create(body) };
        request.Headers.Add("X-CSRFToken", csrf);
        return request;
    }

    /// <summary>
    /// 读取对外 JSON 响应。
    /// :param response: HTTP 响应。
    /// :returns: 调用方负责释放的文档。
    /// </summary>
    private static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response) =>
        await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
}
