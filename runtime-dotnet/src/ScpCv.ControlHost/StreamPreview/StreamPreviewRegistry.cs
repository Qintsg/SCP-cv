// 观看租约与同源生产者的原子注册；POST只登记需求，实际消费才拉流。
using System.Threading.Channels;

namespace ScpCv.ControlHost.StreamPreview;

public sealed record PreviewLeaseSnapshot(Guid Id, DateTimeOffset ExpiresAt, bool Connected, string Status);

public sealed class StreamPreviewRegistry(PreviewOptions options, IPreviewProducer producer,
    PreviewBandwidth budget, TimeProvider clock) : IAsyncDisposable
{
    private readonly object _sync = new();
    private readonly Dictionary<Guid, Lease> _leases = [];
    private readonly Dictionary<string, SharedPreviewFeed> _feeds = new(StringComparer.Ordinal);
    private bool _stopping;
    public int LeaseCount { get { lock (_sync) return _leases.Count; } }
    public int FeedCount { get { lock (_sync) return _feeds.Count; } }

    /// <summary>:param owner: 当前用户／协议／登录代次摘要。:param origin: 精确页面来源。:param source: 已许可来源。:returns: 有界短租约，不启动任何上游。</summary>
    public PreviewLeaseSnapshot Create(string owner, string origin, PreviewSource source)
    {
        lock (_sync)
        {
            if (_stopping) throw new PreviewException("预览服务正在停止");
            if (_leases.Count >= options.MaximumLeases) throw new PreviewException("预览观看数量已达上限", "preview_capacity", 429);
            var lease = new Lease(Guid.NewGuid(), owner, origin, source, clock.GetUtcNow().AddSeconds(options.LeaseSeconds));
            _leases.Add(lease.Id, lease); return Snapshot(lease);
        }
    }

    /// <summary>:param owner: 本次认证身份。:param origin: 建立来源。:param id: 租约。:returns: 仅有效本人同代次租约快照。</summary>
    public PreviewLeaseSnapshot Get(string owner, string origin, Guid id)
    { lock (_sync) return Snapshot(Owned(owner, origin, id)); }

    public PreviewSource Source(string owner, string origin, Guid id)
    { lock (_sync) return Owned(owner, origin, id).Source; }

    /// <summary>:param owner: 本次身份。:param origin: 精确来源。:param id: 租约。:returns: 延长有效租约，不复活过期者、不创建第二个消费连接。</summary>
    public PreviewLeaseSnapshot Heartbeat(string owner, string origin, Guid id)
    {
        lock (_sync)
        {
            var lease = Owned(owner, origin, id); lease.ExpiresAt = clock.GetUtcNow().AddSeconds(options.LeaseSeconds); return Snapshot(lease);
        }
    }

    /// <summary>:param owner: 完整会话归属。:param origin: 精确来源。:param id: 消费租约。:param aliases: 已独立验证的非消费别名。:returns: 只在同实际流内选择仍有效来源，不接受跨流重绑定。</summary>
    public PreviewLeaseSnapshot HeartbeatShared(string owner, string origin, Guid id, IReadOnlyList<Guid> aliases)
    {
        ArgumentNullException.ThrowIfNull(aliases);
        lock (_sync)
        {
            var lease = Owned(owner, origin, id);
            if (aliases.Count is < 1 or > 64 || aliases.Distinct().Count() != aliases.Count || aliases.Contains(id))
                throw new PreviewException("预览别名列表无效", "invalid_preview_aliases", 400);
            var verified = aliases.Select(alias => Owned(owner, origin, alias)).ToArray();
            if (verified.Any(alias => alias.Feed is not null || alias.Source.Key != lease.Source.Key))
                throw new PreviewException("预览别名不属于同一实际流", "invalid_preview_aliases", 400);
            lease.Source = verified[0].Source; lease.ExpiresAt = clock.GetUtcNow().AddSeconds(options.LeaseSeconds);
            return Snapshot(lease);
        }
    }

    /// <summary>:param owner: 当前身份。:param origin: 精确来源。:param id: 已创建租约。:returns: 单消费者有界队列；并发同流只有一份生产者。</summary>
    public PreviewSubscription Open(string owner, string origin, Guid id)
    {
        lock (_sync)
        {
            var lease = Owned(owner, origin, id);
            if (lease.Feed is not null) throw new PreviewException("此预览已经在使用", "preview_already_consumed", 409);
            if (!_feeds.TryGetValue(lease.Source.Key, out var feed))
            {
                if (_feeds.Count >= options.MaximumFeeds) throw new PreviewException("预览上游数量已达上限", "preview_capacity", 429);
                feed = new(lease.Source, producer, budget, clock); _feeds.Add(lease.Source.Key, feed);
            }
            budget.UpdateDemand(_leases.Values.Count(item => item.Feed is not null) + 1, false, false);
            ChannelReader<PreviewGop> reader;
            try { reader = feed.Attach(lease.Id, lease.Source.Quality == "high"); }
            catch { UpdateDemand(); throw; }
            lease.Feed = feed; UpdateDemand();
            return new(reader, () => Disconnect(lease.Id), lease.Lifetime.Token);
        }
    }

    /// <summary>:param owner: 本次身份。:param origin: 精确来源。:param id: 租约。:returns: 仅释放本人租约，取消该消费连接。</summary>
    public void Delete(string owner, string origin, Guid id)
    {
        Lease lease;
        lock (_sync) { lease = Owned(owner, origin, id); Remove(lease); }
        Cancel(lease);
    }

    /// <summary>:param formalPlaybackBusy: 正式流播放活跃。:param hostBusy: NIC尽力观测拥塞。:returns: 回收失联与空闲流；清理期间同键保持占位，不重复启动上游。</summary>
    public async Task SweepAsync(bool formalPlaybackBusy = false, bool hostBusy = false)
    {
        List<SharedPreviewFeed> closing;
        Lease[] expired;
        lock (_sync)
        {
            expired = _leases.Values.Where(item => item.ExpiresAt <= clock.GetUtcNow()).ToArray();
            foreach (var lease in expired) Remove(lease);
            budget.UpdateDemand(_leases.Values.Count(item => item.Feed is not null), formalPlaybackBusy, hostBusy);
            closing = _feeds.Values.Where(feed => feed.TryBeginClose(TimeSpan.FromSeconds(options.IdleGraceSeconds))).ToList();
        }
        foreach (var lease in expired) Cancel(lease);
        await Task.WhenAll(closing.Select(CloseAsync)).ConfigureAwait(false);

        async Task CloseAsync(SharedPreviewFeed feed)
        {
            await feed.DisposeAsync().ConfigureAwait(false);
            lock (_sync)
                if (_feeds.TryGetValue(feed.Key, out var current) && ReferenceEquals(current, feed)) _feeds.Remove(feed.Key);
        }
    }

    private Lease Owned(string owner, string origin, Guid id)
    {
        if (_stopping || !_leases.TryGetValue(id, out var lease) || lease.Owner != owner || lease.Origin != origin)
            throw new PreviewException("预览会话不存在或已失效", "preview_lease_missing", 404);
        if (lease.ExpiresAt <= clock.GetUtcNow()) throw new PreviewException("预览会话已过期", "preview_lease_expired", 410);
        return lease;
    }

    private static PreviewLeaseSnapshot Snapshot(Lease lease) => new(lease.Id, lease.ExpiresAt, lease.Feed is not null, lease.Feed?.Status ?? "idle");
    private void UpdateDemand() => budget.UpdateDemand(_leases.Values.Count(item => item.Feed is not null), false, false);
    private void Disconnect(Guid id)
    {
        Lease? removed = null;
        lock (_sync) if (_leases.TryGetValue(id, out var lease)) { Remove(lease); removed = lease; }
        if (removed is not null) Cancel(removed);
    }
    private void Remove(Lease lease)
    {
        _leases.Remove(lease.Id); lease.Feed?.Detach(lease.Id); UpdateDemand();
    }
    private static void Cancel(Lease lease) { try { lease.Lifetime.Cancel(); } finally { lease.Lifetime.Dispose(); } }

    /// <summary>:returns: 不接受新请求，取消全部消费者并等待各自生产者清理，保持退出不确定异常。</summary>
    public async ValueTask DisposeAsync()
    {
        SharedPreviewFeed[] feeds;
        Lease[] leases;
        lock (_sync)
        {
            _stopping = true;
            leases = _leases.Values.ToArray();
            foreach (var lease in leases) Remove(lease);
            feeds = _feeds.Values.ToArray();
        }
        foreach (var lease in leases) Cancel(lease);
        await Task.WhenAll(feeds.Select(feed => feed.DisposeAsync().AsTask())).ConfigureAwait(false);
        lock (_sync) _feeds.Clear();
    }

    private sealed class Lease(Guid id, string owner, string origin, PreviewSource source, DateTimeOffset expiresAt)
    {
        public Guid Id { get; } = id;
        public string Owner { get; } = owner;
        public string Origin { get; } = origin;
        public PreviewSource Source { get; set; } = source;
        public DateTimeOffset ExpiresAt { get; set; } = expiresAt;
        public SharedPreviewFeed? Feed { get; set; }
        public CancellationTokenSource Lifetime { get; } = new();
    }
}

public sealed class PreviewSubscription(ChannelReader<PreviewGop> reader, Action disconnect, CancellationToken lifetime) : IDisposable
{
    private int _disposed;
    public ChannelReader<PreviewGop> Reader { get; } = reader;
    public CancellationToken Lifetime { get; } = lifetime;
    public void Dispose() { if (Interlocked.Exchange(ref _disposed, 1) == 0) disconnect(); }
}
