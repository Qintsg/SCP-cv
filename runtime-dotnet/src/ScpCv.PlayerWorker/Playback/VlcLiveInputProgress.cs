// 直播输入推进策略：纯计数与单调时间，不能把重连中的原生Playing永远投影为成功。
using LibVLCSharp.Shared;

namespace ScpCv.PlayerWorker.Playback;

public readonly record struct VlcInputCounters(int ReadBytes, int DemuxReadBytes, int DecodedVideo, int DisplayedPictures);

public sealed class VlcLiveInputProgress
{
    private const long StallMilliseconds = 5000;
    private VlcInputCounters _previous;
    private long _lastProgress;
    private bool _active;
    /// <summary>:param counters: 当前输入/解码/呈现累计计数。:param native: 原生状态。:param desired: 已执行用户意图。:param nowMs: 单调毫秒。:returns: 直播Playing是否缺少已确认的数据推进。</summary>
    public bool Observe(VlcInputCounters counters, VLCState native, string desired, long nowMs)
    {
        if (native != VLCState.Playing || desired is "paused" or "stopped")
        {
            _active = false;
            return false;
        }
        if (!_active || counters != _previous)
        {
            _active = true;
            _previous = counters;
            _lastProgress = nowMs;
            return false;
        }
        return nowMs - _lastProgress >= StallMilliseconds;
    }
}
