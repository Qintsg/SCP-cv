// 已部署启动入口合同与缺配置失败测试；不启动计划任务或访问现场设备。
using System.Diagnostics;

namespace ScpCv.ControlHost.Tests;

public sealed class DeployedStartupScriptTests
{
    private static readonly string ScriptPath = Path.Combine(AppContext.BaseDirectory, "scripts", "start-deployed.ps1");

    [Fact]
    public void ScriptHasWindowsPowerShellUtf8Bom()
    {
        Assert.Equal(new byte[] { 0xef, 0xbb, 0xbf }, File.ReadAllBytes(ScriptPath)[..3]);
    }

    [Fact]
    public void TaskStartIsInsideNonBlockingMutexAndDoesNotRestartRunningTask()
    {
        var script = File.ReadAllText(ScriptPath);
        Assert.True(script.IndexOf("WaitOne(0)", StringComparison.Ordinal) < script.IndexOf("Start-ScheduledTask", StringComparison.Ordinal));
        Assert.Contains("if ($task.State -ne 'Running')", script, StringComparison.Ordinal);
        Assert.Contains("$startupMutex.ReleaseMutex()", script, StringComparison.Ordinal);
    }

    [Fact]
    public void ApiResumeRequiresOldRunningTaskAndNoNativeMembersOrOnlinePlayers()
    {
        var script = File.ReadAllText(ScriptPath);
        Assert.Contains("$startupWasRunning[$startupRuntimeTask.name]", script, StringComparison.Ordinal);
        Assert.Contains(".TotalSeconds -gt 120", script, StringComparison.Ordinal);
        Assert.Contains("$startupCanResume -and $startupMembers.Count -eq 0", script, StringComparison.Ordinal);
        Assert.Contains("Where-Object player_online).Count -eq 0", script, StringComparison.Ordinal);
        Assert.DoesNotContain("Stop-Process", script, StringComparison.Ordinal);
        Assert.DoesNotContain("Stop-ScheduledTask", script, StringComparison.Ordinal);
    }

    [Fact]
    public void ConfigurationAndTaskIdentityAreCheckedBeforeMutations()
    {
        var script = File.ReadAllText(ScriptPath);
        Assert.True(script.IndexOf("$definition.action_sha256", StringComparison.Ordinal) < script.IndexOf("Start-ScheduledTask", StringComparison.Ordinal));
        Assert.Contains("$definition.script_sha256", script, StringComparison.Ordinal);
        Assert.Contains("$startupUri.IsLoopback", script, StringComparison.Ordinal);
        Assert.Contains("$startupConfig.computer_name", script, StringComparison.Ordinal);
    }

    [Fact]
    public void NoPolicyOrCertificateBypassAndNoPhysicalCommands()
    {
        var script = File.ReadAllText(ScriptPath);
        foreach (var forbidden in new[] { "-ExecutionPolicy Bypass", "Set-ExecutionPolicy", "SkipCertificateCheck", "ValidationCallback", "/api/video-wall", "/api/venue", "/api/devices", "/api/volume" })
            Assert.DoesNotContain(forbidden, script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("/api/auth/logout/", script, StringComparison.Ordinal);
    }

    [Fact]
    public async Task MissingConfigurationReturnsFailureWithoutStartingTasks()
    {
        if (!OperatingSystem.IsWindows()) return;
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo("powershell.exe")
            {
                UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true,
            },
        };
        foreach (var argument in new[] { "-NoProfile", "-NonInteractive", "-File", ScriptPath, "-ConfigurationPath", Path.Combine(Path.GetTempPath(), $"missing-scp-cv-{Guid.NewGuid():N}.json"), "-NoPause" })
            process.StartInfo.ArgumentList.Add(argument);
        Assert.True(process.Start());
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        using var budget = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        await process.WaitForExitAsync(budget.Token);
        Assert.Equal(1, process.ExitCode);
        Assert.DoesNotContain("已就绪", await output, StringComparison.Ordinal);
        Assert.DoesNotContain("Start-ScheduledTask", await error, StringComparison.Ordinal);
    }
}
