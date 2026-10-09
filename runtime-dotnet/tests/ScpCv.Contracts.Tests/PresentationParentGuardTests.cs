// 正式Framework父身份/Timer逻辑的真实进程回归，无UNO、Office、显示窗口或自有job。
using System.Diagnostics;

namespace ScpCv.Contracts.Tests;

public sealed class PresentationParentGuardTests
{
    /// <summary>:returns: 错误父属性均拒绝，真实父死亡由Timer检测，而非stdin EOF或job级联推断。</summary>
    [Fact]
    public async Task FrameworkParentGuardRejectsForgedIdentityAndDetectsActualParentDeath()
    {
        if (!OperatingSystem.IsWindows()) return;
        var root = Path.Combine(Path.GetTempPath(), "scp-cv-parent-guard-" + Guid.NewGuid().ToString("N"));
        var start = new ProcessStartInfo(Path.Combine(AppContext.BaseDirectory, "ParentGuardTestHost", "ScpCv.ParentGuard.TestHost.exe"))
        { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        start.ArgumentList.Add("observer"); start.ArgumentList.Add(root);
        using var owned = Process.Start(start) ?? throw new InvalidOperationException("Framework父监测测试侧车未启动。");
        var output = owned.StandardOutput.ReadToEndAsync();
        var error = owned.StandardError.ReadToEndAsync();
        try
        {
            await owned.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(20));
            Assert.True(owned.ExitCode == 0, await output + await error);
            Assert.Equal("passed=4;timer-parent-loss=1;identity-rejection=3\n", await File.ReadAllTextAsync(Path.Combine(root, "result")));
            Assert.Contains("libreoffice_parent_lost: owned_job_cleanup", await File.ReadAllTextAsync(Path.Combine(root, "parent-loss", "guard.stderr.log")), StringComparison.Ordinal);
        }
        finally
        {
            if (!owned.HasExited) owned.Kill(entireProcessTree: true);
            await owned.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
            await Task.WhenAll(output, error).WaitAsync(TimeSpan.FromSeconds(5));
        }
    }
}
