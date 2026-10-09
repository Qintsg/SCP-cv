// 真实测试侧车的精确句柄与有界收尾；兜底结束只用于防挂死，不计作测试成功。
using System.Diagnostics;
using ScpCv.Contracts.Presentations;
using ScpCv.Presentation.Protocol;

namespace ScpCv.Contracts.Tests;

internal sealed class PresentationTransportFixture : IAsyncDisposable
{
    private readonly Process _owned;
    internal string Root { get; } = Path.Combine(Path.GetTempPath(), "scp-cv-wire-" + Guid.NewGuid().ToString("N"));
    internal PresentationProcessClient Client { get; }
    internal bool HasExited => _owned.HasExited;
    internal int ExitCode => _owned.ExitCode;

    /// <summary>:param mode: 固定外部故障。:param scope: 真实客户端分配的身份。:returns: 仅自有测试侧车。</summary>
    internal PresentationTransportFixture(string mode, PresentationScope scope)
    {
        Client = new PresentationProcessClient(
            Path.Combine(AppContext.BaseDirectory, "PresentationTestHost", "ScpCv.Presentation.TestHost.exe"),
            Path.Combine(Path.GetTempPath(), mode), Root, scope);
        _owned = Process.GetProcessById(Client.ProcessId);
        _ = _owned.Handle;
    }

    /// <summary>:param operation: 可能已失败的在途任务。:returns: 有界观察，外部强清不能擦除超时失败。</summary>
    internal async Task ObserveForCleanupAsync(Task operation)
    {
        try { await operation.WaitAsync(TimeSpan.FromSeconds(3)); }
        catch (Exception error) when (error is IOException or OperationCanceledException or ArgumentOutOfRangeException) { }
        catch (TimeoutException)
        {
            await TerminateOwnedAsync();
            throw;
        }
    }

    /// <summary>:returns: 只作用于启动时保留的精确句柄，最多等待5秒。</summary>
    private async Task TerminateOwnedAsync()
    {
        if (!_owned.HasExited) _owned.Kill();
        using var budget = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await _owned.WaitForExitAsync(budget.Token);
    }

    /// <summary>:returns: 协作退出必须有界；超时即测试失败，不能在finally永久等待。</summary>
    public async ValueTask DisposeAsync()
    {
        var close = Client.DisposeAsync().AsTask();
        try { await close.WaitAsync(TimeSpan.FromSeconds(5)); }
        catch (TimeoutException)
        {
            await TerminateOwnedAsync();
            try { await close.WaitAsync(TimeSpan.FromSeconds(5)); }
            catch (IOException) { }
            throw new TimeoutException("测试侧车协作关闭超时，已精确结束自有进程；不能标为通过。");
        }
        finally { _owned.Dispose(); }
    }
}
