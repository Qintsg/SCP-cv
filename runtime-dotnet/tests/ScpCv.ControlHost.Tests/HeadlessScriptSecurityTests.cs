using System.Diagnostics;
using System.Net;
using System.Net.Sockets;

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

            using var client = new HttpClient { BaseAddress = new Uri(listenUrl) };
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

    private static async Task<(int ExitCode, string Output, string Error)> RunPowerShellAsync(
        params string[] arguments)
    {
        var startInfo = new ProcessStartInfo("powershell.exe")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        startInfo.ArgumentList.Add("-NoProfile");
        startInfo.ArgumentList.Add("-ExecutionPolicy");
        startInfo.ArgumentList.Add("Bypass");
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("无法启动 PowerShell。");
        await process.WaitForExitAsync();
        return (process.ExitCode, string.Empty, string.Empty);
    }
}
