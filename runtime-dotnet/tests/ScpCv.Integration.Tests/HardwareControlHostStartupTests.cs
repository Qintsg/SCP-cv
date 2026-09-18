using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using Microsoft.Data.Sqlite;

namespace ScpCv.Integration.Tests;

public sealed class HardwareControlHostStartupTests
{
    /// <summary>
    /// 就绪预算。子进程是冷启动（dotnet 宿主 + JIT + 数据库初始化），与其它测试工程或前端构建并行时
    /// 实测可明显超过 10 秒，原先的 10 秒预算会让负载下的正常启动变成假失败。
    /// 真正的启动死锁（例如单例构造环）表现为进程一直存活且永不就绪，仍会在这个预算内失败，
    /// 因此放宽预算不削弱该回归的检出能力，只影响失败时的等待时长。
    /// </summary>
    private static readonly TimeSpan ReadyTimeout = TimeSpan.FromSeconds(60);

    [Fact]
    public async Task HardwareControlHostReachesReadyEndpointWithoutDependencyDeadlock()
    {
        if (!OperatingSystem.IsWindows()) return;

        var dataRoot = Path.Combine(
            Path.GetTempPath(),
            "scp-cv-hardware-startup-tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dataRoot);
        var port = ReserveLoopbackPort();
        var baseAddress = new Uri($"http://127.0.0.1:{port}");
        var assemblyPath = typeof(Program).Assembly.Location;
        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"\"{assemblyPath}\" --SafetyMode=Hardware --urls={baseAddress} --DataRoot=\"{dataRoot}\"",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        }) ?? throw new InvalidOperationException("无法启动 Hardware ControlHost 测试进程。");

        // 必须立刻开始排空 stdout/stderr：重定向后管道缓冲区（约 4 KB）一旦写满，子进程会阻塞在写日志上，
        // 从而制造出“未就绪”的假象；同时把输出留到失败时做诊断。
        var standardOutput = process.StandardOutput.ReadToEndAsync();
        var standardError = process.StandardError.ReadToEndAsync();

        try
        {
            using var client = new HttpClient { BaseAddress = baseAddress };
            using var deadline = new CancellationTokenSource(ReadyTimeout);
            Exception? lastError = null;
            while (!deadline.IsCancellationRequested)
            {
                // 先于超时判定进程早退，避免子进程崩溃时白等到预算耗尽才报一个无信息量的取消异常。
                if (process.HasExited)
                {
                    throw new Xunit.Sdk.XunitException(
                        $"Hardware ControlHost 在就绪前退出，退出码 {process.ExitCode}。\n" +
                        $"stdout:\n{await standardOutput}\nstderr:\n{await standardError}");
                }

                try
                {
                    using var response = await client.GetAsync("/health/ready", deadline.Token);
                    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                    return;
                }
                catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
                {
                    lastError = exception;
                    await Task.Delay(250, CancellationToken.None);
                }
            }

            if (!process.HasExited) process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync();
            throw new Xunit.Sdk.XunitException(
                $"Hardware ControlHost 未在 {ReadyTimeout.TotalSeconds:0} 秒内就绪。" +
                $"最后错误：{lastError?.Message}\nstdout:\n{await standardOutput}\nstderr:\n{await standardError}");
        }
        finally
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync();
            SqliteConnection.ClearAllPools();
            // 被终止的子进程可能在退出后仍短暂持有 SQLite 文件句柄；清理按重试处理，不掩盖启动断言。
            for (var attempt = 0; attempt < 20 && Directory.Exists(dataRoot); attempt++)
            {
                try
                {
                    Directory.Delete(dataRoot, recursive: true);
                }
                catch (IOException) when (attempt < 19)
                {
                    await Task.Delay(250);
                }
            }
        }
    }

    private static int ReserveLoopbackPort()
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
}
