// 公开Player→正式LibreOffice adapter→真实外部stdio的候选双失败诊断；仅自有隐藏HWND，不启动Office或显示画面。
using System.Windows.Interop;
using ScpCv.Contracts.Presentations;
using ScpCv.PlayerWorker;
using ScpCv.PlayerWorker.Playback;

namespace ScpCv.Windows.Tests;

[CollectionDefinition("PresentationCreationEnvironment", DisableParallelization = true)]
public sealed class PresentationCreationEnvironmentGroup;

[Collection("PresentationCreationEnvironment")]
public sealed partial class PlayerRuntimeHostLifecycleTests
{
    /// <summary>:returns: 外部明确拒绝OPEN与后EOF退出2两原因必须保留，候选不能发布或生成实时健康采样。</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task RejectedNativeOpenAndFailedExitPreserveBothCreationCauses(bool exitFails)
    {
        var originalHost = Environment.GetEnvironmentVariable("SCP_CV_LIBREOFFICE_HOST");
        var originalProgram = Environment.GetEnvironmentVariable("SCP_CV_LIBREOFFICE_PROGRAM");
        try
        {
            Environment.SetEnvironmentVariable("SCP_CV_LIBREOFFICE_HOST", Path.Combine(AppContext.BaseDirectory, "PresentationTestHost", "ScpCv.Presentation.TestHost.exe"));
            Environment.SetEnvironmentVariable("SCP_CV_LIBREOFFICE_PROGRAM", Path.Combine(Path.GetTempPath(), exitFails ? "open-rejected-exit-error" : "open-rejected"));
            await OnHiddenDispatcherAsync(async () =>
            {
                var window = new PlayerWindow { Width = 200, Height = 150 };
                var handle = new WindowInteropHelper(window).EnsureHandle();
                Assert.NotEqual(0, handle);
                Assert.False(window.IsVisible);
                await using var host = new PlayerRuntimeHost(window, 1);
                try
                {
                    var path = CreatePackage();
                    var lease = OpenLease(path);
                    lease.Args["content_digest"] = System.Text.Json.JsonSerializer.SerializeToElement(
                        Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(path))));
                    if (exitFails)
                    {
                        var failed = await Assert.ThrowsAsync<PresentationTransportException>(() => host.ExecuteAsync(lease, CancellationToken.None));
                        var causes = Assert.IsType<AggregateException>(failed.InnerException).InnerExceptions;
                        Assert.Equal(2, causes.Count);
                        var rejected = Assert.IsType<InvalidOperationException>(causes[0]);
                        Assert.Contains("controlled_open_rejected", rejected.Message, StringComparison.Ordinal);
                        Assert.IsType<PresentationTransportException>(causes[1]);
                    }
                    else
                    {
                        var original = await Assert.ThrowsAsync<InvalidOperationException>(() => host.ExecuteAsync(lease, CancellationToken.None));
                        Assert.Contains("controlled_open_rejected", original.Message, StringComparison.Ordinal);
                        Assert.Contains("外部夹具明确拒绝打开", original.Message, StringComparison.Ordinal);
                    }
                    Assert.Null(await host.SampleProgressAsync(CancellationToken.None));
                    Assert.False(window.IsVisible);
                }
                finally { window.Close(); }
            });
        }
        finally
        {
            Environment.SetEnvironmentVariable("SCP_CV_LIBREOFFICE_HOST", originalHost);
            Environment.SetEnvironmentVariable("SCP_CV_LIBREOFFICE_PROGRAM", originalProgram);
        }
    }
}
