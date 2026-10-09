// 认证HTTP拒绝畸形或过期静态文稿清单，且不能排队或改变最后会话状态。
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ScpCv.Domain.Model;
using ScpCv.Infrastructure.Persistence;

namespace ScpCv.ControlHost.Tests;

public sealed class PresentationMetadataEndpointTests
{
    [Theory]
    [InlineData("pdf", "source_revision", "9223372036854775808", false)]
    [InlineData("pdf", "source_revision", "9223372036854775808", true)]
    [InlineData("pdf", "page_count", "2147483648", false)]
    [InlineData("pdf", "page_count", "2147483648", true)]
    [InlineData("slide_images", "source_revision", "1e999", false)]
    [InlineData("slide_images", "source_revision", "1e999", true)]
    [InlineData("slide_images", "page_count", "\"3\"", false)]
    [InlineData("slide_images", "page_count", "\"3\"", true)]
    public async Task MalformedStaticNumbersReturnPreparationErrorWithoutQueuingOrChangingSession(
        string mode, string field, string value, bool fitText)
    {
        using var factory = new ControlHostApplicationFactory();
        using var client = factory.CreateHttpsClient();
        var pdfPath = Path.Combine(factory.TemporaryRoot, "静态制品.pdf");
        await File.WriteAllTextAsync(pdfPath, "%PDF-1.4\n%%EOF\n");
        var contexts = factory.Services.GetRequiredService<IDbContextFactory<ControlDbContext>>();
        long sourceId;
        await using (var database = await contexts.CreateDbContextAsync())
        {
            var artifact = new JsonObject
            {
                ["status"] = "ready", ["source_digest"] = "fixture-digest", ["source_revision"] = 1,
                ["page_count"] = 3, ["render_semantics"] = "raster_pages",
                ["path"] = pdfPath, ["directory"] = factory.TemporaryRoot,
            };
            artifact[field] = JsonNode.Parse(value);
            var key = (fitText ? "fitted_" : string.Empty) + (mode == "pdf" ? "static_pdf" : "slide_images");
            var source = new MediaSource
            {
                SourceType = MediaSourceType.Presentation, Name = "畸形清单", Uri = "原件.pptx",
                ContentDigest = "fixture-digest", SourceRevision = 1,
                MetadataJson = JsonSerializer.Serialize(new JsonObject { [key] = artifact }),
            };
            database.MediaSources.Add(source);
            await database.SaveChangesAsync();
            sourceId = source.Id;
        }

        var csrf = await AuthenticateAsync(client);
        using var before = await ReadSessionAsync(client);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/playback/1/open/")
        {
            Content = JsonContent.Create(new { source_id = sourceId, presentation_mode = mode, fit_text = fitText }),
        };
        request.Headers.Add("X-CSRFToken", csrf);
        using var response = await client.SendAsync(request);
        using var body = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("presentation_not_prepared", body.RootElement.GetProperty("code").GetString());
        Assert.Contains(mode == "pdf" ? "PDF" : "页图", body.RootElement.GetProperty("detail").GetString()!, StringComparison.Ordinal);
        using var after = await ReadSessionAsync(client);
        foreach (var property in new[] { "source_id", "actual_source_id", "playback_state", "pending_command", "desired_generation" })
            Assert.Equal(before.RootElement.GetProperty("session").GetProperty(property).GetRawText(),
                after.RootElement.GetProperty("session").GetProperty(property).GetRawText());
        await using var check = await contexts.CreateDbContextAsync();
        Assert.Empty(await check.CommandRecords.ToArrayAsync());
    }

    /// <summary>使用测试主机正式认证流程，不连接D4或其它服务。</summary>
    private static async Task<string> AuthenticateAsync(HttpClient client)
    {
        using var csrfResponse = await client.GetAsync("/api/auth/csrf/");
        using var csrfBody = await JsonDocument.ParseAsync(await csrfResponse.Content.ReadAsStreamAsync());
        using var login = await client.PostAsJsonAsync("/api/auth/login/",
            new { username = "operator", password = "Old-password-123" });
        login.EnsureSuccessStatusCode();
        return csrfBody.RootElement.GetProperty("csrfToken").GetString()!;
    }

    /// <summary>通过公开会话合同比较失败前后状态，而非推断未执行。</summary>
    private static async Task<JsonDocument> ReadSessionAsync(HttpClient client)
    {
        using var response = await client.GetAsync("/api/sessions/1/");
        response.EnsureSuccessStatusCode();
        return await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
    }
}
