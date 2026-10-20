// 所有预览共享的有界出站预算与质量迟滞；不改变正式播放或假称系统级网络QoS。
namespace ScpCv.ControlHost.StreamPreview;

public sealed record PreviewEncoding(int Width, int Height, int FramesPerSecond, int KilobitsPerSecond)
{
    public static IReadOnlyList<PreviewEncoding> Levels { get; } = Array.AsReadOnly<PreviewEncoding>(
        [new(320, 180, 4, 96), new(640, 360, 8, 300), new(960, 540, 12, 650), new(1280, 720, 15, 1200)]);
}

public sealed class PreviewBandwidth : IDisposable
{
    public const int MaximumChunkBytes = 16 * 1024;
    private readonly PreviewOptions _options;
    private readonly TimeProvider _clock;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly object _demandGate = new();
    private double _tokens = MaximumChunkBytes;
    private long _last;
    private int _viewers = 1;
    private DateTimeOffset _pressureUntil;
    private bool _hostBusy, _playbackBusy;

    /// <summary>:param options: 独立预览预算。:param clock: 可替换时钟。:returns: 无网络访问的资源控制器。</summary>
    public PreviewBandwidth(PreviewOptions options, TimeProvider clock)
    { options.Validate(); _options = options; _clock = clock; _last = clock.GetTimestamp(); }

    public int CurrentKilobitsPerSecond
    {
        get
        {
            lock (_demandGate)
                return Math.Max(128, _options.EgressKilobitsPerSecond / (UnderPressure() ? 4 : 1));
        }
    }

    /// <summary>:param viewers: 真实消费连接数，不按媒体库数量分配。:param playbackBusy: 正式直播活跃。:param hostBusy: 主机NIC尽力观测拥塞。:returns: 降档立即、压力消失至少30秒才恢复。</summary>
    public void UpdateDemand(int viewers, bool playbackBusy, bool hostBusy)
    {
        lock (_demandGate)
        {
            _viewers = Math.Max(1, viewers); _playbackBusy = playbackBusy; _hostBusy = hostBusy;
            if (playbackBusy || hostBusy) _pressureUntil = _clock.GetUtcNow().AddSeconds(30);
        }
    }

    /// <summary>:param highRequested: 该共享流是否有高清意图。:returns: 不超过当前每观看者分摊预算的编码档；硬下限后仍由总限速和完整GOP追赶兜底。</summary>
    public PreviewEncoding SelectEncoding(bool highRequested)
    {
        lock (_demandGate)
        {
            var pressure = UnderPressure();
            var available = Math.Max(1, _options.EgressKilobitsPerSecond / (pressure ? 4 : 1)) / _viewers / 1.3;
            var ceiling = pressure || !highRequested ? 1 : PreviewEncoding.Levels.Count - 1;
            for (var index = ceiling; index >= 0; index--)
                if (PreviewEncoding.Levels[index].KilobitsPerSecond <= available) return PreviewEncoding.Levels[index];
            return PreviewEncoding.Levels[0];
        }
    }

    /// <summary>:param elapsed: 不含限速等待的单次实际写出耗时。:returns: 慢写只影响预览预算，30秒内保守恢复。</summary>
    public void ObserveWrite(TimeSpan elapsed)
    {
        if (elapsed < TimeSpan.FromMilliseconds(500)) return;
        lock (_demandGate) _pressureUntil = _clock.GetUtcNow().AddSeconds(30);
    }

    /// <summary>:param bytes: 至多16KiB的本次块。:param cancellationToken: 消费取消。:returns: 全部预览共用令牌桶，单次突发≤16KiB；取消不预占未来配额。</summary>
    public async Task WaitAsync(int bytes, CancellationToken cancellationToken)
    {
        if (bytes is < 1 or > MaximumChunkBytes) throw new ArgumentOutOfRangeException(nameof(bytes));
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            while (true)
            {
                var now = _clock.GetTimestamp(); var rate = CurrentKilobitsPerSecond * 1000d / 8;
                _tokens = Math.Min(MaximumChunkBytes, _tokens + _clock.GetElapsedTime(_last, now).TotalSeconds * rate); _last = now;
                if (_tokens >= bytes) { _tokens -= bytes; return; }
                await Task.Delay(TimeSpan.FromSeconds((bytes - _tokens) / rate), _clock, cancellationToken).ConfigureAwait(false);
            }
        }
        finally { _gate.Release(); }
    }

    private bool UnderPressure() => _hostBusy || _playbackBusy || _clock.GetUtcNow() < _pressureUntil;
    public void Dispose() => _gate.Dispose();
}
