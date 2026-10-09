// 真实LibreOffice加载拒绝的公开转换回归；仅自有夹具/新库/独占Office，常规非Physical不执行。
using System.Security.Cryptography;
using ScpCv.ControlHost.Media;
using ScpCv.Domain.Model;
using ScpCv.Infrastructure.Configuration;
using ScpCv.Infrastructure.Media;
using ScpCv.Infrastructure.Persistence;
using ScpCv.Contracts.Presentations;

namespace ScpCv.Integration.Tests;

[Collection("PresentationPackageEnvironment")]
public sealed class LibreOfficeLoadRejectionPhysicalTests
{
    /// <summary>:returns: 已验证加密ODP无密码加载终止应明确Failed，不能使侧车故障/作业uncertain。</summary>
    [Fact]
    [Trait("Category", "Physical")]
    public async Task EncryptedOpenDocumentLoadAbortIsFailedWithNormalEngineExit()
    {
        var original = Environment.GetEnvironmentVariable("SCP_CV_TEST_ENCRYPTED_ODP");
        Assert.True(!string.IsNullOrWhiteSpace(original) && File.Exists(original), "请提供自有已验证的加密ODP夹具SCP_CV_TEST_ENCRYPTED_ODP；本测试不会创建/读取用户密钥。");
        var originalDigest = SHA256.HashData(await File.ReadAllBytesAsync(original));
        var root = Path.Combine(Path.GetTempPath(), "SCP-cv-real-load-rejection", Guid.NewGuid().ToString("N"));
        var options = new DataRootOptions { RootPath = root };
        var factory = new ControlDbContextFactory(options, root);
        await new DatabaseInitializer(factory).InitializeAsync();
        using var writes = new WriteCoordinator(factory);
        var media = new MediaSourceService(factory, writes, factory, new MediaStorageOptions());
        var preparation = new MediaPreparationService(factory, writes, options);
        using var upload = File.OpenRead(original);
        var source = await media.AddUploadedAsync(upload, "encrypted.odp", null, "自有加密拒绝夹具", "ppt", null, false, false);
        var job = await preparation.ClaimNextAsync(PreparationJobKind.PptImages);
        Assert.NotNull(job);
        var stage = Path.Combine(root, "cache", "staging", job.JobId.ToString("N"));
        using var budget = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        var result = await new LibreOfficeSlideConverter(factory).ConvertAsync(job.JobId, job.SourceRevision, source.Uri, stage, budget.Token);
        Assert.Equal(OperationStatus.Failed, result.Status);
        Assert.Equal("presentation_load_rejected", result.Code);
        Assert.Equal(0, result.PageCount);
        Assert.Empty(Directory.GetFiles(stage));
        await preparation.FailSlideImagesAsync(job.JobId, result.Detail);
        var observed = Assert.Single(await media.ListSourcesAsync(null, null));
        Assert.Equal("failed", observed.PreparationState);
        Assert.Equal(originalDigest, SHA256.HashData(await File.ReadAllBytesAsync(source.Uri)));
        Assert.Equal(originalDigest, SHA256.HashData(await File.ReadAllBytesAsync(original)));
        using var exclusive = File.Open(source.Uri, FileMode.Open, FileAccess.Read, FileShare.None);
    }

    /// <summary>:returns: 先前输出存在时即使是同一加密输入，也不能以加载拒绝规则记明确失败或擦除证据。</summary>
    [Fact]
    [Trait("Category", "Physical")]
    public async Task ExistingOutputIsNotDowngradedToKnownLoadRejection()
    {
        var original = Environment.GetEnvironmentVariable("SCP_CV_TEST_ENCRYPTED_ODP");
        Assert.True(!string.IsNullOrWhiteSpace(original) && File.Exists(original), "缺少自有已验证的加密ODP夹具配置。");
        var root = Path.Combine(Path.GetTempPath(), "SCP-cv-real-load-output", Guid.NewGuid().ToString("N"));
        var options = new DataRootOptions { RootPath = root };
        var factory = new ControlDbContextFactory(options, root);
        await new DatabaseInitializer(factory).InitializeAsync();
        using var writes = new WriteCoordinator(factory);
        var media = new MediaSourceService(factory, writes, factory, new MediaStorageOptions());
        var preparation = new MediaPreparationService(factory, writes, options);
        using var upload = File.OpenRead(original);
        var source = await media.AddUploadedAsync(upload, "encrypted.odp", null, "已有输出不能重放", "ppt", null, false, false);
        var job = await preparation.ClaimNextAsync(PreparationJobKind.PptImages);
        Assert.NotNull(job);
        var stage = Path.Combine(root, "cache", "staging", job.JobId.ToString("N"));
        Directory.CreateDirectory(stage);
        var evidence = Path.Combine(stage, "prior-output.txt");
        await File.WriteAllTextAsync(evidence, "已有证据，不能删除或当空stage");
        using var budget = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        await Assert.ThrowsAsync<PresentationTransportException>(() => new LibreOfficeSlideConverter(factory)
            .ConvertAsync(job.JobId, job.SourceRevision, source.Uri, stage, budget.Token));
        Assert.Equal("已有证据，不能删除或当空stage", await File.ReadAllTextAsync(evidence));
        Assert.Single(Directory.GetFiles(stage));
        using var exclusive = File.Open(source.Uri, FileMode.Open, FileAccess.Read, FileShare.None);
    }
}
