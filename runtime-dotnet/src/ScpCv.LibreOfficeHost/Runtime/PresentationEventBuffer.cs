// 文稿事件的有限纯值诊断缓冲；回调零等待，争用/溢出明确丢失，不表示动画完成。
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace ScpCv.LibreOfficeHost.Runtime;

internal sealed class PresentationEventBuffer
{
    private readonly object _gate = new();
    private readonly Queue<PresentationEventEntry> _entries;
    private readonly int _capacity;
    private long _sequence;
    private long _dropped;

    /// <summary>:param capacity: 固定容量1..128，默认128。:returns: 不含UNO/IO的诊断实例。</summary>
    internal PresentationEventBuffer(int capacity = 128)
    {
#if NET8_0_OR_GREATER
        ArgumentOutOfRangeException.ThrowIfLessThan(capacity, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(capacity, 128);
#else
        // 正式Framework宿主没有现代ThrowIf API，保留相同参数门禁。
        if (capacity < 1 || capacity > 128) throw new ArgumentOutOfRangeException(nameof(capacity));
#endif
        _entries = new Queue<PresentationEventEntry>(capacity);
        _capacity = capacity;
    }

    /// <summary>:param kind: 纯值事件种类。:param observedWindow: 观测窗口标签，不代表因果。:returns: 非等待接受；满额或争用时明确丢失且false。</summary>
    internal bool TryRecord(string kind, long observedWindow)
    {
        if (!Monitor.TryEnter(_gate))
        {
            Interlocked.Increment(ref _dropped);
            return false;
        }
        try
        {
            if (_entries.Count == _capacity)
            {
                Interlocked.Increment(ref _dropped);
                return false;
            }
            _entries.Enqueue(new PresentationEventEntry(++_sequence, Stopwatch.GetTimestamp(), kind, observedWindow));
            return true;
        }
        finally { Monitor.Exit(_gate); }
    }

    /// <summary>:returns: 主调用线程排空的固定纯值批次，不做完成判断或原生回调。</summary>
    internal PresentationEventBatch Drain()
    {
        lock (_gate)
        {
            var entries = _entries.ToArray();
            _entries.Clear();
            return new PresentationEventBatch(entries, Interlocked.Exchange(ref _dropped, 0));
        }
    }
}

internal sealed class PresentationEventEntry
{
    /// <summary>:param sequence: 实例记录序号。:param timestamp: Stopwatch单调ticks。:param kind: 事件种类。:param observedWindow: 观测标签，不是命令归属或完成授权。</summary>
    internal PresentationEventEntry(long sequence, long timestamp, string kind, long observedWindow)
    { Sequence = sequence; Timestamp = timestamp; Kind = kind; ObservedWindow = observedWindow; }
    internal long Sequence { get; }
    internal long Timestamp { get; }
    internal string Kind { get; }
    internal long ObservedWindow { get; }
}

internal sealed class PresentationEventBatch
{
    /// <summary>:param entries: 排空快照。:param dropped: 本批明确丢失数量。:returns: 只读纯值批次。</summary>
    internal PresentationEventBatch(PresentationEventEntry[] entries, long dropped)
    { Entries = Array.AsReadOnly(entries); Dropped = dropped; }
    internal IReadOnlyList<PresentationEventEntry> Entries { get; }
    internal long Dropped { get; }
}
