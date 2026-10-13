// 自有进程原身份登记；独立控制台创建证明只在内存保留，不接受状态文件授信。
using System.Diagnostics;

namespace ScpCv.Supervisor.Processes;

public sealed record OwnedProcess(
    string Role,
    int ProcessId,
    DateTimeOffset StartTime,
    int SessionId,
    Process Process,
    Guid InstanceId = default,
    bool IsolatedConsole = false);

public sealed class ProcessRegistry
{
    private readonly Dictionary<int, OwnedProcess> _processes = [];
    private readonly object _gate = new();

    public OwnedProcess Register(string role, Process process, Guid instanceId = default, bool isolatedConsole = false)
    {
        ArgumentNullException.ThrowIfNull(process);
        process.Refresh();
        var owned = new OwnedProcess(
            role,
            process.Id,
            new DateTimeOffset(process.StartTime.ToUniversalTime(), TimeSpan.Zero),
            process.SessionId,
            process,
            instanceId,
            isolatedConsole);
        lock (_gate) _processes[process.Id] = owned;
        return owned;
    }

    public static bool StillOwns(OwnedProcess owned)
    {
        try
        {
            owned.Process.Refresh();
            return !owned.Process.HasExited && owned.Process.Id == owned.ProcessId && owned.Process.SessionId == owned.SessionId &&
                new DateTimeOffset(owned.Process.StartTime.ToUniversalTime(), TimeSpan.Zero) == owned.StartTime;
        }
        catch (InvalidOperationException) { return false; }
    }

    public IReadOnlyList<OwnedProcess> Snapshot() { lock (_gate) return _processes.Values.ToArray(); }
    public void Remove(int processId) { lock (_gate) _processes.Remove(processId); }
}
