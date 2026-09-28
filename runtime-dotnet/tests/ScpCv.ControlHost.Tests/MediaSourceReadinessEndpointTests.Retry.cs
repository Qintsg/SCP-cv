// 缺摘要旧 PPT 的显式重试只读取选定原件，并保持原件、写入和幂等语义。
using System.Net;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ScpCv.Domain.Model;
using ScpCv.Infrastructure.Configuration;
using ScpCv.Infrastructure.Media;
using ScpCv.Infrastructure.Persistence;

namespace ScpCv.ControlHost.Tests;

public sealed partial class MediaSourceReadinessEndpointTests
{
    [Fact]
    public async Task ExplicitRetryRestoresAMissingLegacyDigestAndQueuesOnlyOnce()
    {
        using var factory = new ControlHostApplicationFactory();
        using var client = factory.CreateHttpsClient();
        var csrf = await AuthenticateAsync(client);
        var source = await AddLegacyPptAsync(factory);
        await ClearLegacyDigestAsync(factory, source.Id);
        var before = await ReadListedSourceAsync(client, source.Id);
        Assert.False(before.GetProperty("is_available").GetBoolean());
        Assert.Equal(string.Empty, before.GetProperty("preparation_state").GetString());
        using var request = CreateJsonRequest(HttpMethod.Post, $"/api/sources/{source.Id}/prepare/", csrf, new { });

        using var response = await client.SendAsync(request);
        using var body = await ReadJsonAsync(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var queued = body.RootElement.GetProperty("source");
        Assert.Equal("queued", queued.GetProperty("preparation_state").GetString());
        Assert.False(queued.GetProperty("is_available").GetBoolean());
        Assert.Equal(source.Uri, queued.GetProperty("uri").GetString());
        Assert.Equal(source.ContentDigest,
            queued.GetProperty("metadata").GetProperty("slide_images").GetProperty("source_digest").GetString());
        var preparation = factory.Services.GetRequiredService<MediaPreparationService>();
        var job = await preparation.ClaimNextAsync(PreparationJobKind.PptImages);
        Assert.NotNull(job);
        Assert.Equal(source.ContentDigest, job.SourceDigest);
        Assert.Equal(1, job.SourceRevision);
        using var repeatedRequest = CreateJsonRequest(HttpMethod.Post, $"/api/sources/{source.Id}/prepare/", csrf, new { });
        using var repeated = await client.SendAsync(repeatedRequest);
        Assert.Equal(HttpStatusCode.OK, repeated.StatusCode);
        Assert.Null(await preparation.ClaimNextAsync(PreparationJobKind.PptImages));
        using var download = await client.GetAsync($"/api/sources/{source.Id}/download/");
        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        Assert.Equal("legacy-original-preserved"u8.ToArray(), await download.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task UnreadableLegacyOriginalDoesNotQueueOrClaimThatItsDigestWasRecovered()
    {
        if (!OperatingSystem.IsWindows()) return;
        using var factory = new ControlHostApplicationFactory();
        using var client = factory.CreateHttpsClient();
        var csrf = await AuthenticateAsync(client);
        var source = await AddLegacyPptAsync(factory);
        await ClearLegacyDigestAsync(factory, source.Id);
        using var fileLock = new FileStream(source.Uri, FileMode.Open, FileAccess.Read, FileShare.None);
        using var request = CreateJsonRequest(HttpMethod.Post, $"/api/sources/{source.Id}/prepare/", csrf, new { });

        using var response = await client.SendAsync(request);
        using var error = await ReadJsonAsync(response);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("media_error", error.RootElement.GetProperty("code").GetString());
        var listed = await ReadListedSourceAsync(client, source.Id);
        Assert.False(listed.GetProperty("is_available").GetBoolean());
        Assert.Equal(string.Empty, listed.GetProperty("preparation_state").GetString());
        Assert.False(listed.GetProperty("metadata").TryGetProperty("slide_images", out _));
        Assert.Null(await factory.Services.GetRequiredService<MediaPreparationService>()
            .ClaimNextAsync(PreparationJobKind.PptImages));
    }

    [Fact]
    public async Task WaitingForLegacyDigestDoesNotBlockAnIndependentLibraryWrite()
    {
        using var factory = new ControlHostApplicationFactory();
        var source = await AddLegacyPptAsync(factory);
        await ClearLegacyDigestAsync(factory, source.Id);
        var reader = new ControlledOriginalDigestReader();
        var preparation = CreatePreparationWithReader(factory, reader);
        var retry = preparation.RetryPptImagesAsync(source.Id);
        await reader.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var write = factory.Services.GetRequiredService<MediaSourceService>().CreateFolderAsync("独立写入", null);
        try
        {
            var folder = await write.WaitAsync(TimeSpan.FromSeconds(2));
            Assert.Equal("独立写入", folder.Name);
        }
        finally
        {
            reader.Release();
            await Task.WhenAll(retry, write).WaitAsync(TimeSpan.FromSeconds(5));
        }
        Assert.Equal("queued", (await retry).PreparationState);
    }

    [Fact]
    public async Task ConcurrentLegacyRetriesShareTheCommittedQueuedJob()
    {
        using var factory = new ControlHostApplicationFactory();
        var source = await AddLegacyPptAsync(factory);
        await ClearLegacyDigestAsync(factory, source.Id);
        var firstReader = new ControlledOriginalDigestReader();
        var secondReader = new ControlledOriginalDigestReader();
        var firstPreparation = CreatePreparationWithReader(factory, firstReader);
        var secondPreparation = CreatePreparationWithReader(factory, secondReader);
        var first = firstPreparation.RetryPptImagesAsync(source.Id);
        await firstReader.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var second = secondPreparation.RetryPptImagesAsync(source.Id);
        await secondReader.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        firstReader.Release();
        var firstResult = await first.WaitAsync(TimeSpan.FromSeconds(5));
        secondReader.Release();
        var secondResult = await second.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal("queued", firstResult.PreparationState);
        Assert.Equal("queued", secondResult.PreparationState);
        Assert.Equal(source.Uri, secondResult.Uri);
        var job = await firstPreparation.ClaimNextAsync(PreparationJobKind.PptImages);
        Assert.NotNull(job);
        Assert.Equal(source.ContentDigest, job.SourceDigest);
        Assert.Equal(1, job.SourceRevision);
        Assert.Null(await firstPreparation.ClaimNextAsync(PreparationJobKind.PptImages));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ChangedOriginalIdentityRejectsTheCompletedDigestBeforeQueuing(bool moveOriginal)
    {
        using var factory = new ControlHostApplicationFactory();
        using var client = factory.CreateHttpsClient();
        await AuthenticateAsync(client);
        var source = await AddLegacyPptAsync(factory);
        await ClearLegacyDigestAsync(factory, source.Id);
        var media = factory.Services.GetRequiredService<MediaSourceService>();
        var target = await media.CreateFolderAsync("读取期间移动", null);
        var reader = new ControlledOriginalDigestReader();
        var preparation = CreatePreparationWithReader(factory, reader);
        var retry = preparation.RetryPptImagesAsync(source.Id);
        await reader.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        try
        {
            if (moveOriginal)
                await media.MoveSourceAsync(source.Id, target.Id).WaitAsync(TimeSpan.FromSeconds(5));
            else
                await factory.Services.GetRequiredService<WriteCoordinator>().ExecuteAsync(async (database, token) =>
                {
                    var stored = await database.MediaSources.SingleAsync(item => item.Id == source.Id, token);
                    stored.SourceRevision = 7;
                }).WaitAsync(TimeSpan.FromSeconds(5));
        }
        finally
        {
            reader.Release();
        }

        var error = await Assert.ThrowsAsync<MediaServiceException>(() => retry);

        Assert.Contains("已变化", error.Message);
        Assert.Null(await preparation.ClaimNextAsync(PreparationJobKind.PptImages));
        var listed = await ReadListedSourceAsync(client, source.Id);
        Assert.False(listed.GetProperty("is_available").GetBoolean());
        Assert.Equal(string.Empty, listed.GetProperty("preparation_state").GetString());
        var current = factory.Services.GetRequiredService<MediaPreparationService>();
        Assert.Equal("queued", (await current.RetryPptImagesAsync(source.Id)).PreparationState);
        var job = await current.ClaimNextAsync(PreparationJobKind.PptImages);
        Assert.NotNull(job);
        Assert.Equal(source.ContentDigest, job.SourceDigest);
        Assert.Equal(moveOriginal ? 1 : 8, job.SourceRevision);
    }

    [Theory]
    [InlineData(OperationStatus.Running)]
    [InlineData(OperationStatus.Uncertain)]
    public async Task UnresolvedOfficeJobAppearingDuringDigestReadPreventsNewQueuing(OperationStatus status)
    {
        using var factory = new ControlHostApplicationFactory();
        using var client = factory.CreateHttpsClient();
        await AuthenticateAsync(client);
        var source = await AddLegacyPptAsync(factory);
        await ClearLegacyDigestAsync(factory, source.Id);
        var reader = new ControlledOriginalDigestReader();
        var preparation = CreatePreparationWithReader(factory, reader);
        var retry = preparation.RetryPptImagesAsync(source.Id);
        await reader.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        try
        {
            await factory.Services.GetRequiredService<WriteCoordinator>().ExecuteAsync((database, token) =>
            {
                database.MediaPreparationJobs.Add(new MediaPreparationJob
                {
                    JobId = Guid.NewGuid(), SourceId = source.Id, SourceRevision = 0,
                    Kind = PreparationJobKind.PptImages, Status = status, RecipeVersion = "legacy-unresolved",
                });
                return Task.CompletedTask;
            }).WaitAsync(TimeSpan.FromSeconds(5));
        }
        finally
        {
            reader.Release();
        }

        var error = await Assert.ThrowsAsync<MediaServiceException>(() => retry);

        Assert.Contains("不能盲目重试", error.Message);
        Assert.Null(await preparation.ClaimNextAsync(PreparationJobKind.PptImages));
        var listed = await ReadListedSourceAsync(client, source.Id);
        Assert.Equal(string.Empty, listed.GetProperty("preparation_state").GetString());
        Assert.False(listed.GetProperty("metadata").TryGetProperty("slide_images", out _));
    }

    [Fact]
    public async Task ExistingOriginalDigestSkipsTheExternalFileRead()
    {
        using var factory = new ControlHostApplicationFactory();
        var source = await AddLegacyPptAsync(factory);
        var reader = new ControlledOriginalDigestReader();
        var preparation = CreatePreparationWithReader(factory, reader);
        var retry = preparation.RetryPptImagesAsync(source.Id);
        try
        {
            Assert.Equal("queued", (await retry.WaitAsync(TimeSpan.FromSeconds(5))).PreparationState);
            Assert.False(reader.Started.Task.IsCompleted);
        }
        finally
        {
            reader.Release();
            await retry;
        }
    }

    [Fact]
    public async Task CancelledLegacyDigestReadDoesNotEnterTheWriteTransactionOrQueueAJob()
    {
        using var factory = new ControlHostApplicationFactory();
        using var client = factory.CreateHttpsClient();
        await AuthenticateAsync(client);
        var source = await AddLegacyPptAsync(factory);
        await ClearLegacyDigestAsync(factory, source.Id);
        var reader = new ControlledOriginalDigestReader();
        var preparation = CreatePreparationWithReader(factory, reader);
        using var cancellation = new CancellationTokenSource();
        var retry = preparation.RetryPptImagesAsync(source.Id, cancellation.Token);
        await reader.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));

        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => retry);
        Assert.Null(await preparation.ClaimNextAsync(PreparationJobKind.PptImages));
        var listed = await ReadListedSourceAsync(client, source.Id);
        Assert.Equal(string.Empty, listed.GetProperty("preparation_state").GetString());
        Assert.False(listed.GetProperty("metadata").TryGetProperty("slide_images", out _));
    }

    /// <summary>
    /// 保留真实临时库和同一个写协调器，仅替换外部文件读取边界。
    /// :param factory: 临时主机。
    /// :param reader: 受控原件读取器。
    /// :returns: 可从公开接口执行的准备服务。
    /// </summary>
    private static MediaPreparationService CreatePreparationWithReader(
        ControlHostApplicationFactory factory, IOriginalMediaDigestReader reader) => new(
        factory.Services.GetRequiredService<IDbContextFactory<ControlDbContext>>(),
        factory.Services.GetRequiredService<WriteCoordinator>(),
        new DataRootOptions { RootPath = factory.TemporaryRoot },
        originalDigestReader: reader);

    /// <summary>在实际读取后暂停返回摘要，用外部 I/O 时序控制事务竞争。</summary>
    private sealed class ControlledOriginalDigestReader : IOriginalMediaDigestReader
    {
        private readonly TaskCompletionSource _released = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>
        /// 读取真实原件后等待测试允许返回。
        /// :param path: 原件路径。
        /// :param cancellationToken: 取消令牌。
        /// :returns: 真实原件摘要。
        /// </summary>
        public async Task<string> ReadSha256Async(string path, CancellationToken cancellationToken = default)
        {
            var bytes = await File.ReadAllBytesAsync(path, cancellationToken);
            var digest = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
            Started.TrySetResult();
            await _released.Task.WaitAsync(cancellationToken);
            return digest;
        }

        /// <summary>允许外部读取返回，让测试中的重试完成或拒绝。</summary>
        public void Release() => _released.TrySetResult();
    }

    /// <summary>
    /// 在独立测试库模拟升级前没有原件摘要/版本的 PPT。
    /// :param factory: 临时主机。
    /// :param sourceId: 既有原件源标识。
    /// :returns: 测试旧状态写入完成。
    /// </summary>
    private static Task ClearLegacyDigestAsync(ControlHostApplicationFactory factory, long sourceId) =>
        factory.Services.GetRequiredService<WriteCoordinator>().ExecuteAsync(async (database, token) =>
        {
            var stored = await database.MediaSources.SingleAsync(item => item.Id == sourceId, token);
            stored.ContentDigest = string.Empty;
            stored.SourceRevision = 0;
        });
}
