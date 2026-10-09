// 公开Player CLOSE→正式LibreOffice adapter→外部stdio关闭诊断；不启动Office，仅自有隐藏HWND。
using System.Windows.Interop;
using System.ComponentModel;
using ScpCv.Contracts.Ipc;
using ScpCv.Contracts.Presentations;
using ScpCv.Contracts.Runtime;
using ScpCv.PlayerWorker;
using ScpCv.PlayerWorker.Playback;

namespace ScpCv.Windows.Tests;

public sealed partial class PlayerRuntimeHostLifecycleTests
{
    /// <summary>:returns: 已打开文稿shutdown拒绝和EOF故障退出同时保持；重复CLOSE和Host退出不能擦除或重放首次失败。</summary>
    [Fact]
    public Task RejectedShutdownAndFailedExitPreserveBothCloseCauses() => RunCloseDiagnosticAsync("shutdown-rejected-exit-error", async host =>
    {
        var first = await Assert.ThrowsAsync<PresentationTransportException>(() => CloseNativeAsync(host));
        var causes = Assert.IsType<AggregateException>(first.InnerException).InnerExceptions;
        Assert.Equal(2, causes.Count);
        var shutdown = Assert.IsType<InvalidOperationException>(causes[0]);
        Assert.Contains("controlled_shutdown_rejected", shutdown.Message, StringComparison.Ordinal);
        Assert.Contains("外部夹具明确拒绝关闭", shutdown.Message, StringComparison.Ordinal);
        var cleanup = Assert.IsType<PresentationTransportException>(causes[1]);
        Assert.Contains("exit_code=2", cleanup.ToString(), StringComparison.Ordinal);
        var repeated = await Assert.ThrowsAsync<PresentationTransportException>(() => CloseNativeAsync(host));
        Assert.Same(first, repeated);
        var exiting = await Assert.ThrowsAsync<PresentationTransportException>(() => host.DisposeAsync().AsTask());
        Assert.Same(first, exiting);
    });

    /// <summary>:returns: 仅shutdown拒绝且后退出正常，保留原类型/原因，不额外包装或伪报成功。</summary>
    [Fact]
    public Task RejectedShutdownWithNormalExitKeepsOriginalCloseFailure() => RunCloseDiagnosticAsync("shutdown-rejected", async host =>
    {
        var first = await Assert.ThrowsAsync<InvalidOperationException>(() => CloseNativeAsync(host));
        Assert.Contains("controlled_shutdown_rejected", first.Message, StringComparison.Ordinal);
        Assert.Contains("外部夹具明确拒绝关闭", first.Message, StringComparison.Ordinal);
        Assert.Same(first, await Assert.ThrowsAsync<InvalidOperationException>(() => CloseNativeAsync(host)));
        Assert.Same(first, await Assert.ThrowsAsync<InvalidOperationException>(() => host.DisposeAsync().AsTask()));
    });

    /// <summary>:returns: shutdown成功但进程exit2仍不确定，只保留实际退出错误，不伪造第二失败。</summary>
    [Fact]
    public Task SuccessfulShutdownWithFailedExitKeepsOriginalTransportFailure() => RunCloseDiagnosticAsync("close-normal-exit-error", async host =>
    {
        var first = await Assert.ThrowsAsync<PresentationTransportException>(() => CloseNativeAsync(host));
        Assert.IsType<IOException>(first.InnerException);
        Assert.Contains("exit_code=2", first.ToString(), StringComparison.Ordinal);
        Assert.Same(first, await Assert.ThrowsAsync<PresentationTransportException>(() => CloseNativeAsync(host)));
        Assert.Same(first, await Assert.ThrowsAsync<PresentationTransportException>(() => host.DisposeAsync().AsTask()));
    });

    /// <summary>:returns: 正常CLOSE仍清实际源/页码/布局，再CLOSE和Host退出保持正常。</summary>
    [Fact]
    public Task SuccessfulShutdownAndExitKeepNormalCloseResult() => RunCloseDiagnosticAsync("close-normal", async host =>
    {
        var result = await CloseNativeAsync(host);
        Assert.Equal("completed", result.Status);
        Assert.Equal(System.Text.Json.JsonValueKind.Null, result.ActualState.GetProperty("source_id").ValueKind);
        Assert.Equal(0, result.ActualState.GetProperty("current_slide").GetInt32());
        Assert.Equal(System.Text.Json.JsonValueKind.Null, result.ActualState.GetProperty("actual_fit_text").ValueKind);
        Assert.Equal("completed", (await CloseNativeAsync(host)).Status);
        await host.DisposeAsync();
    });

    /// <summary>:returns: 自有父窗已销毁时，shutdown/exit/viewport三个失败仍全部保持，不以最后Win32错误降级。</summary>
    [Fact]
    public Task LostOwnedWindowKeepsAllThreeCloseFailures() => RunCloseDiagnosticAsync("shutdown-rejected-exit-error", async host =>
    {
        var first = await Assert.ThrowsAsync<PresentationTransportException>(() => CloseNativeAsync(host));
        var causes = Assert.IsType<AggregateException>(first.InnerException).InnerExceptions;
        Assert.Equal(3, causes.Count);
        Assert.IsType<InvalidOperationException>(causes[0]);
        Assert.IsType<PresentationTransportException>(causes[1]);
        Assert.IsType<Win32Exception>(causes[2]);
        Assert.Same(first, await Assert.ThrowsAsync<PresentationTransportException>(() => host.DisposeAsync().AsTask()));
    }, destroyWindowBeforeClose: true);

    /// <summary>:param mode: 外部关闭/退出故障模式。:param verify: 公开关闭行为。:param destroyWindowBeforeClose: 仅销毁本轮自有父窗的负例。:returns: 实际adapter及stdio生命周期，不使用PresentationFactory接缝。</summary>
    private static async Task RunCloseDiagnosticAsync(string mode, Func<PlayerRuntimeHost, Task> verify, bool destroyWindowBeforeClose = false)
    {
        var originalHost = Environment.GetEnvironmentVariable("SCP_CV_LIBREOFFICE_HOST");
        var originalProgram = Environment.GetEnvironmentVariable("SCP_CV_LIBREOFFICE_PROGRAM");
        try
        {
            Environment.SetEnvironmentVariable("SCP_CV_LIBREOFFICE_HOST", Path.Combine(AppContext.BaseDirectory, "PresentationTestHost", "ScpCv.Presentation.TestHost.exe"));
            Environment.SetEnvironmentVariable("SCP_CV_LIBREOFFICE_PROGRAM", Path.Combine(Path.GetTempPath(), mode));
            await OnHiddenDispatcherAsync(async () =>
            {
                var window = new PlayerWindow { Width = 200, Height = 150 };
                Assert.NotEqual(0, new WindowInteropHelper(window).EnsureHandle());
                var host = new PlayerRuntimeHost(window, 1);
                try
                {
                    var path = CreatePackage();
                    var lease = OpenLease(path);
                    lease.Args["content_digest"] = System.Text.Json.JsonSerializer.SerializeToElement(
                        Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(path))));
                    var opened = await host.ExecuteAsync(lease, CancellationToken.None);
                    Assert.Equal("completed", opened.Status);
                    Assert.Equal(101, opened.ActualState.GetProperty("source_id").GetInt64());
                    Assert.False(window.IsVisible);
                    if (destroyWindowBeforeClose) window.Close();
                    await verify(host);
                }
                finally
                {
                    try { await host.DisposeAsync(); }
                    catch (Exception error) when (error is PresentationTransportException or InvalidOperationException) { }
                    window.Close();
                }
            });
        }
        finally
        {
            Environment.SetEnvironmentVariable("SCP_CV_LIBREOFFICE_HOST", originalHost);
            Environment.SetEnvironmentVariable("SCP_CV_LIBREOFFICE_PROGRAM", originalProgram);
        }
    }

    /// <summary>:param host: 本轮已打开的真实宿主。:returns: 公开CLOSE结果，不直接调用内部adapter/helper。</summary>
    private static Task<WorkerExecutionResult> CloseNativeAsync(PlayerRuntimeHost host) => host.ExecuteAsync(new CommandLeaseDto
    {
        CommandId = Guid.NewGuid(), ClaimToken = Guid.NewGuid(), Command = "CLOSE", TargetSequence = 2,
        GroupEpoch = 1, OwnerEpoch = 1, SourceGeneration = 2,
    }, CancellationToken.None);
}
