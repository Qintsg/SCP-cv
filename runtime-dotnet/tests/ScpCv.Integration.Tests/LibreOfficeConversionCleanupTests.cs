// 公开Converter/真实stdio进程的双失败诊断回归；外部夹具不引用UNO/Office，不显示窗口或修改实体设备。
using ScpCv.Contracts.Presentations;
using ScpCv.ControlHost.Media;
using ScpCv.Domain.Model;
using ScpCv.Infrastructure.Configuration;
using ScpCv.Infrastructure.Media;
using ScpCv.Infrastructure.Persistence;

namespace ScpCv.Integration.Tests;

[Collection("PresentationPackageEnvironment")]
public sealed class LibreOfficeConversionCleanupTests
{
    /// <summary>:returns: 已匹配转换响应后真实文件缺失与后续宿主exit2同时保持，不能仅剩最后退出错误。</summary>
    [Fact]
    public Task MissingArtifactAndFailedExitPreserveBothConverterCauses() => RunScenarioAsync("missing-artifact-exit-error", async convert =>
    {
        var failure = await Assert.ThrowsAsync<PresentationTransportException>(convert);
        var causes = Assert.IsType<AggregateException>(failure.InnerException).InnerExceptions;
        Assert.Equal(2, causes.Count);
        Assert.IsType<FileNotFoundException>(causes[0]);
        var cleanup = Assert.IsType<PresentationTransportException>(causes[1]);
        Assert.Contains("exit_code=2", cleanup.ToString(), StringComparison.Ordinal);
    });

    /// <summary>:returns: 正常退出时不包装原本的文件错误，也不宣称转换完成。</summary>
    [Fact]
    public Task MissingArtifactWithNormalExitKeepsOriginalException() => RunScenarioAsync("missing-artifact", async convert =>
        await Assert.ThrowsAsync<FileNotFoundException>(convert));

    /// <summary>:returns: 原生返回的Failed响应与退出失败也须同时可诊断，不能只有异常形态才保留。</summary>
    [Fact]
    public Task FailedExportResponseAndFailedExitPreserveBothConverterCauses() => RunScenarioAsync("export-rejected-exit-error", async convert =>
    {
        var failure = await Assert.ThrowsAsync<PresentationTransportException>(convert);
        var causes = Assert.IsType<AggregateException>(failure.InnerException).InnerExceptions;
        Assert.Equal(2, causes.Count);
        Assert.Contains("controlled_export_rejected", causes[0].Message, StringComparison.Ordinal);
        Assert.IsType<PresentationTransportException>(causes[1]);
    });

    /// <summary>:returns: 外部正常Failed响应及正常退出仍返回原失败合同，不变成抛错或成功。</summary>
    [Fact]
    public Task FailedExportResponseWithNormalExitKeepsOriginalResult() => RunScenarioAsync("export-rejected", async convert =>
    {
        var result = await convert();
        Assert.Equal(OperationStatus.Failed, result.Status);
        Assert.Equal("controlled_export_rejected", result.Code);
        Assert.Equal("外部夹具明确拒绝转换", result.Detail);
    });

    /// <summary>:param mode: 外部固定故障模式。:param verify: 公开转换结果断言。:returns: 独立真实库/stdio生命周期，不启动Office或更改生产依赖。</summary>
    private static async Task RunScenarioAsync(string mode, Func<Func<Task<SlideConversionResult>>, Task> verify)
    {
        var root = Path.Combine(Path.GetTempPath(), "SCP-cv-converter-cleanup", Guid.NewGuid().ToString("N"));
        var originalHost = Environment.GetEnvironmentVariable("SCP_CV_LIBREOFFICE_HOST");
        var originalProgram = Environment.GetEnvironmentVariable("SCP_CV_LIBREOFFICE_PROGRAM");
        try
        {
            Environment.SetEnvironmentVariable("SCP_CV_LIBREOFFICE_HOST", Path.Combine(AppContext.BaseDirectory, "PresentationTestHost", "ScpCv.Presentation.TestHost.exe"));
            Environment.SetEnvironmentVariable("SCP_CV_LIBREOFFICE_PROGRAM", Path.Combine(root, mode));
            var options = new DataRootOptions { RootPath = root };
            var factory = new ControlDbContextFactory(options, root);
            await new DatabaseInitializer(factory).InitializeAsync();
            using var writes = new WriteCoordinator(factory);
            var media = new MediaSourceService(factory, writes, factory, new MediaStorageOptions());
            var preparation = new MediaPreparationService(factory, writes, options);
            using var upload = new MemoryStream([0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1]);
            var source = await media.AddUploadedAsync(upload, "sample.ppt", null, "受控外部转换", "ppt", null, false, false);
            var job = await preparation.ClaimNextAsync(PreparationJobKind.PptImages);
            Assert.NotNull(job);
            var stage = Path.Combine(root, "cache", "staging", job.JobId.ToString("N"));
            Directory.CreateDirectory(stage);
            await verify(() =>
                new LibreOfficeSlideConverter(factory).ConvertAsync(job.JobId, job.SourceRevision, source.Uri, stage).WaitAsync(TimeSpan.FromSeconds(25)));
            Assert.Empty(Directory.GetFiles(stage));
        }
        finally
        {
            Environment.SetEnvironmentVariable("SCP_CV_LIBREOFFICE_HOST", originalHost);
            Environment.SetEnvironmentVariable("SCP_CV_LIBREOFFICE_PROGRAM", originalProgram);
        }
    }
}
