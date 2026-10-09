// 可选布局声明若存在必须与所选制品键一致；公开HTTP不能将冲突原图冒称适配图。
using System.Net;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using ScpCv.Domain.Model;
using ScpCv.Infrastructure.Media;

namespace ScpCv.ControlHost.Tests;

public sealed partial class PresentationPreviewEndpointTests
{
    [Theory]
    [InlineData(false, "\"fit\"")]
    [InlineData(true, "\"original\"")]
    [InlineData(false, "true")]
    [InlineData(false, "\"unknown\"")]
    public async Task ConflictingOrInvalidDeclaredTextLayoutCannotAdvertiseOrServeTheSelectedPreview(bool fitText, string declaredLayout)
    {
        using var factory = new ControlHostApplicationFactory();
        using var client = factory.CreateHttpsClient();
        await AuthenticateAsync(client);
        var original = await PrepareOriginalAsync(factory);
        if (fitText)
        {
            var preparation = factory.Services.GetRequiredService<MediaPreparationService>();
            await preparation.RetryPptImagesAsync(original.SourceId, fitText: true);
            var job = await preparation.ClaimNextAsync(PreparationJobKind.PptImages);
            Assert.NotNull(job);
            Assert.StartsWith("ppt-images-fit-", job.RecipeVersion);
            var staging = Path.Combine(factory.TemporaryRoot, "cache", "staging", job.JobId.ToString("N"));
            Directory.CreateDirectory(staging);
            await File.WriteAllBytesAsync(Path.Combine(staging, "page-0001.png"), original.Png);
            await File.WriteAllBytesAsync(Path.Combine(staging, "page-0002.png"), original.Png);
            await preparation.PublishSlideImagesAsync(job.JobId, staging, 2);
        }
        await MutatePreviewSourceAsync(factory, original.SourceId, source =>
        {
            var metadata = JsonNode.Parse(source.MetadataJson)!;
            metadata[fitText ? "fitted_slide_images" : "slide_images"]!["text_layout"] = JsonNode.Parse(declaredLayout);
            source.MetadataJson = metadata.ToJsonString();
        });

        using var png = await client.GetAsync($"/api/sources/{original.SourceId}/slides/1/?fit_text={(fitText ? "true" : "false")}");
        Assert.Equal(HttpStatusCode.NotFound, png.StatusCode);
        await AssertPreviewImagesEmptyAsync(client, original.SourceId, fitText);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task HistoricalArtifactWithoutDeclaredTextLayoutKeepsItsExplicitKeyContract(bool fitText)
    {
        using var factory = new ControlHostApplicationFactory();
        using var client = factory.CreateHttpsClient();
        await AuthenticateAsync(client);
        var original = await PrepareOriginalAsync(factory);
        if (fitText)
        {
            var preparation = factory.Services.GetRequiredService<MediaPreparationService>();
            await preparation.RetryPptImagesAsync(original.SourceId, fitText: true);
            var job = await preparation.ClaimNextAsync(PreparationJobKind.PptImages);
            Assert.NotNull(job);
            Assert.StartsWith("ppt-images-fit-", job.RecipeVersion);
            var staging = Path.Combine(factory.TemporaryRoot, "cache", "staging", job.JobId.ToString("N"));
            Directory.CreateDirectory(staging);
            await File.WriteAllBytesAsync(Path.Combine(staging, "page-0001.png"), original.Png);
            await File.WriteAllBytesAsync(Path.Combine(staging, "page-0002.png"), original.Png);
            await preparation.PublishSlideImagesAsync(job.JobId, staging, 2);
        }
        await MutatePreviewSourceAsync(factory, original.SourceId, source =>
        {
            var metadata = JsonNode.Parse(source.MetadataJson)!;
            ((JsonObject)metadata[fitText ? "fitted_slide_images" : "slide_images"]!).Remove("text_layout");
            source.MetadataJson = metadata.ToJsonString();
        });

        var query = $"?fit_text={(fitText ? "true" : "false")}";
        using var png = await client.GetAsync($"/api/sources/{original.SourceId}/slides/1/{query}");
        Assert.Equal(HttpStatusCode.OK, png.StatusCode);
        Assert.Equal(original.Png, await png.Content.ReadAsByteArrayAsync());
        using var resourcesResponse = await client.GetAsync($"/api/sources/{original.SourceId}/ppt-resources/{query}");
        resourcesResponse.EnsureSuccessStatusCode();
        using var body = await System.Text.Json.JsonDocument.ParseAsync(await resourcesResponse.Content.ReadAsStreamAsync());
        Assert.Equal(2, body.RootElement.GetProperty("resources").GetArrayLength());
    }
}
