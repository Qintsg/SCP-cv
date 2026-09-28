// 退役运行组脚本必须在任何进程或状态副作用前拒绝旧调用，并提供认证入口指引。
using System.Diagnostics;
using System.Text;

namespace ScpCv.ControlHost.Tests;

public sealed class RuntimeScriptContractTests
{
    private static readonly string ScriptPath = Path.Combine(
        AppContext.BaseDirectory, "scripts", "runtime.ps1");

    [Theory]
    [InlineData("start", "POST /api/system/restart/")]
    [InlineData("restart", "POST /api/system/restart/")]
    [InlineData("stop", "POST /api/system/shutdown/")]
    [InlineData("status", "GET /api/runtime/")]
    [InlineData(null, "GET /api/runtime/")]
    public async Task LegacyActionsFailBeforeRuntimeAccessAndDescribeReplacement(
        string? action,
        string replacement)
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var result = await RunGuardedScriptAsync(action);

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("已退役", result.Error, StringComparison.Ordinal);
        Assert.Contains(replacement, result.Error, StringComparison.Ordinal);
        Assert.Contains("ControlHost", result.Error, StringComparison.Ordinal);
        Assert.Contains("会话", result.Error, StringComparison.Ordinal);
        Assert.Contains("CSRF", result.Error, StringComparison.Ordinal);
        Assert.DoesNotContain("contract_side_effect", result.Error, StringComparison.Ordinal);
        Assert.DoesNotContain("contract_unexpected_success", result.Error, StringComparison.Ordinal);

        if (action is "start" or "restart")
        {
            Assert.Contains("run-headless.ps1", result.Error, StringComparison.Ordinal);
            Assert.Contains("-DataRoot", result.Error, StringComparison.Ordinal);
            Assert.Contains("BigScreenOutputs.Window1/Window2", result.Error, StringComparison.Ordinal);
        }

        if (action == "stop")
        {
            Assert.Contains("-Stop -DataRoot", result.Error, StringComparison.Ordinal);
            Assert.Contains("退出", result.Error, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void RetirementDoesNotRetainSupervisorInvocationOrIndependentStateFile()
    {
        var script = File.ReadAllText(ScriptPath);

        Assert.DoesNotContain("ScpCv.Supervisor.exe", script, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("runtime-processes.json", script, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("$supervisorArgs", script, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Start-Process", script, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Stop-Process", script, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ScriptHasWindowsPowerShellUtf8BomAndLfLineEndings()
    {
        var bytes = File.ReadAllBytes(ScriptPath);

        Assert.True(bytes.Length >= 3);
        Assert.Equal((byte)0xef, bytes[0]);
        Assert.Equal((byte)0xbb, bytes[1]);
        Assert.Equal((byte)0xbf, bytes[2]);
        Assert.DoesNotContain((byte)'\r', bytes);
    }

    /// <summary>
    /// 只在阻断文件探测、HTTP、进程和本机管理调用的 PowerShell 中运行旧入口。
    /// :param action: 旧动作；空值表示默认动作。
    /// :returns: 隔离 PowerShell 的退出码与标准错误。
    /// </summary>
    private static async Task<(int ExitCode, string Error)> RunGuardedScriptAsync(string? action)
    {
        var pathBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(ScriptPath));
        var actionArgument = action is null ? string.Empty : $" -Action '{action}'";
        var command = $$"""
            $ErrorActionPreference = 'Stop'
            [Console]::OutputEncoding = New-Object Text.UTF8Encoding($false)
            function Test-Path { throw 'contract_side_effect: Test-Path' }
            function Start-Process { throw 'contract_side_effect: Start-Process' }
            function Stop-Process { throw 'contract_side_effect: Stop-Process' }
            function Get-CimInstance { throw 'contract_side_effect: Get-CimInstance' }
            function Invoke-WebRequest { throw 'contract_side_effect: Invoke-WebRequest' }
            function Invoke-RestMethod { throw 'contract_side_effect: Invoke-RestMethod' }
            $scriptPath = [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String('{{pathBase64}}'))
            $tokens = $null
            $errors = $null
            [Management.Automation.Language.Parser]::ParseFile($scriptPath, [ref]$tokens, [ref]$errors) | Out-Null
            if ($errors.Count -gt 0) { [Console]::Error.WriteLine($errors[0].Message); exit 2 }
            try {
                & $scriptPath -RuntimeRoot 'C:\scp-cv-contract-nonexistent' -MediaMtxPath 'contract-unused'{{actionArgument}}
                throw 'contract_unexpected_success'
            }
            catch {
                [Console]::Error.WriteLine($_.Exception.Message)
                exit 1
            }
            """;
        var startInfo = new ProcessStartInfo("powershell.exe")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        string[] arguments =
        [
            "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-EncodedCommand",
            Convert.ToBase64String(Encoding.Unicode.GetBytes(command)),
        ];
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("无法启动隔离 PowerShell。");
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            // 仅终止本测试持有的隔离 PowerShell，绝不按名称查杀运行组。
            process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync();
            throw;
        }

        await output;
        return (process.ExitCode, await error);
    }
}
