// 文稿预览布局/query及自有目录junction重解析点保护回归；缺能力会真实失败，不伪造通过。
using System.Diagnostics;
using System.Net;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using ScpCv.Domain.Model;
using ScpCv.Infrastructure.Media;

namespace ScpCv.ControlHost.Tests;

public sealed partial class PresentationPreviewEndpointTests
{
    [Theory]
    [InlineData("fit_text=")]
    [InlineData("fit_text=1")]
    [InlineData("fit_text=TRUE")]
    [InlineData("fit_text=unknown")]
    [InlineData("fit_text=true&fit_text=false")]
    [InlineData("fit_text=true&fit_text=true")]
    public async Task InvalidOrRepeatedPreviewLayoutQueryIsRejectedByBothGetEndpoints(string query)
    {
        using var factory = new ControlHostApplicationFactory();
        using var client = factory.CreateHttpsClient();
        await AuthenticateAsync(client);
        var artifact = await PrepareOriginalAsync(factory);
        foreach (var path in new[] { "ppt-resources/", "slides/1/" })
        {
            using var response = await client.GetAsync($"/api/sources/{artifact.SourceId}/{path}?{query}");
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            using var body = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
            Assert.Equal("invalid_fit_text", body.RootElement.GetProperty("code").GetString());
        }
    }

    [Fact]
    public async Task PublishedDirectoryJunctionMakesTheWholePreviewArtifactUnavailable()
    {
        using var factory = new ControlHostApplicationFactory();
        using var client = factory.CreateHttpsClient();
        await AuthenticateAsync(client);
        var artifact = await PrepareOriginalAsync(factory);
        var linkedDirectory = Path.GetFullPath(artifact.Directory);
        var ownTarget = Path.GetFullPath(Path.Combine(factory.TemporaryRoot, "owned-junction-target"));
        var ownedRoot = Path.GetFullPath(factory.TemporaryRoot) + Path.DirectorySeparatorChar;
        Assert.StartsWith(ownedRoot, linkedDirectory, StringComparison.OrdinalIgnoreCase);
        Assert.StartsWith(ownedRoot, ownTarget, StringComparison.OrdinalIgnoreCase);
        Directory.Move(linkedDirectory, ownTarget);
        try
        {
            await CreateOwnedDirectoryJunctionAsync(linkedDirectory, ownTarget);
            Assert.True((File.GetAttributes(linkedDirectory) & FileAttributes.ReparsePoint) != 0,
                "测试平台未建立实际目录junction，不能将门禁标为已验证。");
            using var page = await client.GetAsync($"/api/sources/{artifact.SourceId}/slides/1/");
            Assert.Equal(HttpStatusCode.NotFound, page.StatusCode);
            await AssertPreviewImagesEmptyAsync(client, artifact.SourceId, fitText: false);
        }
        finally
        {
            // 仅移除已确认的junction自身，不递归删除target；再把自有制品目录恢复原位。
            if (Directory.Exists(linkedDirectory))
            {
                Assert.True((File.GetAttributes(linkedDirectory) & FileAttributes.ReparsePoint) != 0,
                    "拒绝把普通目录作为junction清理。");
                Directory.Delete(linkedDirectory, recursive: false);
            }
            Directory.Move(ownTarget, linkedDirectory);
        }
    }

    /// <summary>:param link: 已校验的自有路径。:param target: 同一fixture内自有目录。:returns: 建立目录junction，不改系统权限或开发模式。</summary>
    private static async Task CreateOwnedDirectoryJunctionAsync(string link, string target)
    {
        var startInfo = new ProcessStartInfo("cmd.exe")
        {
            UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true,
        };
        foreach (var argument in new[] { "/d", "/c", "mklink", "/J", link, target }) startInfo.ArgumentList.Add(argument);
        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("无法启动自有junction夹具。");
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        try { await process.WaitForExitAsync(timeout.Token); }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync();
            throw new TimeoutException("自有junction创建超时，未标记已验证。");
        }
        Assert.True(process.ExitCode == 0, $"无法建立自有junction：{await output}{await error}");
    }

    [Fact]
    public async Task OriginalAndFittedPreviewBytesStayIndependentAndMissingFitNeverBorrowsOriginal()
    {
        using var factory = new ControlHostApplicationFactory();
        using var client = factory.CreateHttpsClient();
        await AuthenticateAsync(client);
        var original = await PrepareOriginalAsync(factory);
        using (var missingFit = await client.GetAsync($"/api/sources/{original.SourceId}/slides/1/?fit_text=true"))
            Assert.Equal(HttpStatusCode.NotFound, missingFit.StatusCode);
        await AssertPreviewImagesEmptyAsync(client, original.SourceId, fitText: true);
        var preparation = factory.Services.GetRequiredService<MediaPreparationService>();
        await preparation.RetryPptImagesAsync(original.SourceId, fitText: true);
        var fittedJob = await preparation.ClaimNextAsync(PreparationJobKind.PptImages);
        Assert.NotNull(fittedJob);
        Assert.StartsWith("ppt-images-fit-", fittedJob.RecipeVersion);
        var staging = Path.Combine(factory.TemporaryRoot, "cache", "staging", fittedJob.JobId.ToString("N"));
        Directory.CreateDirectory(staging);
        // 在完整自有PNG后附测试标记，只用于辨认HTTP字节归属，不声称视觉/解码不同。
        var fittedBytes = original.Png.Concat("fitted-layout-owned-marker"u8.ToArray()).ToArray();
        await File.WriteAllBytesAsync(Path.Combine(staging, "page-0001.png"), fittedBytes);
        await File.WriteAllBytesAsync(Path.Combine(staging, "page-0002.png"), fittedBytes);
        var fitted = await preparation.PublishSlideImagesAsync(fittedJob.JobId, staging, 2);

        foreach (var query in new[] { string.Empty, "?fit_text=false", "?fit_text=true" })
        {
            using var page = await client.GetAsync($"/api/sources/{original.SourceId}/slides/1/{query}");
            Assert.Equal(HttpStatusCode.OK, page.StatusCode);
            Assert.Equal(query == "?fit_text=true" ? fittedBytes : original.Png, await page.Content.ReadAsByteArrayAsync());
        }
        var fittedDirectory = Path.Combine(factory.TemporaryRoot, "cache", "artifacts", fitted.RelativePath);
        File.Delete(Path.Combine(fittedDirectory, "page-0002.png"));
        using var rejectedFit = await client.GetAsync($"/api/sources/{original.SourceId}/slides/1/?fit_text=true");
        Assert.Equal(HttpStatusCode.NotFound, rejectedFit.StatusCode);
        await AssertPreviewImagesEmptyAsync(client, original.SourceId, fitText: true);
        using var preservedOriginal = await client.GetAsync($"/api/sources/{original.SourceId}/slides/1/");
        Assert.Equal(HttpStatusCode.OK, preservedOriginal.StatusCode);
        Assert.Equal(original.Png, await preservedOriginal.Content.ReadAsByteArrayAsync());
    }

    /// <summary>:param client: 已认证客户端。:param sourceId: 文稿。:param fitText: 布局。:returns: 所有资源图片均不可用的公开合同断言。</summary>
    private static async Task AssertPreviewImagesEmptyAsync(HttpClient client, long sourceId, bool fitText)
    {
        using var response = await client.GetAsync($"/api/sources/{sourceId}/ppt-resources/?fit_text={(fitText ? "true" : "false")}");
        response.EnsureSuccessStatusCode();
        using var body = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        Assert.All(body.RootElement.GetProperty("resources").EnumerateArray(), resource =>
        {
            Assert.Equal(string.Empty, resource.GetProperty("slide_image").GetString());
            Assert.Equal(string.Empty, resource.GetProperty("next_slide_image").GetString());
        });
    }
}
