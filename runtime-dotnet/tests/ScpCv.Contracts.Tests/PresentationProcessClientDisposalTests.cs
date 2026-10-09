// 使用仅接收不可识别参数的自有PowerShell子进程证明公共关闭结果；不执行脚本或启动Office。
using ScpCv.Contracts.Presentations;
using ScpCv.Presentation.Protocol;

namespace ScpCv.Contracts.Tests;

public sealed class PresentationProcessClientDisposalTests
{
    /// <summary>:returns: 非零退出的真实失败在并发和重复Dispose后仍保留，不变成正常退出证明。</summary>
    [Fact]
    public async Task FailedOwnedProcessExitRemainsFailedForConcurrentAndRepeatedDisposal()
    {
        var executable = Path.Combine(Environment.SystemDirectory, "WindowsPowerShell", "v1.0", "powershell.exe");
        var profile = Path.Combine(Path.GetTempPath(), "scp-cv-disposal-" + Guid.NewGuid().ToString("N"));
        var identity = new PresentationScope
        {
            Role = "converter", EngineInstanceId = Guid.NewGuid(), GroupEpoch = 1, OwnerEpoch = 1, EngineEpoch = 1,
        };
        if (!OperatingSystem.IsWindows())
        {
            Assert.Throws<PlatformNotSupportedException>(() => new PresentationProcessClient(executable, Path.GetTempPath(), profile, identity));
            return;
        }

        Assert.True(File.Exists(executable), "公共进程回归需要系统Windows PowerShell，不会安装或替换系统程序。");
        var client = new PresentationProcessClient(executable, Path.GetTempPath(), profile, identity);
        try
        {
            // 客户端只传自己的--parent-gate等固定参数，没有-Command、-File或用户脚本。
            var firstClose = client.DisposeAsync().AsTask();
            var concurrentClose = client.DisposeAsync().AsTask();
            var first = await Assert.ThrowsAsync<PresentationTransportException>(() => firstClose);
            var concurrent = await Assert.ThrowsAsync<PresentationTransportException>(() => concurrentClose);
            var repeated = await Assert.ThrowsAsync<PresentationTransportException>(() => client.DisposeAsync().AsTask());

            Assert.Same(first, concurrent);
            Assert.Same(first, repeated);
        }
        finally
        {
            // 清理只针对本轮客户端；已有失败在再次等待时应保持可见。
            try { await client.DisposeAsync(); }
            catch (PresentationTransportException) { }
        }
    }
}
