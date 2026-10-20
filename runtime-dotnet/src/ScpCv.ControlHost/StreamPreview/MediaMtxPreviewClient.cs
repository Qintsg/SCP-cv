// 现有MediaMTX的私有按需路径；只管理本Host随机命名空间，不读写正式流配置。
using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;

namespace ScpCv.ControlHost.StreamPreview;

public sealed class MediaMtxPreviewClient : IDisposable
{
    private readonly HttpClient _http;
    private readonly PreviewOptions _options;
    private readonly string _prefix = "scp-preview-" + Guid.NewGuid().ToString("N") + "-";
    private readonly ConcurrentDictionary<string, bool> _owned = new(StringComparer.Ordinal);
    private readonly object _registration = new();

    /// <summary>:param http: 私有无代理回环客户端。:param options: 已验证预算。:returns: 不探测、不启动任何流。</summary>
    public MediaMtxPreviewClient(HttpClient http, PreviewOptions options)
    {
        options.Validate(); _http = http; _options = options;
    }

    public int OwnedPathCount => _owned.Count;
    public void Dispose() => _http.Dispose();

    /// <summary>:param source: 已许可的服务端流。:param cancellationToken: 创建取消。:returns: 唯一按需路径；登记成功也不代表上游已出画。</summary>
    public async Task<MediaMtxPreviewPath> RegisterAsync(PreviewSource source, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(source);
        var name = _prefix + Guid.NewGuid().ToString("N");
        lock (_registration)
        {
            if (_owned.Count >= _options.MaximumFeeds * 4)
                throw new PreviewException("预览资源正在清理，请稍后重试", "preview_capacity", 429);
            if (!_owned.TryAdd(name, true)) throw new InvalidOperationException("预览路径身份冲突");
        }
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, Api("add", name));
            request.Content = JsonContent.Create(new
            {
                source = source.UpstreamUri, sourceOnDemand = true, sourceOnDemandStartTimeout = "8s",
                sourceOnDemandCloseAfter = "1s", rtspTransport = "tcp",
            });
            using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode) throw new PreviewException("MediaMTX无法登记此预览流", "preview_relay_unavailable");
            return new MediaMtxPreviewPath(this, name, $"rtsp://127.0.0.1:{_options.LocalRtspPort}/{name}");
        }
        catch (Exception error) when (error is HttpRequestException or OperationCanceledException or PreviewException)
        {
            _owned[name] = false;
            await TryCleanupAsync(name).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            throw new PreviewException("预览中继暂不可用，请检查MediaMTX", "preview_relay_unavailable");
        }
    }

    /// <summary>:returns: 仅重试本实例已经请求关闭的路径；绝不扫描／删除其它MediaMTX项。</summary>
    public async Task CleanupPendingAsync()
    {
        foreach (var pair in _owned)
            if (!pair.Value) await TryCleanupAsync(pair.Key).ConfigureAwait(false);
    }

    internal async Task ReleaseAsync(string name)
    {
        if (!_owned.TryGetValue(name, out _)) return;
        _owned[name] = false;
        if (!await TryCleanupAsync(name).ConfigureAwait(false))
            throw new PreviewException("预览已停止，但中继路径尚待清理", "preview_cleanup_pending");
    }

    /// <summary>:param name: 本实例仍活跃的路径。:param source: 原来源。:param cancellationToken: 本轮取消。:returns: MediaMTX重启丢失动态项后，仅重建该自有按需路径。</summary>
    internal async Task EnsureAsync(string name, PreviewSource source, CancellationToken cancellationToken)
    {
        if (!_owned.TryGetValue(name, out var active) || !active) throw new PreviewException("原预览路径已释放", "preview_cleanup_pending");
        try
        {
            using var lookup = new HttpRequestMessage(HttpMethod.Get, Api("get", name));
            using var found = await _http.SendAsync(lookup, cancellationToken).ConfigureAwait(false);
            if (found.IsSuccessStatusCode) return;
            if (found.StatusCode != HttpStatusCode.NotFound) throw new PreviewException("预览中继暂不可用", "preview_relay_unavailable");
            using var add = new HttpRequestMessage(HttpMethod.Post, Api("add", name));
            add.Content = JsonContent.Create(new { source = source.UpstreamUri, sourceOnDemand = true,
                sourceOnDemandStartTimeout = "8s", sourceOnDemandCloseAfter = "1s", rtspTransport = "tcp" });
            using var recreated = await _http.SendAsync(add, cancellationToken).ConfigureAwait(false);
            if (!recreated.IsSuccessStatusCode) throw new PreviewException("预览路径恢复暂未完成", "preview_relay_unavailable");
        }
        catch (HttpRequestException) { throw new PreviewException("预览中继暂不可用", "preview_relay_unavailable"); }
    }

    private async Task<bool> TryCleanupAsync(string name)
    {
        if (!name.StartsWith(_prefix, StringComparison.Ordinal) || !_owned.TryGetValue(name, out var active) || active) return false;
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Delete, Api("delete", name));
            using var response = await _http.SendAsync(request, timeout.Token).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode && response.StatusCode != HttpStatusCode.NotFound) return false;
            _owned.TryRemove(name, out _); return true;
        }
        catch (Exception error) when (error is HttpRequestException or OperationCanceledException) { return false; }
    }

    private Uri Api(string operation, string name) => new(new Uri(_options.ApiBase.TrimEnd('/') + "/"), $"v3/config/paths/{operation}/{name}");
}

public sealed class MediaMtxPreviewPath : IAsyncDisposable
{
    private readonly MediaMtxPreviewClient _owner;
    private readonly string _name;
    public string LocalRtspUri { get; }

    internal MediaMtxPreviewPath(MediaMtxPreviewClient owner, string name, string localRtspUri)
    { _owner = owner; _name = name; LocalRtspUri = localRtspUri; }

    /// <summary>:returns: 只释放这一个随机所有权路径；不操作MediaMTX进程。</summary>
    public ValueTask DisposeAsync() => new(_owner.ReleaseAsync(_name));
    public Task EnsureAsync(PreviewSource source, CancellationToken cancellationToken) => _owner.EnsureAsync(_name, source, cancellationToken);
}
