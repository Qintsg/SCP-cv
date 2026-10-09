// 一次 VLC 播放尝试的单调事件标记；旧尝试只写旧对象，不污染同播放器的新尝试。
using LibVLCSharp.Shared;

namespace ScpCv.PlayerWorker.Playback;

public sealed class VlcPlaybackAttempt(long sourceGeneration, bool liveInput = false)
{
    private const int ErrorFlag = 1;
    private const int PlayingFlag = 2;
    private const int EndedFlag = 4;
    private int _signals;
    public long SourceGeneration { get; } = sourceGeneration;
    public bool HasError => (Volatile.Read(ref _signals) & ErrorFlag) != 0;
    public bool IsLiveInput { get; } = liveInput;
    private readonly VlcLiveInputProgress? _liveProgress = liveInput ? new() : null;

    /// <summary>:param counters: 本尝试的原生累计计数。:param native: 原生状态。:param desired: 已执行意图。:param nowMs: 单调时间。:returns: 不触发重连；缺推进的错误由显式重开创建新尝试。</summary>
    public void ObserveInputProgress(VlcInputCounters counters, VLCState native, string desired, long nowMs)
    {
        if (_liveProgress?.Observe(counters, native, desired, nowMs) == true) RecordError();
    }

    /// <summary>原生回调只记录错误；后来的 Playing/Ended 不清除该证据。</summary>
    public void RecordError() => Interlocked.Or(ref _signals, ErrorFlag);
    /// <summary>记录已开始播放，不在回调调用原生控制或 WPF。</summary>
    public void RecordPlaying() => Interlocked.Or(ref _signals, PlayingFlag);
    /// <summary>记录自然结束；是否重播由持有媒体门禁的宿主决定。</summary>
    public void RecordEnded() => Interlocked.Or(ref _signals, EndedFlag);

    /// <summary>结合本次事件、原生状态与已执行用户意图，不以时间零或 Play 返回值证明出画。</summary>
    /// <remarks>:param nativeState: 当前原生状态。:param desiredState: 已执行意图。:returns: 对外状态。</remarks>
    public string ResolveState(VLCState nativeState, string desiredState)
    {
        if (desiredState == "stopped") return "stopped";
        if (nativeState == VLCState.Error) RecordError();
        if (HasError || desiredState == "error") return "error";
        if (desiredState == "paused" || nativeState == VLCState.Paused) return "paused";
        var signals = Volatile.Read(ref _signals);
        if ((signals & EndedFlag) != 0 || nativeState == VLCState.Ended) return "stopped";
        if (nativeState is VLCState.Opening or VLCState.Buffering) return "loading";
        if (nativeState == VLCState.Playing || (signals & PlayingFlag) != 0) return "playing";
        return nativeState == VLCState.Stopped && desiredState != "loading" ? "stopped" : "loading";
    }
}
