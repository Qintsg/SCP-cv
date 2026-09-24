// Supervisor 运行组状态文件的原子发布与跨进程归属校验。
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ScpCv.Supervisor.Runtime;

public sealed record SupervisorProcessState(string Role, int ProcessId, DateTimeOffset StartTime, int SessionId);

public static class SupervisorStateStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public static SupervisorProcessState[] Read(string path) =>
        WithLock(path, () => ReadCore(path));

    public static void Write(string path, IReadOnlyList<SupervisorProcessState> processes)
    {
        ArgumentNullException.ThrowIfNull(processes);
        WithLock(path, () =>
        {
            var fullPath = Path.GetFullPath(path);
            var parent = Path.GetDirectoryName(fullPath)
                ?? throw new InvalidOperationException("运行组状态文件缺少父目录。");
            Directory.CreateDirectory(parent);
            var temporaryPath = Path.Combine(parent, $".{Path.GetFileName(fullPath)}.{Guid.NewGuid():N}.tmp");
            try
            {
                File.WriteAllText(temporaryPath, JsonSerializer.Serialize(processes, JsonOptions), new UTF8Encoding(false));
                File.Move(temporaryPath, fullPath, overwrite: true);
            }
            finally
            {
                if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
            }
            return true;
        });
    }

    public static bool DeleteIfMatches(string path, IReadOnlyList<SupervisorProcessState> expected)
    {
        ArgumentNullException.ThrowIfNull(expected);
        return WithLock(path, () =>
        {
            var fullPath = Path.GetFullPath(path);
            if (expected.Count == 0 || !File.Exists(fullPath)) return false;
            var current = ReadCore(fullPath);
            if (!current.SequenceEqual(expected)) return false;
            File.Delete(fullPath);
            return true;
        });
    }

    private static SupervisorProcessState[] ReadCore(string path)
    {
        var fullPath = Path.GetFullPath(path);
        if (!File.Exists(fullPath)) return [];
        using var stream = File.OpenRead(fullPath);
        return JsonSerializer.Deserialize<SupervisorProcessState[]>(stream, JsonOptions) ?? [];
    }

    private static T WithLock<T>(string path, Func<T> action)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(action);
        var normalizedPath = Path.GetFullPath(path).ToUpperInvariant();
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalizedPath)));
        using var mutex = new Mutex(false, $@"Local\ScpCv.Supervisor.State.{hash}");
        var acquired = false;
        try
        {
            try
            {
                acquired = mutex.WaitOne(TimeSpan.FromSeconds(15));
            }
            catch (AbandonedMutexException)
            {
                acquired = true;
            }
            if (!acquired) throw new TimeoutException("等待 Supervisor 状态文件锁超时。");
            return action();
        }
        finally
        {
            if (acquired) mutex.ReleaseMutex();
        }
    }
}
