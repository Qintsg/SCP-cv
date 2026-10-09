// 预览metadata/PNG/路径保护矩阵；先正式发布，再仅损坏自有fixture，断言公开HTTP结果。
using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ScpCv.Domain.Model;
using ScpCv.Infrastructure.Persistence;

namespace ScpCv.ControlHost.Tests;

public sealed partial class PresentationPreviewEndpointTests
{
    [Theory]
    [InlineData("status", "true")]
    [InlineData("source_digest", "{}")]
    [InlineData("source_revision", "0")]
    [InlineData("source_revision", "9223372036854775808")]
    [InlineData("source_revision", "\"1\"")]
    [InlineData("page_count", "0")]
    [InlineData("page_count", "501")]
    [InlineData("page_count", "2147483648")]
    [InlineData("page_count", "1.5")]
    [InlineData("page_count", "\"2\"")]
    [InlineData("page_count", "{}")]
    [InlineData("page_count", "null")]
    [InlineData("directory", "true")]
    public async Task InvalidMetadataFieldCannotAdvertiseOrServePreparedPreview(string field, string value)
    {
        using var factory = new ControlHostApplicationFactory();
        using var client = factory.CreateHttpsClient();
        await AuthenticateAsync(client);
        var artifact = await PrepareOriginalAsync(factory);
        await MutatePreviewSourceAsync(factory, artifact.SourceId, source =>
        {
            var metadata = JsonNode.Parse(source.MetadataJson)!;
            metadata["slide_images"]![field] = JsonNode.Parse(value);
            source.MetadataJson = metadata.ToJsonString();
        });
        await AssertPreviewUnavailableAsync(client, artifact.SourceId);
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("null")]
    [InlineData("\"not-an-object\"")]
    [InlineData("{broken")]
    public async Task InvalidMetadataRootReturnsUnavailableInsteadOfAnInternalServerError(string metadata)
    {
        using var factory = new ControlHostApplicationFactory();
        using var client = factory.CreateHttpsClient();
        await AuthenticateAsync(client);
        var artifact = await PrepareOriginalAsync(factory);
        await MutatePreviewSourceAsync(factory, artifact.SourceId, source => source.MetadataJson = metadata);
        await AssertPreviewUnavailableAsync(client, artifact.SourceId);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public async Task MatchingBlankDigestsCannotAuthorizeAnExistingArtifact(string digest)
    {
        using var factory = new ControlHostApplicationFactory();
        using var client = factory.CreateHttpsClient();
        await AuthenticateAsync(client);
        var artifact = await PrepareOriginalAsync(factory);
        await MutatePreviewSourceAsync(factory, artifact.SourceId, source =>
        {
            source.ContentDigest = digest;
            var metadata = JsonNode.Parse(source.MetadataJson)!;
            metadata["slide_images"]!["source_digest"] = digest;
            source.MetadataJson = metadata.ToJsonString();
        });
        await AssertPreviewUnavailableAsync(client, artifact.SourceId);
    }

    [Fact]
    public async Task MatchingDigestComparisonKeepsExistingCaseInsensitiveSemantics()
    {
        using var factory = new ControlHostApplicationFactory();
        using var client = factory.CreateHttpsClient();
        await AuthenticateAsync(client);
        var artifact = await PrepareOriginalAsync(factory);
        await MutatePreviewSourceAsync(factory, artifact.SourceId, source =>
        {
            var metadata = JsonNode.Parse(source.MetadataJson)!;
            metadata["slide_images"]!["source_digest"] = source.ContentDigest.ToUpperInvariant();
            source.MetadataJson = metadata.ToJsonString();
        });
        using var png = await client.GetAsync($"/api/sources/{artifact.SourceId}/slides/1/");
        Assert.Equal(HttpStatusCode.OK, png.StatusCode);
        Assert.Equal(artifact.Png, await png.Content.ReadAsByteArrayAsync());
    }

    [Theory]
    [InlineData("empty")]
    [InlineData("signature-only")]
    [InlineData("wrong-signature")]
    public async Task DamagedOtherPageInvalidatesTheCompleteArtifact(string damage)
    {
        using var factory = new ControlHostApplicationFactory();
        using var client = factory.CreateHttpsClient();
        await AuthenticateAsync(client);
        var artifact = await PrepareOriginalAsync(factory);
        var damaged = damage switch
        {
            "empty" => Array.Empty<byte>(),
            "signature-only" => artifact.Png[..8],
            _ => "not-a-png-owned-fixture"u8.ToArray(),
        };
        await File.WriteAllBytesAsync(Path.Combine(artifact.Directory, "page-0002.png"), damaged);
        await AssertPreviewUnavailableAsync(client, artifact.SourceId);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MalformedOrOutsideManagedDirectoryCannotServePreviewOrLeakItsValue(bool malformed)
    {
        using var factory = new ControlHostApplicationFactory();
        using var client = factory.CreateHttpsClient();
        await AuthenticateAsync(client);
        var artifact = await PrepareOriginalAsync(factory);
        var outside = Path.Combine(factory.TemporaryRoot, "fixture-secret-outside-artifacts");
        if (!malformed)
        {
            Directory.CreateDirectory(outside);
            await File.WriteAllBytesAsync(Path.Combine(outside, "page-0001.png"), artifact.Png);
            await File.WriteAllBytesAsync(Path.Combine(outside, "page-0002.png"), artifact.Png);
        }
        await MutatePreviewSourceAsync(factory, artifact.SourceId, source =>
        {
            var metadata = JsonNode.Parse(source.MetadataJson)!;
            metadata["slide_images"]!["directory"] = malformed ? "\0fixture-secret-path" : outside;
            source.MetadataJson = metadata.ToJsonString();
        });
        await AssertPreviewUnavailableAsync(client, artifact.SourceId);
    }

    /// <summary>:param factory: 自有主机。:param sourceId: 自有源。:param mutation: 只构造坏数据的fixture写入。:returns: 提交故障条件，测试结果只通过HTTP观察。</summary>
    private static Task MutatePreviewSourceAsync(ControlHostApplicationFactory factory, long sourceId, Action<MediaSource> mutation) =>
        factory.Services.GetRequiredService<WriteCoordinator>().ExecuteAsync(async (database, token) =>
        {
            var source = await database.MediaSources.SingleAsync(item => item.Id == sourceId, token);
            mutation(source);
        });

    /// <summary>:param client: 已认证客户端。:param sourceId: 文稿。:returns: PNG拒绝且资源图片均为空，不泄漏坏路径的公开断言。</summary>
    private static async Task AssertPreviewUnavailableAsync(HttpClient client, long sourceId)
    {
        using var png = await client.GetAsync($"/api/sources/{sourceId}/slides/1/");
        Assert.Equal(HttpStatusCode.NotFound, png.StatusCode);
        using var error = await JsonDocument.ParseAsync(await png.Content.ReadAsStreamAsync());
        Assert.Equal("media_error", error.RootElement.GetProperty("code").GetString());
        Assert.DoesNotContain("fixture-secret", error.RootElement.GetProperty("detail").GetString()!, StringComparison.Ordinal);
        await AssertPreviewImagesEmptyAsync(client, sourceId, fitText: false);
    }
}
