// 原生退出观察的只读进程句柄回归；只启动及清理本测试自有休眠进程。
using System.Diagnostics;
using ScpCv.ControlHost.Ipc;

namespace ScpCv.Integration.Tests;

public sealed class NativeProcessExitObserverTests
{
    /// <summary>进程仍在时持续观察，只有精确自有进程退出后给出证明。</summary>
    [Fact]
    public async Task ExactOwnedProcessMustExitBeforeEvidenceIsConfirmed()
    {
        using var process = Process.Start(new ProcessStartInfo("powershell.exe")
        {
            ArgumentList = { "-NoProfile", "-Command", "Start-Sleep -Seconds 30" },
            UseShellExecute = false,
            CreateNoWindow = true,
        }) ?? throw new InvalidOperationException("无法创建测试自有进程。");
        using var budget = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        try
        {
            var watching = new WindowsRuntimeProcessExitObserver().WaitForExitAsync(Identity(process), budget.Token);
            Assert.False(watching.IsCompleted);
            process.Kill();
            var evidence = await watching;
            Assert.True(evidence.Confirmed);
            Assert.Equal("process_exited", evidence.Reason);
        }
        finally
        {
            if (!process.HasExited) process.Kill();
            await process.WaitForExitAsync();
        }
    }

    /// <summary>宿主取消只结束观察，不能报告原生死亡。</summary>
    [Fact]
    public async Task CancelledObservationDoesNotProduceExitEvidence()
    {
        using var current = Process.GetCurrentProcess();
        using var cancelled = new CancellationTokenSource();
        var watching = new WindowsRuntimeProcessExitObserver().WaitForExitAsync(Identity(current), cancelled.Token);
        Assert.False(watching.IsCompleted);
        cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => watching);
        Assert.False(current.HasExited);
    }

    /// <summary>PID 已被另一个 start/session 占用时只证明原身份不再存在，不等待新进程。</summary>
    [Fact]
    public async Task ReusedPidIdentityDoesNotObserveReplacementProcessAsOriginal()
    {
        using var current = Process.GetCurrentProcess();
        var previous = Identity(current) with { ProcessStartTime = DateTimeOffset.UnixEpoch };
        var evidence = await new WindowsRuntimeProcessExitObserver().WaitForExitAsync(previous, CancellationToken.None);
        Assert.True(evidence.Confirmed);
        Assert.Equal("original_identity_replaced", evidence.Reason);
        Assert.False(current.HasExited);
    }

    /// <summary>捕获自有进程的精确身份。</summary>
    /// <remarks>:param process: 已知归属的测试进程。:returns: 已核对的身份。</remarks>
    private static RegisteredProcessIdentity Identity(Process process) => new(process.Id,
        new DateTimeOffset(process.StartTime.ToUniversalTime(), TimeSpan.Zero), process.SessionId, "supervisor", Guid.NewGuid());
}
