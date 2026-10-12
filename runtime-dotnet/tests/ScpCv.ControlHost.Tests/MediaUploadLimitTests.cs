// 媒体上传请求长度/畸形表单的公开HTTP门禁，不创建大内存文件或启动转换。
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace ScpCv.ControlHost.Tests;

public sealed class MediaUploadLimitTests
{
    [Fact]
    public async Task MissingMultipartBoundaryReturnsReadable400WithoutRegisteringSource()
    {
        using var factory = new ControlHostApplicationFactory();
        using var client = factory.CreateHttpsClient();
        using var token = await client.GetAsync("/api/auth/csrf/");
        using var tokenJson = JsonDocument.Parse(await token.Content.ReadAsStringAsync());
        using var login = await client.PostAsJsonAsync("/api/auth/login/", new { username = "operator", password = "Old-password-123" });
        login.EnsureSuccessStatusCode();
        using var body = new ByteArrayContent("invalid-multipart"u8.ToArray());
        body.Headers.ContentType = new("multipart/form-data");
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/sources/upload/") { Content = body };
        request.Headers.Add("X-CSRFToken", tokenJson.RootElement.GetProperty("csrfToken").GetString());
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var error = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("invalid_upload_form", error.RootElement.GetProperty("code").GetString());
        using var listed = await client.GetAsync("/api/sources/");
        using var listedJson = JsonDocument.Parse(await listed.Content.ReadAsStringAsync());
        var sources = listedJson.RootElement.GetProperty("sources").EnumerateArray().ToArray();
        Assert.Equal(6, sources.Length);
        Assert.All(sources, source => Assert.True(source.GetProperty("is_protected").GetBoolean()));
    }

    [Fact]
    public async Task DeclaredUploadAboveFiniteLimitReturnsReadable413WithoutRegisteringSource()
    {
        using var factory = new ControlHostApplicationFactory();
        using var client = factory.CreateHttpsClient();
        using var token = await client.GetAsync("/api/auth/csrf/");
        using var tokenJson = JsonDocument.Parse(await token.Content.ReadAsStringAsync());
        using var login = await client.PostAsJsonAsync("/api/auth/login/", new { username = "operator", password = "Old-password-123" });
        login.EnsureSuccessStatusCode();
        using var body = new ByteArrayContent([]);
        body.Headers.ContentType = new("multipart/form-data"); body.Headers.ContentType.Parameters.Add(new("boundary", "qa-limit"));
        body.Headers.ContentLength = 1073741825;
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/sources/upload/") { Content = body };
        request.Headers.Add("X-CSRFToken", tokenJson.RootElement.GetProperty("csrfToken").GetString());
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
        using var error = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("upload_too_large", error.RootElement.GetProperty("code").GetString());
        using var listed = await client.GetAsync("/api/sources/");
        using var listedJson = JsonDocument.Parse(await listed.Content.ReadAsStringAsync());
        var sources = listedJson.RootElement.GetProperty("sources").EnumerateArray().ToArray();
        Assert.Equal(6, sources.Length);
        Assert.All(sources, source => Assert.True(source.GetProperty("is_protected").GetBoolean()));
    }
}
