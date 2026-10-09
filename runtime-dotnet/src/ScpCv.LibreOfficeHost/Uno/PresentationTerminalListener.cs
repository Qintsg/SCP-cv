// 不可变播放轮listener：回调只匹配冻结OID并写有限纯值缓冲，不读取UNO属性、不写IO或等锁。
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using ScpCv.LibreOfficeHost.Runtime;
using unoidl.com.sun.star.animations;
using unoidl.com.sun.star.lang;
using unoidl.com.sun.star.presentation;

namespace ScpCv.LibreOfficeHost.Uno;

internal sealed class PresentationTerminalListener : uno.util.WeakBase, XSlideShowListener
{
    private readonly PresentationEventBuffer _buffer=new(16);
    private readonly Dictionary<object,string> _begins=new();
    private readonly Dictionary<object,string> _ends=new();
    private readonly long _epoch;
    private long _transitions;
    private int _retired;

    /// <summary>:param epoch: 本listener不可变身份。:param rows: owner冻结的原主序/最后组；回调仅用官方CLI本地OID hash/equality。</summary>
    internal PresentationTerminalListener(long epoch, List<PresentationTerminalBinding> rows)
    {
        _epoch=epoch;
        foreach(var row in rows)
        {
            var key=row.Key.ToString(CultureInfo.InvariantCulture);
            _begins.Add(row.Sequence.Main,"M"+key); _ends.Add(row.Sequence.Main,"m"+key);
            _begins.Add(row.Sequence.Last,"L"+key); _ends.Add(row.Sequence.Last,"l"+key);
        }
    }
    internal long TransitionStarts=>Interlocked.Read(ref _transitions);
    internal PresentationEventBatch Drain()=>_buffer.Drain();
    internal void Retire()=>Interlocked.Exchange(ref _retired,1);

    /// <summary>:param kind: 已冻结纯值。:returns: 旧回调不进入新轮；丢失由owner明确处理。</summary>
    private void Record(string kind) { if(Volatile.Read(ref _retired)==0) _buffer.TryRecord(kind,_epoch); }
    public void slideTransitionStarted(){if(Volatile.Read(ref _retired)!=0)return;Interlocked.Increment(ref _transitions);Record("t");}
    public void beginEvent(XAnimationNode node){if(_begins.TryGetValue(node,out var kind))Record(kind);}
    public void endEvent(XAnimationNode node){if(_ends.TryGetValue(node,out var kind))Record(kind);}
    public void disposing(EventObject source)=>Record("x");
    public void paused() { }
    public void resumed() { }
    public void slideTransitionEnded() { }
    public void slideAnimationsEnded() { }
    public void slideEnded(bool reverse) { }
    public void hyperLinkClicked(string hyperLink) { }
    public void repeat(XAnimationNode node,int repeat) { }
}
