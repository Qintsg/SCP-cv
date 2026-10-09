// 真实媒体 HTTP 响应与公开 OpenAPI 枚举一致性；仅使用 Simulation 和自有上传内容。
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace ScpCv.Contracts.Tests;

public sealed class MediaSourceSchemaResponseTests
{
    [Fact]
    public async Task UploadedPresentationDefaultModeIsCoveredByMediaSourceSchema()
    {
        using var factory = new ContractApplicationFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false,
            HandleCookies = true,
        });
        var csrf = await AuthenticateAsync(client);
        using var form = new MultipartFormDataContent();
        form.Add(new ByteArrayContent("schema-contract-original"u8.ToArray()), "file", "schema-contract.pptx");
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/sources/upload/") { Content = form };
        request.Headers.Add("X-CSRFToken", csrf);

        using var response = await client.SendAsync(request);
        using var body = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var source = body.RootElement.GetProperty("source");
        var mode = source.GetProperty("playback_mode").GetString()!;
        Assert.Equal("libreoffice", mode);
        Assert.True(source.GetProperty("is_available").GetBoolean());
        Assert.Equal("queued", source.GetProperty("preparation_state").GetString());
        Assert.Contains(mode, ReadStringEnum("playback_mode"));
    }

    [Theory]
    [InlineData("library.pptx", "ppt", "libreoffice")]
    [InlineData("library.pdf", "ppt", "pdf")]
    [InlineData("library.png", "image", "")]
    public async Task ListedUploadedSourceModeIsCoveredByMediaSourceSchema(
        string filename, string sourceType, string expectedMode)
    {
        using var factory = new ContractApplicationFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false,
            HandleCookies = true,
        });
        var csrf = await AuthenticateAsync(client);
        using var form = new MultipartFormDataContent();
        // 本用例只验证媒体登记/投影合同，不声称这些字节可渲染或具有实际文稿格式。
        form.Add(new ByteArrayContent("library-schema-contract"u8.ToArray()), "file", filename);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/sources/upload/") { Content = form };
        request.Headers.Add("X-CSRFToken", csrf);
        using var uploaded = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Created, uploaded.StatusCode);
        using var uploadedBody = await JsonDocument.ParseAsync(await uploaded.Content.ReadAsStreamAsync());
        var sourceId = uploadedBody.RootElement.GetProperty("source").GetProperty("id").GetInt64();

        using var response = await client.GetAsync("/api/sources/");
        response.EnsureSuccessStatusCode();
        using var body = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        var source = Assert.Single(body.RootElement.GetProperty("sources").EnumerateArray(),
            item => item.GetProperty("id").GetInt64() == sourceId);
        var actualMode = source.GetProperty("playback_mode").GetString()!;

        Assert.Equal(sourceType, source.GetProperty("source_type").GetString());
        Assert.Equal(expectedMode, actualMode);
        Assert.Contains(actualMode, ReadStringEnum("playback_mode"));
        Assert.True(source.GetProperty("is_available").GetBoolean());
        Assert.NotEqual("powerpoint", actualMode);
    }

    /// <summary>
    /// :param client: Simulation 主机的 Cookie 客户端。
    /// :returns: 当前真实认证会话的 CSRF token。
    /// </summary>
    private static async Task<string> AuthenticateAsync(HttpClient client)
    {
        using var csrfResponse = await client.GetAsync("/api/auth/csrf/");
        csrfResponse.EnsureSuccessStatusCode();
        using var csrfBody = await JsonDocument.ParseAsync(await csrfResponse.Content.ReadAsStreamAsync());
        using var login = await client.PostAsJsonAsync("/api/auth/login/",
            new { username = "operator", password = "Contract-password-123" });
        login.EnsureSuccessStatusCode();
        return csrfBody.RootElement.GetProperty("csrfToken").GetString()!;
    }

    /// <summary>
    /// 读取既有逐行枚举格式；不是通用 YAML/schema 验证器，完整语法仍由 Redocly 校验。
    /// :param propertyName: MediaSource 顶层字符串属性。
    /// :returns: 该属性公开允许的字符串集合；缺失或改变未支持格式时明确失败。
    /// </summary>
    private static string[] ReadStringEnum(string propertyName)
    {
        var lines = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "OpenApiSchemas", "MediaSource.yaml"));
        var propertyIndex = Array.IndexOf(lines, $"  {propertyName}:");
        Assert.True(propertyIndex >= 0, $"MediaSource schema 缺少 {propertyName}。");
        var block = lines.Skip(propertyIndex + 1)
            .TakeWhile(line => !line.StartsWith("  ", StringComparison.Ordinal) || line.StartsWith("    ", StringComparison.Ordinal))
            .ToArray();
        var enumIndex = Array.IndexOf(block, "    enum:");
        Assert.True(enumIndex >= 0, $"MediaSource.{propertyName} 未声明受支持的逐行 enum。");
        var values = block.Skip(enumIndex + 1)
            .TakeWhile(line => line.StartsWith("      - ", StringComparison.Ordinal))
            .Select(line => line[8..].Trim(' ', '\'', '"'))
            .ToArray();
        Assert.NotEmpty(values);
        return values;
    }
}
