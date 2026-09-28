// 只读观察已认证进程的真实退出；持有原生句柄，不按后来的同号 PID 推断存活。
using System.ComponentModel;
using System.Diagnostics;

namespace ScpCv.ControlHost.Ipc;

public sealed record RuntimeProcessExitEvidence(bool Confirmed, string Reason);

public interface IRuntimeProcessExitObserver
{
    /// <summary>核对身份并等待原进程退出；取消和无法取证均不提供死亡证明。</summary>
    /// <remarks>:param identity: 已通过管道 OS 验证的进程。:param cancellationToken: 宿主寿命。:returns: 原进程退出证据。</remarks>
    Task<RuntimeProcessExitEvidence> WaitForExitAsync(RegisteredProcessIdentity identity, CancellationToken cancellationToken);
}

public sealed class WindowsRuntimeProcessExitObserver : IRuntimeProcessExitObserver
{
    /// <summary>先锁定原生进程句柄和 PID/start/session，再观察退出。</summary>
    public async Task<RuntimeProcessExitEvidence> WaitForExitAsync(
        RegisteredProcessIdentity identity, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(identity);
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            using var process = Process.GetProcessById(identity.ProcessId);
            // SafeHandle 在等待前打开并由 Process 持有；PID 后续复用不改变所观察对象。
            _ = process.SafeHandle;
            if (process.HasExited) return new(true, "process_exited");
            var start = new DateTimeOffset(process.StartTime.ToUniversalTime(), TimeSpan.Zero);
            if (start != identity.ProcessStartTime || process.SessionId != identity.LogonSessionId)
                return new(true, "original_identity_replaced");
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            return new(true, "process_exited");
        }
        catch (ArgumentException) { return new(true, "process_not_found"); }
        catch (Win32Exception) { return new(false, "process_identity_unverifiable"); }
        catch (InvalidOperationException) { return new(false, "process_identity_unverifiable"); }
    }
}
