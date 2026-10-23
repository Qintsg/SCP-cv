// 已部署启动入口与解释器拒绝验证，不启动计划任务。项目：SCP-cv；文件：DeployedStartupScriptTests.cs；作者：Qintsg；日期：2026-10-23。
using System.Diagnostics;
using System.Text.Json;

namespace ScpCv.ControlHost.Tests;

public sealed class DeployedStartupScriptTests
{
    private static readonly string ScriptPath = Path.Combine(AppContext.BaseDirectory, "scripts", "start-deployed.ps1");

    [Fact]
    public void ScriptHasUtf8BomAndLfLineEndings()
    {
        var bytes = File.ReadAllBytes(ScriptPath);
        Assert.Equal(new byte[] { 0xef, 0xbb, 0xbf }, bytes[..3]);
        Assert.DoesNotContain((byte)'\r', bytes);
    }

    [Fact]
    public async Task ScriptParsesInPowerShell7AndRequiresCoreVersion72()
    {
        if (!OperatingSystem.IsWindows()) return;
        var result = await RunShellAsync("pwsh.exe", "-Command",
            "$tokens=$null;$errors=$null;$ast=[Management.Automation.Language.Parser]::ParseFile('" +
            ScriptPath.Replace("'", "''", StringComparison.Ordinal) + "',[ref]$tokens,[ref]$errors);" +
            "@{version=[string]$ast.ScriptRequirements.RequiredPSVersion;editions=@($ast.ScriptRequirements.RequiredPSEditions);" +
            "errors=@($errors|ForEach-Object Message);shell_version=$PSVersionTable.PSVersion.ToString();shell_edition=$PSVersionTable.PSEdition}|ConvertTo-Json -Depth 3");
        Assert.True(result.ExitCode == 0, result.Error);
        using var document = JsonDocument.Parse(result.Output);
        var root = document.RootElement;
        Assert.Equal("7.2", root.GetProperty("version").GetString());
        Assert.Equal("Core", root.GetProperty("editions")[0].GetString());
        Assert.Equal(0, root.GetProperty("errors").GetArrayLength());
        Assert.True(Version.Parse(root.GetProperty("shell_version").GetString()!) >= new Version(7, 2));
        Assert.Equal("Core", root.GetProperty("shell_edition").GetString());
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
        var missingPath = Path.Combine(Path.GetTempPath(), $"missing-scp-cv-{Guid.NewGuid():N}.json");
        var result = await RunShellAsync("pwsh.exe", "-File", ScriptPath, "-ConfigurationPath", missingPath, "-NoPause");
        Assert.Equal(1, result.ExitCode);
        Assert.Contains("启动失败：", result.Output, StringComparison.Ordinal);
        Assert.Contains(Path.GetFileName(missingPath), result.Output, StringComparison.Ordinal);
        Assert.DoesNotContain("ScriptRequires", result.Output + result.Error, StringComparison.Ordinal);
        Assert.DoesNotContain("已就绪", result.Output, StringComparison.Ordinal);
        Assert.DoesNotContain("Start-ScheduledTask", result.Error, StringComparison.Ordinal);
        Assert.False(File.Exists(missingPath));
    }

    [Fact]
    public async Task WindowsPowerShell51IsRejectedBeforeReadingConfiguration()
    {
        if (!OperatingSystem.IsWindows()) return;
        var missingPath = Path.Combine(Path.GetTempPath(), $"missing-scp-cv-{Guid.NewGuid():N}.json");
        var legacyShell = Path.Combine(Environment.SystemDirectory, "WindowsPowerShell", "v1.0", "powershell.exe");
        var result = await RunShellAsync(legacyShell, "-File", ScriptPath, "-ConfigurationPath", missingPath, "-NoPause");
        Assert.Equal(1, result.ExitCode);
        Assert.Contains("ScriptRequiresUnmatchedPSVersion", result.Error, StringComparison.Ordinal);
        Assert.Empty(result.Output);
        Assert.False(File.Exists(missingPath));
    }

    /// <summary>:param executable: 明确解释器。:param arguments: 实际脚本参数。:returns: 有时间限制并完整读取的实际结果。</summary>
    private static async Task<(int ExitCode, string Output, string Error)> RunShellAsync(
        string executable, params string[] arguments)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo(executable)
            {
                UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true,
            },
        };
        process.StartInfo.ArgumentList.Add("-NoLogo");
        process.StartInfo.ArgumentList.Add("-NoProfile");
        process.StartInfo.ArgumentList.Add("-NonInteractive");
        foreach (var argument in arguments)
            process.StartInfo.ArgumentList.Add(argument);
        Assert.True(process.Start());
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        using var budget = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        try
        {
            await process.WaitForExitAsync(budget.Token);
            return (process.ExitCode, await output, await error);
        }
        finally
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                using var exitBudget = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                await process.WaitForExitAsync(exitBudget.Token);
            }
        }
    }
}
