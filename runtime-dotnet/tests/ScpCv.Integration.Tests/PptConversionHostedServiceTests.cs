// 后台 PPT 转换的非 Physical 回归：通过公开媒体接口核对页图、失败、重试与原件。
using System.Collections.Concurrent;
using Microsoft.Extensions.Logging.Abstractions;
using ScpCv.Contracts.Http;
using ScpCv.ControlHost.Media;
using ScpCv.Domain.Model;
using ScpCv.Infrastructure.Configuration;
using ScpCv.Infrastructure.Media;
using ScpCv.Integration.Tests.Fixtures;

namespace ScpCv.Integration.Tests;

public sealed class PptConversionHostedServiceTests
{
    /// <summary>完整页图发布前不可播放，发布后各页仍可经公开接口检索。</summary>
    [Fact]
    public async Task QueuedPptBecomesAvailableOnlyAfterConverterPublishesAllPages()
    {
        await using var context = await ConversionContext.CreateAsync();
        Assert.False(context.Source.IsAvailable);
        Assert.Equal("queued", context.Source.PreparationState);
        await Assert.ThrowsAsync<MediaServiceException>(() => context.Media.GetPptSlideImageAsync(context.Source.Id, 1));

        await context.StartAsync(SuccessfulConverter());
        var ready = await context.WaitForStateAsync("ready");

        Assert.True(ready.IsAvailable);
        Assert.Equal(2, ready.PageCount);
        var pages = await context.Media.GetPptResourcesAsync(context.Source.Id);
        Assert.Equal([1, 2], pages.Select(page => page.PageIndex));
        for (var page = 1; page <= ready.PageCount; page++)
        {
            var image = await context.Media.GetPptSlideImageAsync(context.Source.Id, page);
            Assert.Equal(PageBytes(page), await File.ReadAllBytesAsync(image.Path));
        }
        Assert.Equal(ConversionContext.OriginalBytes, await File.ReadAllBytesAsync(context.Source.Uri));
    }

    /// <summary>转换失败与异常都保留原件及原因，显式重试使用新的作业身份。</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ConversionFailureDoesNotPublishPartialPagesAndExplicitRetryCanRecover(bool throwException)
    {
        await using var context = await ConversionContext.CreateAsync();
        const string failure = "受控转换器在第二页失败";
        var firstRequest = new TaskCompletionSource<ConversionRequest>(TaskCreationOptions.RunContinuationsAsynchronously);
        var converter = new ControlledConverter(async (request, token) =>
        {
            firstRequest.TrySetResult(request);
            await WritePageAsync(request.OutputDirectory, 1, token);
            if (throwException) throw new IOException(failure);
            return new SlideConversionResult(OperationStatus.Failed, 0, "com_export_failed", failure);
        });
        var hosted = await context.StartAsync(converter);
        var failed = await context.WaitForStateAsync("failed");

        await context.AssertNotPlayableAsync(failed);
        Assert.Equal(failure, failed.Metadata["slide_images"].GetProperty("error").GetString());
        Assert.Equal(ConversionContext.OriginalBytes, await File.ReadAllBytesAsync(failed.Uri));
        await hosted.StopAsync(CancellationToken.None);
        var first = await firstRequest.Task.WaitAsync(TimeSpan.FromSeconds(8));
        Assert.True(File.Exists(Path.Combine(first.OutputDirectory, "page-0001.png")));

        var retried = await context.Preparation.RetryPptImagesAsync(context.Source.Id);
        Assert.Equal("queued", retried.PreparationState);
        var retryRequest = new TaskCompletionSource<ConversionRequest>(TaskCreationOptions.RunContinuationsAsynchronously);
        await context.StartAsync(SuccessfulConverter(retryRequest));
        var ready = await context.WaitForStateAsync("ready");
        var retry = await retryRequest.Task.WaitAsync(TimeSpan.FromSeconds(8));

        Assert.NotEqual(first.JobId, retry.JobId);
        Assert.Equal(first.SourceRevision, retry.SourceRevision);
        Assert.True(ready.IsAvailable);
        Assert.Equal(2, ready.PageCount);
        Assert.Equal(ConversionContext.OriginalBytes, await File.ReadAllBytesAsync(ready.Uri));
    }

    /// <summary>转换器声称成功但页图缺失、页序断开或 PNG 无效时仍应失败。</summary>
    [Theory]
    [InlineData("missing", "数量")]
    [InlineData("gap", "不连续")]
    [InlineData("invalid", "有效 PNG")]
    public async Task SuccessfulResultWithInvalidOutputCannotMakePresentationPlayable(string outputKind, string error)
    {
        await using var context = await ConversionContext.CreateAsync();
        await context.StartAsync(new ControlledConverter(async (request, token) =>
        {
            await WritePageAsync(request.OutputDirectory, 1, token);
            if (outputKind == "gap") await WritePageAsync(request.OutputDirectory, 3, token);
            if (outputKind == "invalid")
                await File.WriteAllTextAsync(Path.Combine(request.OutputDirectory, "page-0002.png"), "不是 PNG", token);
            return new SlideConversionResult(OperationStatus.Succeeded, 2, "ok", string.Empty);
        }));
        var failed = await context.WaitForStateAsync("failed");

        await context.AssertNotPlayableAsync(failed);
        Assert.Contains(error, failed.Metadata["slide_images"].GetProperty("error").GetString(), StringComparison.Ordinal);
        Assert.Equal(ConversionContext.OriginalBytes, await File.ReadAllBytesAsync(failed.Uri));
    }

    /// <summary>停止中途转换后保留部分输出，状态不确定且重启不能重放 Office 副作用。</summary>
    [Fact]
    public async Task StopDuringConversionRetainsEvidenceAndRestartDoesNotReplayUncertainJob()
    {
        await using var context = await ConversionContext.CreateAsync();
        var started = new TaskCompletionSource<ConversionRequest>(TaskCreationOptions.RunContinuationsAsynchronously);
        var hosted = await context.StartAsync(new ControlledConverter(async (request, token) =>
        {
            await WritePageAsync(request.OutputDirectory, 1, token);
            started.TrySetResult(request);
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
            return new SlideConversionResult(OperationStatus.Succeeded, 1, "ok", string.Empty);
        }));
        var interrupted = await started.Task.WaitAsync(TimeSpan.FromSeconds(8));

        await hosted.StopAsync(CancellationToken.None);
        var uncertain = await context.WaitForStateAsync("uncertain");

        await context.AssertNotPlayableAsync(uncertain);
        Assert.Contains("ControlHost 停止", uncertain.Metadata["slide_images"].GetProperty("error").GetString(), StringComparison.Ordinal);
        Assert.Equal(PageBytes(1), await File.ReadAllBytesAsync(Path.Combine(interrupted.OutputDirectory, "page-0001.png")));
        await Assert.ThrowsAsync<MediaServiceException>(() => context.Preparation.RetryPptImagesAsync(context.Source.Id));
        await context.AssertRestartDoesNotConvertAsync();
        Assert.Equal(ConversionContext.OriginalBytes, await File.ReadAllBytesAsync(uncertain.Uri));
    }

    /// <summary>上次退出遗留的运行中作业先转为不确定，不会因后台重启重新调用转换器。</summary>
    [Fact]
    public async Task InterruptedRunningJobIsReconciledBeforeAnyExternalConversion()
    {
        await using var context = await ConversionContext.CreateAsync();
        var interrupted = await context.Preparation.ClaimNextAsync(PreparationJobKind.PptImages);
        Assert.NotNull(interrupted);

        await context.AssertRestartDoesNotConvertAsync();
        var uncertain = await context.WaitForStateAsync("uncertain");

        await context.AssertNotPlayableAsync(uncertain);
        Assert.Contains("上次退出", uncertain.Metadata["slide_images"].GetProperty("error").GetString(), StringComparison.Ordinal);
        await Assert.ThrowsAsync<MediaServiceException>(() => context.Preparation.RetryPptImagesAsync(context.Source.Id));
    }

    /// <summary>旧暂存输出不能被同一作业的转换覆盖，必须保留供核查。</summary>
    [Fact]
    public async Task ExistingStagingOutputIsPreservedWithoutReplayingConversion()
    {
        await using var context = await ConversionContext.CreateAsync();
        var previous = await context.Preparation.ClaimNextAsync(PreparationJobKind.PptImages);
        Assert.NotNull(previous);
        await context.Preparation.RequeueSlideImagesAsync(previous.JobId, "受控模拟恢复排队");
        var staging = Path.Combine(context.Root, "cache", "staging", previous.JobId.ToString("N"));
        Directory.CreateDirectory(staging);
        await WritePageAsync(staging, 1, CancellationToken.None);

        await context.AssertRestartDoesNotConvertAsync();
        var uncertain = await context.WaitForStateAsync("uncertain");

        await context.AssertNotPlayableAsync(uncertain);
        Assert.Contains("暂存目录", uncertain.Metadata["slide_images"].GetProperty("error").GetString(), StringComparison.Ordinal);
        Assert.Equal(PageBytes(1), await File.ReadAllBytesAsync(Path.Combine(staging, "page-0001.png")));
    }

    /// <summary>准备后的原件跨中文目录移动，下载和页图地址、内容保持一致。</summary>
    [Fact]
    public async Task PreparedOriginalAndContainingFolderCanMoveWithoutChangingPublishedPages()
    {
        await using var context = await ConversionContext.CreateAsync();
        var hosted = await context.StartAsync(SuccessfulConverter());
        var ready = await context.WaitForStateAsync("ready");
        await hosted.StopAsync(CancellationToken.None);
        var firstPage = await context.Media.GetPptSlideImageAsync(context.Source.Id, 1);
        var resources = await context.Media.GetPptResourcesAsync(context.Source.Id);
        var parent = await context.Media.CreateFolderAsync("中文一级", null);
        var child = await context.Media.CreateFolderAsync("中文二级", parent.Id);

        var moved = await context.Media.MoveSourceAsync(context.Source.Id, child.Id);
        Assert.False(File.Exists(ready.Uri));
        Assert.Equal(Path.Combine(context.Root, "media", "中文一级", "中文二级", "受控文稿.pptx"), moved.Uri);
        await context.Media.UpdateFolderAsync(parent.Id, "移动后一级", null, updateParent: false);
        var current = Assert.Single(await context.Media.ListSourcesAsync(null, null), item => item.Id == context.Source.Id);
        var download = await context.Media.GetDownloadAsync(context.Source.Id);
        var currentPage = await context.Media.GetPptSlideImageAsync(context.Source.Id, 1);

        Assert.False(File.Exists(moved.Uri));
        Assert.Equal(Path.Combine(context.Root, "media", "移动后一级", "中文二级", "受控文稿.pptx"), current.Uri);
        Assert.Equal(ConversionContext.OriginalBytes, await File.ReadAllBytesAsync(download.Path));
        Assert.Equal("ready", current.PreparationState);
        Assert.True(current.IsAvailable);
        Assert.Equal(ready.Metadata["slide_images"].GetProperty("source_digest").GetString(),
            current.Metadata["slide_images"].GetProperty("source_digest").GetString());
        Assert.Equal(firstPage.Path, currentPage.Path);
        Assert.Equal(PageBytes(1), await File.ReadAllBytesAsync(currentPage.Path));
        var currentResources = await context.Media.GetPptResourcesAsync(context.Source.Id);
        Assert.Equal(resources.Select(page => (page.Id, page.PageIndex, page.SlideImage, page.NextSlideImage)),
            currentResources.Select(page => (page.Id, page.PageIndex, page.SlideImage, page.NextSlideImage)));
        Assert.Equal(2, current.PageCount);
    }

    /// <summary>转换中拒绝移动源或包含它的目录，同时原件路径与字节不变。</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RunningConversionRejectsOriginalOrContainingFolderMove(bool moveFolder)
    {
        await using var context = await ConversionContext.CreateAsync();
        var folder = await context.Media.CreateFolderAsync("转换中目录", null);
        var original = await context.Media.MoveSourceAsync(context.Source.Id, folder.Id);
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var hosted = await context.StartAsync(new ControlledConverter(async (_, token) =>
        {
            started.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
            return new SlideConversionResult(OperationStatus.Succeeded, 1, "ok", string.Empty);
        }));
        await started.Task.WaitAsync(TimeSpan.FromSeconds(8));

        var error = moveFolder
            ? await Assert.ThrowsAsync<MediaServiceException>(() => context.Media.UpdateFolderAsync(folder.Id, "不应移动", null, false))
            : await Assert.ThrowsAsync<MediaServiceException>(() => context.Media.MoveSourceAsync(context.Source.Id, null));

        Assert.Contains("转换", error.Message, StringComparison.Ordinal);
        var current = Assert.Single(await context.Media.ListSourcesAsync(null, null), item => item.Id == context.Source.Id);
        Assert.Equal(original.Uri, current.Uri);
        Assert.Equal(original.FolderId, current.FolderId);
        Assert.Equal(ConversionContext.OriginalBytes, await File.ReadAllBytesAsync(current.Uri));
        var currentFolder = Assert.Single(await context.Media.ListFoldersAsync(), item => item.Id == folder.Id);
        Assert.Equal("转换中目录", currentFolder.Name);
        await hosted.StopAsync(CancellationToken.None);
    }

    /// <summary>暂时不可执行的 Office 返回值应保留原作业，下一轮条件恢复后可完成。</summary>
    [Theory]
    [InlineData("office_unavailable")]
    [InlineData("group_not_armed")]
    [InlineData("office_slot_busy")]
    [InlineData("office_conversion_busy")]
    public async Task TransientOfficeRefusalRequeuesSameJobAndCanRecover(string code)
    {
        await using var context = await ConversionContext.CreateAsync();
        var requests = new ConcurrentQueue<ConversionRequest>();
        await context.StartAsync(new ControlledConverter(async (request, token) =>
        {
            requests.Enqueue(request);
            if (requests.Count == 1) return new SlideConversionResult(OperationStatus.Failed, 0, code, "暂时不可执行");
            await WritePageAsync(request.OutputDirectory, 1, token);
            await WritePageAsync(request.OutputDirectory, 2, token);
            return new SlideConversionResult(OperationStatus.Succeeded, 2, "ok", string.Empty);
        }));

        var ready = await context.WaitForStateAsync("ready");
        var attempts = requests.ToArray();

        Assert.Equal(2, attempts.Length);
        Assert.Equal(attempts[0].JobId, attempts[1].JobId);
        Assert.Equal(attempts[0].OutputDirectory, attempts[1].OutputDirectory);
        Assert.True(ready.IsAvailable);
        Assert.Equal(2, ready.PageCount);
    }

    /// <summary>原件在外部暂失时明确失败，不应把缺失路径交给 Office。</summary>
    [Fact]
    public async Task MissingOriginalFailsBeforeInvokingExternalConverter()
    {
        await using var context = await ConversionContext.CreateAsync();
        var relocated = Path.Combine(context.Root, "外部暂移原件.pptx");
        File.Move(context.Source.Uri, relocated);
        var invoked = false;
        await context.StartAsync(new ControlledConverter((_, _) =>
        {
            invoked = true;
            return Task.FromResult(new SlideConversionResult(OperationStatus.Succeeded, 2, "ok", string.Empty));
        }));

        var failed = await context.WaitForStateAsync("failed");

        await context.AssertNotPlayableAsync(failed);
        Assert.False(invoked);
        Assert.Contains("原件文件不存在", failed.Metadata["slide_images"].GetProperty("error").GetString(), StringComparison.Ordinal);
        Assert.Equal(ConversionContext.OriginalBytes, await File.ReadAllBytesAsync(relocated));
    }

    /// <summary>排队阶段移动原件后领取当前路径，转换和下载仍指向同一份内容。</summary>
    [Fact]
    public async Task QueuedOriginalCanMoveBeforeConversionAndUsesCurrentPath()
    {
        await using var context = await ConversionContext.CreateAsync();
        var target = await context.Media.CreateFolderAsync("排队时移动", null);
        var moved = await context.Media.MoveSourceAsync(context.Source.Id, target.Id);
        var started = new TaskCompletionSource<ConversionRequest>(TaskCreationOptions.RunContinuationsAsynchronously);

        await context.StartAsync(SuccessfulConverter(started));
        var ready = await context.WaitForStateAsync("ready");
        var request = await started.Task.WaitAsync(TimeSpan.FromSeconds(8));

        Assert.Equal(moved.Uri, request.SourcePath);
        Assert.Equal(moved.Uri, ready.Uri);
        Assert.False(File.Exists(context.Source.Uri));
        Assert.Equal(ConversionContext.OriginalBytes, await File.ReadAllBytesAsync((await context.Media.GetDownloadAsync(ready.Id)).Path));
        Assert.Equal(PageBytes(2), await File.ReadAllBytesAsync((await context.Media.GetPptSlideImageAsync(ready.Id, 2)).Path));
    }

    /// <summary>外部转换结果不确定时保持诊断，禁止显式重试与后台自动重放。</summary>
    [Fact]
    public async Task UncertainConverterResultIsNotPlayableAndCannotBeRetried()
    {
        await using var context = await ConversionContext.CreateAsync();
        await context.StartAsync(new ControlledConverter((_, _) => Task.FromResult(
            new SlideConversionResult(OperationStatus.Uncertain, 0, "office_timeout", "外部 Office 结果未知"))));

        var uncertain = await context.WaitForStateAsync("uncertain");

        await context.AssertNotPlayableAsync(uncertain);
        Assert.Equal("外部 Office 结果未知", uncertain.Metadata["slide_images"].GetProperty("error").GetString());
        await Assert.ThrowsAsync<MediaServiceException>(() => context.Preparation.RetryPptImagesAsync(context.Source.Id));
    }

    /// <summary>替代转换器只负责外部 Office seam，真实发布和文件接口仍由产品代码执行。</summary>
    private sealed class ControlledConverter(
        Func<ConversionRequest, CancellationToken, Task<SlideConversionResult>> convert,
        Func<bool>? available = null) : IPptSlideConverter
    {
        public bool IsAvailable => available?.Invoke() ?? true;

        /// <summary>把 Office 请求交给当前用例定义的受控外部行为。</summary>
        public Task<SlideConversionResult> ConvertAsync(
            Guid jobId,
            long sourceRevision,
            string sourcePath,
            string outputDirectory,
            CancellationToken cancellationToken = default) =>
            convert(new ConversionRequest(jobId, sourceRevision, sourcePath, outputDirectory), cancellationToken);
    }

    private sealed record ConversionRequest(Guid JobId, long SourceRevision, string SourcePath, string OutputDirectory);

    /// <summary>生成可区分页序的受控 PNG 输出，不把它视为 Office 实际导出证据。</summary>
    private static byte[] PageBytes(int page) => [137, 80, 78, 71, 13, 10, 26, 10, (byte)page, 2, 3, 4];

    /// <summary>按产品要求的文件名写入一页替代转换输出。</summary>
    private static Task WritePageAsync(string output, int page, CancellationToken token) =>
        File.WriteAllBytesAsync(Path.Combine(output, $"page-{page:0000}.png"), PageBytes(page), token);

    /// <summary>建立完整两页输出的外部转换器，同时可记录作业身份。</summary>
    private static ControlledConverter SuccessfulConverter(TaskCompletionSource<ConversionRequest>? started = null) =>
        new(async (request, token) =>
        {
            started?.TrySetResult(request);
            await WritePageAsync(request.OutputDirectory, 1, token);
            await WritePageAsync(request.OutputDirectory, 2, token);
            return new SlideConversionResult(OperationStatus.Succeeded, 2, "ok", string.Empty);
        });

    /// <summary>隔离数据库和原件目录，保证后台作业退出后再回收临时库。</summary>
    private sealed class ConversionContext : IAsyncDisposable
    {
        private readonly ControlHostFixture _fixture;
        private readonly List<PptConversionHostedService> _hosts = [];

        private ConversionContext(ControlHostFixture fixture, MediaSourceService media, MediaSourceDto source)
        {
            _fixture = fixture;
            Media = media;
            Source = source;
            Preparation = new MediaPreparationService(fixture.Database, fixture.Writes,
                new DataRootOptions { RootPath = fixture.TemporaryRoot });
        }

        public static byte[] OriginalBytes => "受控非 Physical 文稿原件"u8.ToArray();
        public MediaSourceService Media { get; }
        public MediaSourceDto Source { get; }
        public MediaPreparationService Preparation { get; }
        public string Root => _fixture.TemporaryRoot;

        /// <summary>通过上传公开接口创建文稿及准备作业。</summary>
        public static async Task<ConversionContext> CreateAsync()
        {
            var fixture = await ControlHostFixture.CreateAsync();
            try
            {
                var media = new MediaSourceService(fixture.Database, fixture.Writes, fixture.Database, new MediaStorageOptions());
                await using var upload = new MemoryStream(OriginalBytes);
                var source = await media.AddUploadedAsync(upload, "受控文稿.pptx", null, null, null, null, false, false);
                return new ConversionContext(fixture, media, source);
            }
            catch
            {
                await fixture.DisposeAsync();
                throw;
            }
        }

        /// <summary>启动真实后台编排，仅替换外部转换器。</summary>
        public async Task<PptConversionHostedService> StartAsync(IPptSlideConverter converter)
        {
            var hosted = new PptConversionHostedService(Preparation, converter, _fixture.Database,
                new DataRootOptions { RootPath = Root }, NullLogger<PptConversionHostedService>.Instance);
            _hosts.Add(hosted);
            await hosted.StartAsync(CancellationToken.None);
            return hosted;
        }

        /// <summary>等待公开源状态，超时给出最后观测而不查询内部数据库字段。</summary>
        public async Task<MediaSourceDto> WaitForStateAsync(string state)
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(8));
            var lastState = string.Empty;
            try
            {
                while (true)
                {
                    var current = Assert.Single(await Media.ListSourcesAsync(null, null, timeout.Token), source => source.Id == Source.Id);
                    lastState = current.PreparationState;
                    if (lastState == state) return current;
                    await Task.Delay(25, timeout.Token);
                }
            }
            catch (OperationCanceledException) when (timeout.IsCancellationRequested)
            {
                throw new TimeoutException($"文稿未在预算内进入 {state}，最后状态为 {lastState}。");
            }
        }

        /// <summary>失败或不确定状态不能暴露部分页图作为可播放资源。</summary>
        public async Task AssertNotPlayableAsync(MediaSourceDto source)
        {
            Assert.False(source.IsAvailable);
            Assert.Equal(0, source.PageCount);
            Assert.Empty(await Media.GetPptResourcesAsync(source.Id));
            await Assert.ThrowsAsync<MediaServiceException>(() => Media.GetPptSlideImageAsync(source.Id, 1));
        }

        /// <summary>等待后台完成两个领取周期，证明重启没有重放外部转换。</summary>
        public async Task AssertRestartDoesNotConvertAsync()
        {
            var secondCycle = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var availabilityChecks = 0;
            var conversionCalls = 0;
            var converter = new ControlledConverter((_, _) =>
            {
                Interlocked.Increment(ref conversionCalls);
                return Task.FromResult(new SlideConversionResult(OperationStatus.Failed, 0, "unexpected_replay", "不应重放"));
            }, () =>
            {
                if (Interlocked.Increment(ref availabilityChecks) >= 2) secondCycle.TrySetResult();
                return true;
            });
            var restarted = await StartAsync(converter);
            await secondCycle.Task.WaitAsync(TimeSpan.FromSeconds(8));
            await restarted.StopAsync(CancellationToken.None);
            Assert.Equal(0, Volatile.Read(ref conversionCalls));
        }

        /// <summary>先停止全部后台作业，再按既有 fixture 的受控路径回收临时库。</summary>
        public async ValueTask DisposeAsync()
        {
            foreach (var hosted in _hosts)
            {
                await hosted.StopAsync(CancellationToken.None);
                hosted.Dispose();
            }
            await _fixture.DisposeAsync();
        }
    }
}
