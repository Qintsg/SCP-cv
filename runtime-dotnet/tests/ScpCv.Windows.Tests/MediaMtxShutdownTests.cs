// 自有回环MediaMTX的协作退出回归；原句柄码0才通过，不将强杀或状态文件删除当正常。
using System.Diagnostics;
using ScpCv.Supervisor.Processes;
using ScpCv.Supervisor.Runtime;

namespace ScpCv.Windows.Tests;

public sealed class MediaMtxShutdownTests
{
    [Theory]
    [InlineData("role")]
    [InlineData("creation")]
    [InlineData("session")]
    [InlineData("pid")]
    [InlineData("cancelled")]
    public async Task UnknownOwnershipCannotSignalTheLiveService(string invalid)
    {
        var root = RepositoryRoot();
        var fixture = Path.Combine(root, ".validation", "mediamtx-stop", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(fixture);
        var configuration = Path.Combine(fixture, "mediamtx.yml");
        await File.WriteAllTextAsync(configuration, "rtsp: no\nrtmp: no\nhls: no\nwebrtc: no\nsrt: no\napi: no\npaths: {}\n");
        var registry = new ProcessRegistry();
        using var http = new HttpClient();
        var service = new MediaMtxProcess(registry, http);
        service.Start(Path.Combine(root, "tools", "third_party", "mediamtx", "mediamtx.exe"), configuration);
        using var original = service.Owned!.Process;
        _ = original.Handle;
        var untrusted = invalid switch
        {
            "role" => service.Owned with { Role = "player-1" },
            "creation" => service.Owned with { IsolatedConsole = false },
            "session" => service.Owned with { SessionId = service.Owned.SessionId + 1 },
            _ => service.Owned with { ProcessId = service.Owned.ProcessId + 1 },
        };
        try
        {
            if (invalid == "cancelled")
            {
                using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
                await Assert.ThrowsAsync<OperationCanceledException>(() => MediaMtxConsoleLifetime.TryStopAsync(service.Owned, cancelled.Token));
            }
            else Assert.False(await MediaMtxConsoleLifetime.TryStopAsync(untrusted));
            Assert.False(original.HasExited);
            await new ShutdownCoordinator(registry).StopAsync();
            Assert.Equal(0, original.ExitCode);
        }
        finally { if (!original.HasExited) { original.Kill(); await original.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5)); } }
    }

    [Fact]
    public async Task AdditionalConsoleMemberPreventsSignalAndBothProcessesRemainAlive()
    {
        var root = RepositoryRoot();
        var fixture = Path.Combine(root, ".validation", "mediamtx-stop", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(fixture);
        var configuration = Path.Combine(fixture, "mediamtx.yml");
        var ready = Path.Combine(fixture, "holder-ready");
        await File.WriteAllTextAsync(configuration, "rtsp: no\nrtmp: no\nhls: no\nwebrtc: no\nsrt: no\napi: no\npaths: {}\n");
        var registry = new ProcessRegistry();
        using var http = new HttpClient();
        var service = new MediaMtxProcess(registry, http);
        service.Start(Path.Combine(root, "tools", "third_party", "mediamtx", "mediamtx.exe"), configuration);
        using var original = service.Owned!.Process;
        _ = original.Handle;
        var start = new ProcessStartInfo(Path.Combine(AppContext.BaseDirectory, "PresentationTestHost", "ScpCv.Presentation.TestHost.exe"))
        { UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden };
        foreach (var value in new[] { "--console-holder", original.Id.ToString(System.Globalization.CultureInfo.InvariantCulture),
            service.Owned.StartTime.UtcTicks.ToString(System.Globalization.CultureInfo.InvariantCulture), ready }) start.ArgumentList.Add(value);
        using var holder = Process.Start(start)!;
        _ = holder.Handle;
        try
        {
            var budget = Stopwatch.StartNew();
            while (!File.Exists(ready)) { Assert.False(holder.HasExited); Assert.True(budget.Elapsed < TimeSpan.FromSeconds(10)); await Task.Delay(50); }
            Assert.False(await MediaMtxConsoleLifetime.TryStopAsync(service.Owned));
            Assert.False(original.HasExited); Assert.False(holder.HasExited);
            holder.Kill(); await holder.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
            await new ShutdownCoordinator(registry).StopAsync();
            Assert.Equal(0, original.ExitCode);
        }
        finally
        {
            if (!holder.HasExited) { holder.Kill(); await holder.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5)); }
            if (!original.HasExited) { original.Kill(); await original.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5)); }
        }
    }

    [Fact]
    public async Task StaleRecordedIdentityCannotSignalTheLiveConsoleService()
    {
        var root = RepositoryRoot();
        var fixture = Path.Combine(root, ".validation", "mediamtx-stop", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(fixture);
        var configuration = Path.Combine(fixture, "mediamtx.yml");
        await File.WriteAllTextAsync(configuration, "rtsp: no\nrtmp: no\nhls: no\nwebrtc: no\nsrt: no\napi: no\npaths: {}\n");
        var registry = new ProcessRegistry();
        using var http = new HttpClient();
        var service = new MediaMtxProcess(registry, http);
        service.Start(Path.Combine(root, "tools", "third_party", "mediamtx", "mediamtx.exe"), configuration);
        using var original = service.Owned!.Process;
        _ = original.Handle;
        try
        {
            Assert.False(await MediaMtxConsoleLifetime.TryStopAsync(service.Owned with { StartTime = service.Owned.StartTime.AddSeconds(1) }));
            Assert.False(original.HasExited);
            await new ShutdownCoordinator(registry).StopAsync();
            Assert.Equal(0, original.ExitCode);
        }
        finally { if (!original.HasExited) { original.Kill(); await original.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5)); } }
    }

    [Fact]
    public async Task RegisteredConsoleServiceStopsNormallyWithoutForceTermination()
    {
        var root = RepositoryRoot();
        var executable = Path.Combine(root, "tools", "third_party", "mediamtx", "mediamtx.exe");
        Assert.True(File.Exists(executable), "本机需已有项目MediaMTX运行时，不能跳过后称通过。");
        var fixture = Path.Combine(root, ".validation", "mediamtx-stop", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(fixture);
        var configuration = Path.Combine(fixture, "mediamtx.yml");
        var log = Path.Combine(fixture, "mediamtx.log").Replace('\\', '/');
        await File.WriteAllTextAsync(configuration, $"logDestinations: [file]\nlogFile: '{log}'\nrtsp: no\nrtmp: no\nhls: no\nwebrtc: no\nsrt: no\napi: yes\napiAddress: 127.0.0.1:39771\npaths: {{}}\n");
        var registry = new ProcessRegistry();
        using var http = new HttpClient(new HttpClientHandler { UseProxy = false }) { Timeout = TimeSpan.FromSeconds(1) };
        var service = new MediaMtxProcess(registry, http);
        service.Start(executable, configuration);
        using var original = service.Owned!.Process;
        _ = original.Handle;
        try
        {
            var budget = Stopwatch.StartNew();
            while (!await service.IsHealthyAsync(new Uri("http://127.0.0.1:39771/v3/config/global/get")))
            {
                Assert.False(original.HasExited, "自有服务提前退出；证据目录保留。");
                Assert.True(budget.Elapsed < TimeSpan.FromSeconds(15), "回环服务没有就绪。");
                await Task.Delay(100);
            }
            await new ShutdownCoordinator(registry).StopAsync();
            Assert.True(original.HasExited);
            Assert.Equal(0, original.ExitCode);
            Assert.Empty(registry.Snapshot());
        }
        finally
        {
            // 仅清理当前原句柄对应的自有夹具，不按名称清理其他服务；强杀不算通过。
            if (!original.HasExited) { original.Kill(); await original.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5)); }
        }
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "AGENTS.md"))) directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("找不到受控测试仓库。");
    }
}
