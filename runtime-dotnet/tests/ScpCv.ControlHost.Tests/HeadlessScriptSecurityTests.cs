// 控制服务脚本的凭据与解释器合同验证。项目：SCP-cv；文件：HeadlessScriptSecurityTests.cs；作者：Qintsg；日期：2026-10-23。
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;

namespace ScpCv.ControlHost.Tests;

public sealed class HeadlessScriptSecurityTests
{
    private static readonly string ScriptPath = Path.Combine(
        AppContext.BaseDirectory,
        "scripts",
        "run-headless.ps1");

    [Fact]
    public void ControlHostPasswordIsInheritedThroughEnvironmentInsteadOfCommandLine()
    {
        var script = File.ReadAllText(ScriptPath);

        Assert.Contains("Authentication__DevelopmentAccount__Password", script, StringComparison.Ordinal);
        Assert.DoesNotContain("--Authentication:DevelopmentAccount:Password", script, StringComparison.Ordinal);
    }

    [Fact]
    public void DetachedLauncherReferencesProtectedPasswordFileInsteadOfEmbeddingPassword()
    {
        var script = File.ReadAllText(ScriptPath);

        Assert.Contains("DevelopmentPasswordFile = '", script, StringComparison.Ordinal);
        Assert.DoesNotContain("(\"    DevelopmentPassword = '\"", script, StringComparison.Ordinal);
        Assert.Contains("icacls.exe", script, StringComparison.Ordinal);
    }

    [Fact]
    public void AllowedOriginsAcceptsAListAndConfiguresEachEntry()
    {
        var script = File.ReadAllText(ScriptPath);

        Assert.Contains("-split '[,;]'", script, StringComparison.Ordinal);
        Assert.Contains("--Authentication:AllowedOrigins:{0}={1}", script, StringComparison.Ordinal);
    }

    [Fact]
    public void WorkerRestartUsesReadinessTimeout()
    {
        var script = File.ReadAllText(ScriptPath);

        Assert.Contains("TimeoutSec = $TimeoutSeconds", script, StringComparison.Ordinal);
        Assert.Contains("-TimeoutSeconds $ReadyTimeoutSeconds", script, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ScriptParsesInPowerShell7AndRequiresCoreVersion72()
    {
        if (!OperatingSystem.IsWindows()) return;
        var script = File.ReadAllText(ScriptPath);
        Assert.DoesNotContain("powershell.exe", script, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("-ExecutionPolicy", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("$shellProcess.MainModule.FileName", script, StringComparison.Ordinal);
        Assert.Contains("$pwshPath", script, StringComparison.Ordinal);
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
    public async Task MissingDataRootFailsInPowerShell7BeforeCreatingResources()
    {
        if (!OperatingSystem.IsWindows()) return;
        var result = await RunPowerShellAsync("-File", ScriptPath, "-RuntimeRoot", AppContext.BaseDirectory);
        Assert.Equal(1, result.ExitCode);
        Assert.Contains("必须显式传入 -DataRoot", result.Error, StringComparison.Ordinal);
        Assert.DoesNotContain("ScriptRequires", result.Error, StringComparison.Ordinal);
        Assert.DoesNotContain("ControlHost 已无头启动", result.Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WindowsPowerShell51IsRejectedBeforeCreatingDataDirectory()
    {
        if (!OperatingSystem.IsWindows()) return;
        var dataRoot = Path.Combine(Path.GetTempPath(), $"scp-cv-requires-{Guid.NewGuid():N}");
        var legacyShell = Path.Combine(Environment.SystemDirectory, "WindowsPowerShell", "v1.0", "powershell.exe");
        var result = await RunShellAsync(legacyShell, "-File", ScriptPath, "-RuntimeRoot", AppContext.BaseDirectory, "-DataRoot", dataRoot);
        Assert.Equal(1, result.ExitCode);
        Assert.Contains("ScriptRequiresUnmatchedPSVersion", result.Error, StringComparison.Ordinal);
        Assert.Empty(result.Output);
        Assert.False(Directory.Exists(dataRoot));
    }

    [Fact]
    public async Task ExplicitAllowedHostsAcceptsDirectIpHostHeader()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var port = GetAvailablePort();
        var dataRoot = Path.Combine(Path.GetTempPath(), $"scp-cv-headless-hosts-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dataRoot);
        var passwordFile = Path.Combine(dataRoot, "password.txt");
        await File.WriteAllTextAsync(passwordFile, "Headless-host-test-password-123");
        var controlHost = Path.Combine(AppContext.BaseDirectory, "ScpCv.ControlHost.exe");
        var listenUrl = $"http://127.0.0.1:{port}";

        try
        {
            var start = await RunPowerShellAsync(
                "-File", ScriptPath,
                "-ControlHostPath", controlHost,
                "-RuntimeRoot", AppContext.BaseDirectory,
                "-DataRoot", dataRoot,
                "-ListenUrls", listenUrl,
                "-AllowedOrigins", "http://localhost",
                "-AllowedHosts", "localhost;127.0.0.1;192.0.2.10",
                "-SafetyMode", "Simulation",
                "-DevelopmentPasswordFile", passwordFile,
                "-ReadyTimeoutSeconds", "30");
            Assert.True(start.ExitCode == 0, $"run-headless.ps1 failed: {start.Error}\n{start.Output}");

            using var client = new HttpClient(new HttpClientHandler { UseProxy = false }) { BaseAddress = new Uri(listenUrl) };
            using var request = new HttpRequestMessage(HttpMethod.Get, "/health/ready");
            request.Headers.Host = $"192.0.2.10:{port}";
            using var response = await client.SendAsync(request);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
        finally
        {
            await RunPowerShellAsync(
                "-File", ScriptPath,
                "-Stop",
                "-RuntimeRoot", AppContext.BaseDirectory,
                "-DataRoot", dataRoot,
                "-ListenUrls", listenUrl,
                "-AllowedOrigins", "http://localhost");
        }
    }

    private static int GetAvailablePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            return ((IPEndPoint)listener.LocalEndpoint).Port;
        }
        finally
        {
            listener.Stop();
        }
    }

    /// <summary>:param arguments: 当前入口参数。:returns: 正常PowerShell7的实际退出与输出。</summary>
    private static Task<(int ExitCode, string Output, string Error)> RunPowerShellAsync(params string[] arguments) =>
        RunShellAsync("pwsh.exe", arguments);

    /// <summary>:param executable: 明确解释器。:param arguments: 实际脚本参数。:returns: 有时间限制并完整读取的实际结果。</summary>
    private static async Task<(int ExitCode, string Output, string Error)> RunShellAsync(
        string executable, params string[] arguments)
    {
        var startInfo = new ProcessStartInfo(executable)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        startInfo.ArgumentList.Add("-NoLogo");
        startInfo.ArgumentList.Add("-NoProfile");
        startInfo.ArgumentList.Add("-NonInteractive");
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("无法启动 PowerShell。");
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        using var budget = new CancellationTokenSource(TimeSpan.FromSeconds(60));
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
