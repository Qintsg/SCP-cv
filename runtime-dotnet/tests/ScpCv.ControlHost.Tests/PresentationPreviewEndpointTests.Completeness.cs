// 发布后清单页数与实际连续PNG集合漂移的HTTP回归；仅损坏自有测试数据库。
using System.Net;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ScpCv.Infrastructure.Persistence;

namespace ScpCv.ControlHost.Tests;

public sealed partial class PresentationPreviewEndpointTests
{
    [Fact]
    public async Task ManifestPageCountCannotHideExtraPublishedPngPages()
    {
        using var factory = new ControlHostApplicationFactory();
        using var client = factory.CreateHttpsClient();
        await AuthenticateAsync(client);
        var artifact = await PrepareOriginalAsync(factory);
        // 正式发布两页后，只改自有fixture清单页数，留下两张真实发布文件。
        await factory.Services.GetRequiredService<WriteCoordinator>().ExecuteAsync(async (database, token) =>
        {
            var source = await database.MediaSources.SingleAsync(item => item.Id == artifact.SourceId, token);
            var metadata = JsonNode.Parse(source.MetadataJson)!;
            metadata["slide_images"]!["page_count"] = 1;
            source.MetadataJson = metadata.ToJsonString();
        });

        using var png = await client.GetAsync($"/api/sources/{artifact.SourceId}/slides/1/");
        Assert.Equal(HttpStatusCode.NotFound, png.StatusCode);
        await AssertPreviewImagesEmptyAsync(client, artifact.SourceId, fitText: false);
    }
}
