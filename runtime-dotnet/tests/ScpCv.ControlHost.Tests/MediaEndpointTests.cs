// 受保护媒体上传、直播源登记、移动、下载和 PPT 页图合同回归。
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using ScpCv.Domain.Model;
using ScpCv.Infrastructure.Media;

namespace ScpCv.ControlHost.Tests;

public sealed class MediaEndpointTests
{
    [Fact]
    public async Task SingleSourceDeletionReportsLockedFileAndKeepsItInLibrary()
    {
        if (!OperatingSystem.IsWindows()) return;
        using var factory = new ControlHostApplicationFactory();
        using var client = factory.CreateHttpsClient();
        var csrf = await AuthenticateAsync(client);
        var media = factory.Services.GetRequiredService<MediaSourceService>();
        await using var bytes = new MemoryStream("locked-original"u8.ToArray());
        var source = await media.AddUploadedAsync(bytes, "locked.png", null, null, null, null, false, false);
        using var fileLock = new FileStream(source.Uri, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var request = CreateJsonRequest(HttpMethod.Delete, $"/api/sources/{source.Id}/", csrf, new { });

        using var response = await client.SendAsync(request);
        using var body = await ReadJsonAsync(response);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("media_error", body.RootElement.GetProperty("code").GetString());
        Assert.Contains("删除", body.RootElement.GetProperty("detail").GetString());
        Assert.Contains(await media.ListSourcesAsync(null, null), item => item.Id == source.Id);
        Assert.True(File.Exists(source.Uri));
    }

    [Fact]
    public async Task OperatorCanRegisterRtspStreamAndFindItInMediaLibrary()
    {
        using var factory = new ControlHostApplicationFactory();
        using var client = factory.CreateHttpsClient();
        var csrf = await AuthenticateAsync(client);
        using var register = CreateJsonRequest(HttpMethod.Post, "/api/sources/streams/", csrf,
            new { source_type = "rtsp_stream", url = "rtsp://192.0.2.10:8554/live", name = "测试摄像头" });

        using var created = await client.SendAsync(register);
        using var body = await ReadJsonAsync(created);

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var source = body.RootElement.GetProperty("source");
        Assert.Equal("rtsp_stream", source.GetProperty("source_type").GetString());
        Assert.Equal("rtsp://192.0.2.10:8554/live", source.GetProperty("uri").GetString());
        using var listed = await client.GetAsync("/api/sources/?source_type=rtsp_stream");
        using var library = await ReadJsonAsync(listed);
        Assert.Contains(library.RootElement.GetProperty("sources").EnumerateArray(), item =>
            item.GetProperty("id").GetInt64() == source.GetProperty("id").GetInt64());
    }

    [Fact]
    public async Task OperatorCanRegisterSrtStreamWithoutClaimingItIsOnline()
    {
        using var factory = new ControlHostApplicationFactory();
        using var client = factory.CreateHttpsClient();
        var csrf = await AuthenticateAsync(client);
        using var register = CreateJsonRequest(HttpMethod.Post, "/api/sources/streams/", csrf,
            new { source_type = "srt_stream", url = "srt://192.0.2.11:8890?streamid=read:demo", name = "课堂推流" });

        using var created = await client.SendAsync(register);
        using var body = await ReadJsonAsync(created);

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var source = body.RootElement.GetProperty("source");
        Assert.Equal("srt_stream", source.GetProperty("source_type").GetString());
        Assert.Equal("srt://192.0.2.11:8890/?streamid=read:demo", source.GetProperty("uri").GetString());
        Assert.Equal("unverified", source.GetProperty("metadata").GetProperty("stream_status").GetString());
    }

    [Fact]
    public async Task OperatorCanRegisterHttpMediaStreamDistinctFromWebPage()
    {
        using var factory = new ControlHostApplicationFactory();
        using var client = factory.CreateHttpsClient();
        var csrf = await AuthenticateAsync(client);
        using var register = CreateJsonRequest(HttpMethod.Post, "/api/sources/streams/", csrf,
            new { source_type = "custom_stream", url = "http://192.0.2.12:8080/live.ts", name = "现场编码器" });

        using var created = await client.SendAsync(register);
        using var body = await ReadJsonAsync(created);

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var source = body.RootElement.GetProperty("source");
        Assert.Equal("custom_stream", source.GetProperty("source_type").GetString());
        Assert.Equal("http://192.0.2.12:8080/live.ts", source.GetProperty("uri").GetString());
    }

    [Fact]
    public async Task OperatorCanChangeStreamAddressForNextOpening()
    {
        using var factory = new ControlHostApplicationFactory();
        using var client = factory.CreateHttpsClient();
        var csrf = await AuthenticateAsync(client);
        using var register = CreateJsonRequest(HttpMethod.Post, "/api/sources/streams/", csrf,
            new { source_type = "srt_stream", url = "srt://192.0.2.11:8890/live", name = "课堂推流" });
        using var created = await client.SendAsync(register);
        using var createdBody = await ReadJsonAsync(created);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var sourceId = createdBody.RootElement.GetProperty("source").GetProperty("id").GetInt64();
        using var edit = CreateJsonRequest(HttpMethod.Patch, $"/api/sources/{sourceId}/", csrf,
            new { uri = "srt://192.0.2.22:9000/live" });

        using var updated = await client.SendAsync(edit);
        using var body = await ReadJsonAsync(updated);

        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        Assert.Equal("srt://192.0.2.22:9000/live", body.RootElement.GetProperty("source").GetProperty("uri").GetString());
    }

    [Theory]
    [InlineData("rtsp_stream", "http://192.0.2.10/live")]
    [InlineData("srt_stream", "srt://user:secret@192.0.2.11:8890/live")]
    [InlineData("custom_stream", "file:///C:/private.txt")]
    public async Task InvalidStreamUrlIsRejectedWithoutAddingSource(string sourceType, string url)
    {
        using var factory = new ControlHostApplicationFactory();
        using var client = factory.CreateHttpsClient();
        var csrf = await AuthenticateAsync(client);
        using var beforeResponse = await client.GetAsync("/api/sources/");
        using var before = await ReadJsonAsync(beforeResponse);
        var count = before.RootElement.GetProperty("sources").GetArrayLength();
        using var register = CreateJsonRequest(HttpMethod.Post, "/api/sources/streams/", csrf,
            new { source_type = sourceType, url, name = "无效直播源" });

        using var rejected = await client.SendAsync(register);
        using var afterResponse = await client.GetAsync("/api/sources/");
        using var after = await ReadJsonAsync(afterResponse);

        Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
        Assert.Equal(count, after.RootElement.GetProperty("sources").GetArrayLength());
    }

    [Fact]
    public async Task AnonymousClientCannotRegisterStreamSource()
    {
        using var factory = new ControlHostApplicationFactory();
        using var client = factory.CreateHttpsClient();
        using var request = HttpRequestMessageForAnonymousStream();

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static HttpRequestMessage HttpRequestMessageForAnonymousStream() =>
        new(HttpMethod.Post, "/api/sources/streams/")
        {
            Content = JsonContent.Create(new { source_type = "rtsp_stream", url = "rtsp://192.0.2.10/live" }),
        };

    [Fact]
    public async Task PreparedPptSlideImageRequiresSessionAndReturnsPublishedPng()
    {
        using var factory = new ControlHostApplicationFactory();
        using var client = factory.CreateHttpsClient();
        var media = factory.Services.GetRequiredService<MediaSourceService>();
        var preparation = factory.Services.GetRequiredService<MediaPreparationService>();
        await using var upload = new MemoryStream("pptx-test"u8.ToArray());
        var source = await media.AddUploadedAsync(upload, "deck.pptx", null, null, null, null, false, false);
        var job = await preparation.ClaimNextAsync(PreparationJobKind.PptImages);
        Assert.NotNull(job);
        var staging = Path.Combine(factory.TemporaryRoot, "cache", "staging", job.JobId.ToString("N"));
        Directory.CreateDirectory(staging);
        byte[] png = [137, 80, 78, 71, 13, 10, 26, 10, 1, 2, 3, 4];
        await File.WriteAllBytesAsync(Path.Combine(staging, "page-0001.png"), png);
        await preparation.PublishSlideImagesAsync(job.JobId, staging, 1);

        using var anonymous = await client.GetAsync($"/api/sources/{source.Id}/slides/1/");
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        await AuthenticateAsync(client);
        using var response = await client.GetAsync($"/api/sources/{source.Id}/slides/1/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("image/png", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(png, await response.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task FailedPptPreparationCanBeRetriedThroughProtectedEndpoint()
    {
        using var factory = new ControlHostApplicationFactory();
        using var client = factory.CreateHttpsClient();
        var media = factory.Services.GetRequiredService<MediaSourceService>();
        var preparation = factory.Services.GetRequiredService<MediaPreparationService>();
        await using var upload = new MemoryStream("pptx-retry"u8.ToArray());
        var source = await media.AddUploadedAsync(upload, "retry.pptx", null, null, null, null, false, false);
        var job = await preparation.ClaimNextAsync(PreparationJobKind.PptImages);
        Assert.NotNull(job);
        await preparation.FailSlideImagesAsync(job.JobId, "转换失败");
        var csrf = await AuthenticateAsync(client);

        using var request = CreateJsonRequest(HttpMethod.Post, $"/api/sources/{source.Id}/prepare/", csrf, new { });
        using var response = await client.SendAsync(request);
        using var body = await ReadJsonAsync(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("queued", body.RootElement.GetProperty("source").GetProperty("preparation_state").GetString());
    }

    private const string InitialPassword = "Old-password-123";

    [Fact]
    public async Task UploadedBytesRemainIdenticalAfterSourceAndFolderMoves()
    {
        using var factory = new ControlHostApplicationFactory();
        using var client = factory.CreateHttpsClient();
        var csrf = await AuthenticateAsync(client);
        var bytes = "api-move-hash-原件"u8.ToArray();
        using var createFolder = CreateJsonRequest(HttpMethod.Post, "/api/folders/", csrf, new { name = "PPT文件" });
        using var folderResponse = await client.SendAsync(createFolder);
        using var folderBody = await ReadJsonAsync(folderResponse);
        Assert.Equal(HttpStatusCode.Created, folderResponse.StatusCode);
        var folderId = folderBody.RootElement.GetProperty("folder").GetProperty("id").GetInt64();

        using var form = new MultipartFormDataContent();
        form.Add(new ByteArrayContent(bytes), "file", "a.png");
        using var upload = new HttpRequestMessage(HttpMethod.Post, "/api/sources/upload/") { Content = form };
        upload.Headers.Add("X-CSRFToken", csrf);
        using var uploaded = await client.SendAsync(upload);
        using var uploadBody = await ReadJsonAsync(uploaded);
        Assert.Equal(HttpStatusCode.Created, uploaded.StatusCode);
        var sourceId = uploadBody.RootElement.GetProperty("source").GetProperty("id").GetInt64();

        using var moveSource = CreateJsonRequest(HttpMethod.Patch, $"/api/sources/{sourceId}/move/", csrf,
            new { folder_id = folderId });
        using var moved = await client.SendAsync(moveSource);
        using var movedBody = await ReadJsonAsync(moved);
        Assert.Equal(HttpStatusCode.OK, moved.StatusCode);
        var firstPath = movedBody.RootElement.GetProperty("source").GetProperty("uri").GetString()!;
        Assert.Equal(Path.Combine(factory.TemporaryRoot, "media", "PPT文件", "a.png"), firstPath);

        using var rename = CreateJsonRequest(HttpMethod.Patch, $"/api/folders/{folderId}/", csrf,
            new { name = "演示资料" });
        using var renamed = await client.SendAsync(rename);
        Assert.Equal(HttpStatusCode.OK, renamed.StatusCode);
        Assert.False(File.Exists(firstPath));
        Assert.True(File.Exists(Path.Combine(factory.TemporaryRoot, "media", "演示资料", "a.png")));

        using var download = await client.GetAsync($"/api/sources/{sourceId}/download/");
        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        Assert.Equal(bytes, await download.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task FolderAndWebSourceCrudPreservesCompatibilityContract()
    {
        using var factory = new ControlHostApplicationFactory();
        using var client = factory.CreateHttpsClient();
        var csrf = await AuthenticateAsync(client);

        using var createFolder = CreateJsonRequest(HttpMethod.Post, "/api/folders/", csrf, new { name = "网页" });
        using var createdFolder = await client.SendAsync(createFolder);
        using var folderBody = await ReadJsonAsync(createdFolder);
        Assert.True(
            createdFolder.StatusCode == HttpStatusCode.Created,
            $"期望 201，实际 {(int)createdFolder.StatusCode}：{folderBody.RootElement.GetRawText()}");
        var folderId = folderBody.RootElement.GetProperty("folder").GetProperty("id").GetInt64();

        using var createSource = CreateJsonRequest(
            HttpMethod.Post,
            "/api/sources/web/",
            csrf,
            new { url = "example.test/dashboard", name = "监控页", folder_id = folderId, preheat_enabled = true });
        using var createdSource = await client.SendAsync(createSource);
        using var sourceBody = await ReadJsonAsync(createdSource);
        Assert.Equal(HttpStatusCode.Created, createdSource.StatusCode);
        var source = sourceBody.RootElement.GetProperty("source");
        var sourceId = source.GetProperty("id").GetInt64();
        Assert.Equal("http://example.test/dashboard", source.GetProperty("uri").GetString());
        Assert.Equal(folderId, source.GetProperty("folder_id").GetInt64());
        Assert.True(source.GetProperty("keep_alive").GetBoolean());
        Assert.True(source.GetProperty("preheat_enabled").GetBoolean());

        using var updateSource = CreateJsonRequest(
            HttpMethod.Patch,
            $"/api/sources/{sourceId}/",
            csrf,
            new { name = "更新后的监控页", uri = "https://example.test/live", keep_alive = false });
        using var updatedSource = await client.SendAsync(updateSource);
        using var updatedBody = await ReadJsonAsync(updatedSource);
        Assert.Equal(HttpStatusCode.OK, updatedSource.StatusCode);
        Assert.Equal("更新后的监控页", updatedBody.RootElement.GetProperty("source").GetProperty("name").GetString());
        Assert.Equal("https://example.test/live", updatedBody.RootElement.GetProperty("source").GetProperty("uri").GetString());
        Assert.False(updatedBody.RootElement.GetProperty("source").GetProperty("preheat_enabled").GetBoolean());

        using var moveSource = CreateJsonRequest(
            HttpMethod.Patch,
            $"/api/sources/{sourceId}/move/",
            csrf,
            new { folder_id = (long?)null });
        using var movedSource = await client.SendAsync(moveSource);
        using var movedBody = await ReadJsonAsync(movedSource);
        Assert.Equal(HttpStatusCode.OK, movedSource.StatusCode);
        Assert.Equal(JsonValueKind.Null, movedBody.RootElement.GetProperty("source").GetProperty("folder_id").ValueKind);

        using var filtered = await client.GetAsync("/api/sources/?source_type=web&folder_id=-1");
        using var filteredBody = await ReadJsonAsync(filtered);
        Assert.Equal(HttpStatusCode.OK, filtered.StatusCode);
        Assert.Single(filteredBody.RootElement.GetProperty("sources").EnumerateArray());

        using var deleteSource = CreateJsonRequest(HttpMethod.Delete, $"/api/sources/{sourceId}/", csrf, new { });
        using var deletedSource = await client.SendAsync(deleteSource);
        Assert.Equal(HttpStatusCode.OK, deletedSource.StatusCode);

        using var deleteFolder = CreateJsonRequest(HttpMethod.Delete, $"/api/folders/{folderId}/", csrf, new { });
        using var deletedFolder = await client.SendAsync(deleteFolder);
        Assert.Equal(HttpStatusCode.OK, deletedFolder.StatusCode);
    }

    [Fact]
    public async Task UploadPreviewDownloadAndLocalPathBoundaryUsePlaybackHostFiles()
    {
        using var factory = new ControlHostApplicationFactory();
        using var client = factory.CreateHttpsClient();
        var csrf = await AuthenticateAsync(client);
        var imageBytes = Encoding.UTF8.GetBytes("contract-image");

        using var upload = new MultipartFormDataContent();
        using var fileContent = new ByteArrayContent(imageBytes);
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
        upload.Add(fileContent, "file", "poster.png");
        upload.Add(new StringContent("海报"), "name");
        using var uploadRequest = new HttpRequestMessage(HttpMethod.Post, "/api/sources/upload/") { Content = upload };
        uploadRequest.Headers.Add("X-CSRFToken", csrf);
        using var uploaded = await client.SendAsync(uploadRequest);
        using var uploadedBody = await ReadJsonAsync(uploaded);
        Assert.True(
            uploaded.StatusCode == HttpStatusCode.Created,
            $"期望 201，实际 {(int)uploaded.StatusCode}：{uploadedBody.RootElement.GetRawText()}");
        var uploadedSource = uploadedBody.RootElement.GetProperty("source");
        var sourceId = uploadedSource.GetProperty("id").GetInt64();
        var managedPath = uploadedSource.GetProperty("uri").GetString()!;
        Assert.True(File.Exists(managedPath));

        using var preview = await client.GetAsync($"/api/sources/{sourceId}/preview/");
        Assert.Equal(HttpStatusCode.OK, preview.StatusCode);
        Assert.Equal(imageBytes, await preview.Content.ReadAsByteArrayAsync());
        Assert.Null(preview.Content.Headers.ContentDisposition);

        using var download = await client.GetAsync($"/api/sources/{sourceId}/download/");
        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        Assert.Equal("poster.png", download.Content.Headers.ContentDisposition?.FileNameStar);
        Assert.Equal(imageBytes, await download.Content.ReadAsByteArrayAsync());

        var localPath = Path.Combine(factory.TemporaryRoot, "local.mp4");
        await File.WriteAllBytesAsync(localPath, [1, 2, 3, 4]);
        using var addLocal = CreateJsonRequest(
            HttpMethod.Post,
            "/api/sources/local/",
            csrf,
            new { path = localPath });
        using var local = await client.SendAsync(addLocal);
        Assert.Equal(HttpStatusCode.Created, local.StatusCode);

        var outsidePath = Path.Combine(
            Path.GetDirectoryName(factory.TemporaryRoot)!,
            $"outside-{Guid.NewGuid():N}.png");
        await File.WriteAllBytesAsync(outsidePath, [1]);
        try
        {
            using var addOutside = CreateJsonRequest(
                HttpMethod.Post,
                "/api/sources/local/",
                csrf,
                new { path = outsidePath });
            using var rejected = await client.SendAsync(addOutside);
            using var rejectedBody = await ReadJsonAsync(rejected);
            Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
            Assert.Equal("media_error", rejectedBody.RootElement.GetProperty("code").GetString());
        }
        finally
        {
            File.Delete(outsidePath);
        }

        using var deleteUpload = CreateJsonRequest(HttpMethod.Delete, $"/api/sources/{sourceId}/", csrf, new { });
        using var deleted = await client.SendAsync(deleteUpload);
        Assert.Equal(HttpStatusCode.OK, deleted.StatusCode);
        Assert.False(File.Exists(managedPath));
        Assert.True(File.Exists(localPath));
    }

    private static async Task<string> AuthenticateAsync(HttpClient client)
    {
        using var csrfResponse = await client.GetAsync("/api/auth/csrf/");
        using var csrfBody = await ReadJsonAsync(csrfResponse);
        var csrf = csrfBody.RootElement.GetProperty("csrfToken").GetString()!;
        using var login = await client.PostAsJsonAsync(
            "/api/auth/login/",
            new { username = "operator", password = InitialPassword });
        login.EnsureSuccessStatusCode();
        return csrf;
    }

    private static HttpRequestMessage CreateJsonRequest(HttpMethod method, string path, string csrf, object body)
    {
        var request = new HttpRequestMessage(method, path) { Content = JsonContent.Create(body) };
        request.Headers.Add("X-CSRFToken", csrf);
        return request;
    }

    private static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response) =>
        await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
}
