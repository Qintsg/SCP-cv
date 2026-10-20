// 同一实际流只有一份生产者；新观看者带完整初始化加入，慢客户端仅跳过完整GOP。
using System.Threading.Channels;

namespace ScpCv.ControlHost.StreamPreview;

public sealed class SharedPreviewFeed : IAsyncDisposable
{
    private readonly object _sync = new();
    private readonly IPreviewProducer _producer;
    private readonly PreviewBandwidth _budget;
    private readonly TimeProvider _clock;
    private readonly CancellationTokenSource _lifetime = new();
    private readonly Dictionary<Guid, (Channel<PreviewGop> Queue, bool High)> _viewers = [];
    private readonly PreviewSource _source;
    private Task? _running;
    private Task? _closingTask;
    private PreviewGop? _latest;
    private DateTimeOffset _idleSince;
    private bool _closing;
    private string _status = "idle";
    private bool _cleanupUncertain;

    public string Key => _source.Key;
    public string Status { get { lock (_sync) return _status; } }

    public SharedPreviewFeed(PreviewSource source, IPreviewProducer producer, PreviewBandwidth budget, TimeProvider clock)
    { _source = source; _producer = producer; _budget = budget; _clock = clock; _idleSince = clock.GetUtcNow(); }

    /// <summary>:param id: 唯一消费连接。:param high: 输出质量意图。:returns: 独立两GOP队列；不等待慢观看者。</summary>
    public ChannelReader<PreviewGop> Attach(Guid id, bool high)
    {
        lock (_sync)
        {
            if (_closing) throw new PreviewException("预览正在释放，请稍后重试", "preview_cleanup_pending", 429);
            if (_running?.IsCompleted == true)
            {
                if (_cleanupUncertain || _running.IsFaulted) throw new PreviewException("原预览资源退出未确认", "preview_cleanup_pending", 429);
                if (_viewers.Count > 0) throw new PreviewException("原预览正在结束，请稍后重试", "preview_unavailable");
                _running = null; _latest = null; _status = "connecting";
            }
            var channel = Channel.CreateBounded<PreviewGop>(new BoundedChannelOptions(2)
            { FullMode = BoundedChannelFullMode.DropOldest, SingleReader = true, SingleWriter = false, AllowSynchronousContinuations = false });
            if (!_viewers.TryAdd(id, (channel, high))) throw new PreviewException("此预览已经在使用", "preview_already_consumed", 409);
            var allowed = _budget.SelectEncoding(high || _viewers.Values.Any(viewer => viewer.High));
            if (_latest is not null && (_latest.Encoding is null || _latest.Encoding.KilobitsPerSecond <= allowed.KilobitsPerSecond)) channel.Writer.TryWrite(_latest);
            _running ??= Task.Run(RunAsync);
            if (_running.IsCompleted) channel.Writer.TryComplete(new PreviewException("预览连接已结束，请重试", "preview_unavailable"));
            return channel.Reader;
        }
    }

    /// <summary>:param id: 原连接身份。:returns: 只关闭该队列，最后读者进入有限宽限。</summary>
    public void Detach(Guid id)
    {
        lock (_sync)
        {
            if (_viewers.Remove(id, out var viewer)) viewer.Queue.Writer.TryComplete();
            if (_viewers.Count == 0) _idleSince = _clock.GetUtcNow();
        }
    }

    /// <summary>:param grace: 无观看宽限。:returns: 原子进入关闭；进入后不能再认领，避免新旧生产者重叠。</summary>
    public bool TryBeginClose(TimeSpan grace)
    {
        lock (_sync)
        {
            if (_closing || _viewers.Count != 0 || _clock.GetUtcNow() - _idleSince < grace) return false;
            _closing = true; return true;
        }
    }

    private PreviewEncoding Encoding()
    { lock (_sync) return _budget.SelectEncoding(_viewers.Values.Any(viewer => viewer.High)); }

    private void Publish(PreviewGop gop)
    {
        lock (_sync)
        {
            if (_closing) return;
            if (gop.Bytes.Length > 2 * 1024 * 1024) throw new InvalidDataException("预览关键帧组超过安全上限");
            // 人数突增时不把旧高码率GOP塞给全部新读者，等待编码降档；只跳完整组，连接不关闭。
            if (gop.Encoding is not null && gop.Encoding.KilobitsPerSecond > _budget.SelectEncoding(_viewers.Values.Any(viewer => viewer.High)).KilobitsPerSecond) return;
            _latest = gop;
            foreach (var viewer in _viewers.Values)
            {
                if (viewer.Queue.Reader.CanCount && viewer.Queue.Reader.Count == 2) _budget.ObserveWrite(TimeSpan.FromSeconds(1));
                viewer.Queue.Writer.TryWrite(gop);
            }
        }
    }

    private async Task RunAsync()
    {
        try { await _producer.RunAsync(_source, Encoding, Publish, status => { lock (_sync) _status = status; }, _lifetime.Token).ConfigureAwait(false); }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested) { }
        catch (Exception error) when (error is PreviewException or IOException or System.ComponentModel.Win32Exception)
        { lock (_sync) { _status = "unavailable"; _cleanupUncertain = error is PreviewException { Code: "preview_cleanup_pending" }; } }
        finally
        {
            lock (_sync)
                foreach (var viewer in _viewers.Values) viewer.Queue.Writer.TryComplete();
        }
    }

    /// <summary>:returns: 等待本实例生产者退出；退出不确定时不宣称释放成功。</summary>
    public ValueTask DisposeAsync()
    { lock (_sync) { _closing = true; return new(_closingTask ??= Task.Run(StopAsync)); } }

    private async Task StopAsync()
    {
        Task? running;
        lock (_sync) { _closing = true; running = _running; }
        await _lifetime.CancelAsync().ConfigureAwait(false);
        if (running is not null) await running.ConfigureAwait(false);
        lock (_sync)
        {
            if (_cleanupUncertain) throw new PreviewException("预览资源退出尚未确认", "preview_cleanup_pending");
            foreach (var viewer in _viewers.Values) viewer.Queue.Writer.TryComplete();
            _viewers.Clear(); _latest = null;
        }
        _lifetime.Dispose();
    }
}
