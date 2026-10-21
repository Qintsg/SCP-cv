// 受信输入的有界服务端口探测；不拉媒体、不探任意地址，Simulation不产生网络。
using System.Collections.ObjectModel;
using System.Net.Sockets;

namespace ScpCv.Infrastructure.Devices;

public sealed class InputDiscoveryOptions
{
    public int TimeoutMilliseconds { get; set; } = 250;
    public int MaximumConcurrentInputs { get; set; } = 6;
    public int CacheSeconds { get; set; } = 60;
    public void Validate()
    {
        if (TimeoutMilliseconds is < 50 or > 1000 || MaximumConcurrentInputs is < 1 or > 12 || CacheSeconds is < 10 or > 300)
            throw new InvalidOperationException("输入探测配置超出有界范围。");
    }
}

public sealed record InputReachability(bool Rtsp, bool Rtmp, string Status, DateTimeOffset? CheckedAt)
{
    public bool Available => Rtsp || Rtmp;
    public static InputReachability Unknown { get; } = new(false, false, "unknown", null);
}

public interface IInputReachabilityProbe
{
    Task<InputReachability> ProbeAsync(SiteInputDefinition input, CancellationToken cancellationToken);
}

public sealed class SimulationInputReachabilityProbe : IInputReachabilityProbe
{
    public Task<InputReachability> ProbeAsync(SiteInputDefinition input, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(InputReachability.Unknown);
    }
}

public sealed class TcpInputReachabilityProbe(InputDiscoveryOptions options, TimeProvider clock) : IInputReachabilityProbe
{
    /// <summary>:param input: 固定目录项。:param cancellationToken: 本次发现寿命。:returns: 仅端口服务可达，不代表出画。</summary>
    public async Task<InputReachability> ProbeAsync(SiteInputDefinition input, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (!SiteInputCatalog.All.Contains(input)) throw new ArgumentException("只能探测受信目录输入。", nameof(input));
        var rtsp = await ConnectAsync(input.Address, 554, cancellationToken).ConfigureAwait(false);
        var rtmp = input.Group != "camera" && await ConnectAsync(input.Address, 1935, cancellationToken).ConfigureAwait(false);
        return new(rtsp, rtmp, rtsp || rtmp ? "reachable" : "unreachable", clock.GetUtcNow());
    }

    private async Task<bool> ConnectAsync(string host, int port, CancellationToken cancellationToken)
    {
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        budget.CancelAfter(options.TimeoutMilliseconds);
        using var client = new TcpClient();
        try { await client.ConnectAsync(host, port, budget.Token).ConfigureAwait(false); return client.Connected; }
        catch (Exception exception) when (exception is SocketException or IOException || exception is OperationCanceledException && !cancellationToken.IsCancellationRequested) { return false; }
    }
}

public sealed class InputDiscovery(IInputReachabilityProbe probe, InputDiscoveryOptions options, TimeProvider clock) : IAsyncDisposable
{
    private readonly Lock _sync = new();
    private readonly CancellationTokenSource _lifetime = new();
    private IReadOnlyDictionary<string, InputReachability> _snapshot = new ReadOnlyDictionary<string, InputReachability>(new Dictionary<string, InputReachability>());
    private Task<IReadOnlyDictionary<string, InputReachability>>? _refresh;
    private DateTimeOffset _expires;

    public IReadOnlyDictionary<string, InputReachability> Snapshot { get { lock (_sync) return _snapshot; } }

    /// <summary>:param force: 显式刷新仍只合并一个在途任务。:param token: 等待者取消不取消其它等待者。:returns: 有TTL、固定并发的端口快照。</summary>
    public async Task<IReadOnlyDictionary<string, InputReachability>> RefreshAsync(bool force, CancellationToken token = default)
    {
        options.Validate();
        Task<IReadOnlyDictionary<string, InputReachability>> task;
        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(_lifetime.IsCancellationRequested, this);
            if (!force && clock.GetUtcNow() < _expires) return _snapshot;
            task = _refresh is { IsCompleted: false } running ? running : _refresh = RefreshCoreAsync(_lifetime.Token);
        }
        return await task.WaitAsync(token).ConfigureAwait(false);
    }

    private async Task<IReadOnlyDictionary<string, InputReachability>> RefreshCoreAsync(CancellationToken token)
    {
        using var parallel = new SemaphoreSlim(options.MaximumConcurrentInputs);
        var tasks = SiteInputCatalog.All.Select(async input =>
        {
            await parallel.WaitAsync(token).ConfigureAwait(false);
            try { return new KeyValuePair<string, InputReachability>(input.Id, await probe.ProbeAsync(input, token).ConfigureAwait(false)); }
            finally { parallel.Release(); }
        });
        var values = await Task.WhenAll(tasks).ConfigureAwait(false);
        var snapshot = new ReadOnlyDictionary<string, InputReachability>(values.ToDictionary(StringComparer.Ordinal));
        lock (_sync) { _snapshot = snapshot; _expires = clock.GetUtcNow().AddSeconds(options.CacheSeconds); }
        return snapshot;
    }

    public async ValueTask DisposeAsync()
    {
        await _lifetime.CancelAsync().ConfigureAwait(false);
        Task? pending; lock (_sync) pending = _refresh;
        if (pending is not null) { try { await pending.ConfigureAwait(false); } catch (OperationCanceledException) { /* 自有发现已退出。 */ } }
        _lifetime.Dispose();
    }
}
