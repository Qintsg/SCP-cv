// Supervisor 对已登记子进程的协作停机与定向强制退出。
using ScpCv.Supervisor.Processes;

namespace ScpCv.Supervisor.Runtime;

public sealed class ShutdownCoordinator(ProcessRegistry registry, TimeSpan? cooperativeTimeout = null)
{
    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        foreach (var owned in registry.Snapshot())
        {
            if (!ProcessRegistry.StillOwns(owned)) { registry.Remove(owned.ProcessId); continue; }
            try { owned.Process.CloseMainWindow(); } catch (InvalidOperationException) { }
        }

        var deadline = DateTimeOffset.UtcNow.Add(cooperativeTimeout ?? TimeSpan.FromSeconds(5));
        while (DateTimeOffset.UtcNow < deadline && registry.Snapshot().Any(ProcessRegistry.StillOwns))
            await Task.Delay(100, cancellationToken).ConfigureAwait(false);

        foreach (var owned in registry.Snapshot())
        {
            if (!ProcessRegistry.StillOwns(owned)) { registry.Remove(owned.ProcessId); continue; }
            // office 角色是项目自有 PowerPointHost，不是 POWERPNT.EXE。
            // 只结束 Host 本身；绝不能用进程树强杀可能包含用户文稿的 Office 实例。
            owned.Process.Kill(entireProcessTree: owned.Role != "office");
            await owned.Process.WaitForExitAsync(cancellationToken).WaitAsync(TimeSpan.FromSeconds(3), cancellationToken).ConfigureAwait(false);
            registry.Remove(owned.ProcessId);
        }
    }
}
