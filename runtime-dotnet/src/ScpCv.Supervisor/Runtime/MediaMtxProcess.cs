// MediaMTX自有原进程与只读健康探测；启动采用隐藏独立控制台，协作退出不影响其它应用。
using ScpCv.Supervisor.Processes;

namespace ScpCv.Supervisor.Runtime;

public sealed class MediaMtxProcess(ProcessRegistry registry, HttpClient httpClient)
{
    public OwnedProcess? Owned { get; private set; }
    public void Start(string executable, string configPath)
    {
        if (Owned is not null && ProcessRegistry.StillOwns(Owned)) return;
        if (configPath.Contains('"')) throw new InvalidOperationException("MediaMTX配置路径含非法引号。");
        var process = MediaMtxConsoleLifetime.Start(executable, $"\"{Path.GetFullPath(configPath)}\"");
        Owned = registry.Register("mediamtx", process, isolatedConsole: true);
    }

    public async Task<bool> IsHealthyAsync(Uri healthUri, CancellationToken cancellationToken = default)
    {
        try { using var response = await httpClient.GetAsync(healthUri, cancellationToken).ConfigureAwait(false); return response.IsSuccessStatusCode; }
        catch (HttpRequestException) { return false; }
    }
}
