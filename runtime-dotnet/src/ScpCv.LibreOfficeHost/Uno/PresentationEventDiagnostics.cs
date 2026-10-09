// 默认关闭的自有文稿事件诊断；回调零UNO重入/IO，事件窗口标签不是因果或完成回执。
using System;
using System.Diagnostics;
using System.Threading;
using ScpCv.LibreOfficeHost.Runtime;
using ScpCv.Presentation.Protocol;
using unoidl.com.sun.star.animations;
using unoidl.com.sun.star.lang;
using unoidl.com.sun.star.presentation;

namespace ScpCv.LibreOfficeHost.Uno;

internal sealed class PresentationEventDiagnostics : uno.util.WeakBase, XSlideShowListener
{
    private readonly PresentationEventBuffer _buffer = new();
    private readonly Guid _instance;
    private readonly long _documentEpoch;
    private readonly PresentationAnimationCatalog? _nodes;
    private long _window;
    private int _retired;
    private long _retiredDrops;

    /// <summary>:param scope: 自有文稿纯值身份，仅复制不可变诊断字段。</summary>
    internal PresentationEventDiagnostics(PresentationScope scope, PresentationAnimationCatalog? nodes = null)
    { _instance = scope.EngineInstanceId; _documentEpoch = scope.DocumentEpoch; _nodes = nodes; }

    /// <summary>:param request: 主线程当前请求。:returns: 当前观测窗口映射，不授予动画因果关系。</summary>
    internal void BeginWindow(PresentationRequest request)
    {
        var window = Interlocked.Increment(ref _window);
        PresentationGeometry.Note("presentation_event_window: instance=" + _instance + ";document=" + _documentEpoch +
            ";window=" + window + ";operation=" + request.Operation + ";operation_id=" + request.OperationId + ";ticks=" + Stopwatch.GetTimestamp());
    }

    /// <summary>:returns: 主调用返回的本机时刻，仅用于与事件时序比较，不是动画已结束。</summary>
    internal void MarkReturned() => PresentationGeometry.Note("presentation_event_returned: instance=" + _instance +
        ";document=" + _documentEpoch + ";window=" + Interlocked.Read(ref _window) + ";ticks=" + Stopwatch.GetTimestamp());

    /// <summary>:param phase: 主线程采样位置。:returns: 仅主调用线程排空后写日志，丢失明确统计。</summary>
    internal void Flush(string phase)
    {
        var batch = _buffer.Drain();
        PresentationGeometry.Note("presentation_event_batch: instance=" + _instance + ";document=" + _documentEpoch +
            ";phase=" + phase + ";count=" + batch.Entries.Count + ";dropped=" + batch.Dropped +
            ";retired_drops=" + Interlocked.Read(ref _retiredDrops) + ";frequency=" + Stopwatch.Frequency);
        foreach (var entry in batch.Entries)
            PresentationGeometry.Note("presentation_event: instance=" + _instance + ";document=" + _documentEpoch +
                ";sequence=" + entry.Sequence + ";ticks=" + entry.Timestamp + ";kind=" + entry.Kind + ";observed_window=" + entry.ObservedWindow);
    }

    /// <summary>:returns: 先关闭本listener采集；晚回调仅记旧实例丢失，不写新文稿。</summary>
    internal void Retire() => Interlocked.Exchange(ref _retired, 1);

    /// <summary>:param kind: 固定事件种类。:returns: 回调只尝试有界纯值记录，不等锁、读Node或写IO。</summary>
    private void Record(string kind)
    {
        if (Volatile.Read(ref _retired) != 0) { Interlocked.Increment(ref _retiredDrops); return; }
        _buffer.TryRecord(kind, Interlocked.Read(ref _window));
    }

    /// <summary>:returns: 暂停通知，不当媒体冻结证明。</summary>
    public void paused() => Record("paused");
    /// <summary>:returns: 恢复通知。</summary>
    public void resumed() => Record("resumed");
    /// <summary>:returns: 过渡开始通知，可能为同页重播。</summary>
    public void slideTransitionStarted() => Record("transition_started");
    /// <summary>:returns: 过渡结束通知，不当所有效果完成。</summary>
    public void slideTransitionEnded() => Record("transition_ended");
    /// <summary>:returns: 整页主序结束通知，不当单点击回执。</summary>
    public void slideAnimationsEnded() => Record("slide_animations_ended");
    /// <summary>:param reverse: 倒序结束标志。:returns: 固定事件，无UNO读回。</summary>
    public void slideEnded(bool reverse) => Record(reverse ? "slide_ended_reverse" : "slide_ended_forward");
    /// <summary>:param hyperLink: 不记录或访问URL。:returns: 仅事件种类。</summary>
    public void hyperLinkClicked(string hyperLink) => Record("hyperlink_clicked");
    /// <summary>:param node: 不保留/读取原生节点。:returns: 时间线开始通知。</summary>
    public void beginEvent(XAnimationNode node) => Record(NodeKind("begin", node));
    /// <summary>:param node: 不保留/读取原生节点。:returns: 时间线结束通知。</summary>
    public void endEvent(XAnimationNode node) => Record(NodeKind("end", node));
    /// <summary>:param node: 不保留/读取原生节点。:param repeat: 不当新点击。:returns: 重复通知。</summary>
    public void repeat(XAnimationNode node, int repeat) => Record("repeat");
    /// <summary>:param source: 不读原生source或重入移除。:returns: 销毁通知。</summary>
    public void disposing(EventObject source) => Record("disposing");

    /// <summary>:param kind: 固定事件。:param node: 不调用UNO属性，只查已冻结目录。:returns: 本地身份关联仍不授予因果/完成。</summary>
    private string NodeKind(string kind, object node) => _nodes is null ? kind : kind + ";node=" + _nodes.Label(node);
}
