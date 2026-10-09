// 实际页序与动画事实的owner桥接；只有实际无后继且本轮最后组完成才可授予无动作边界。
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using ScpCv.LibreOfficeHost.Runtime;
using unoidl.com.sun.star.beans;
using unoidl.com.sun.star.drawing;
using unoidl.com.sun.star.presentation;

namespace ScpCv.LibreOfficeHost.Uno;

internal sealed class PresentationTerminalBinding
{
    internal PresentationTerminalBinding(long key,XDrawPage page,PresentationMainSequence sequence)
    {Key=key;Page=page;Sequence=sequence;Evidence=new PresentationTerminalEvidence(1,key);}
    internal long Key {get;}
    internal XDrawPage Page {get;}
    internal PresentationMainSequence Sequence {get;}
    internal PresentationTerminalEvidence Evidence {get;set;}
}

internal sealed class PresentationTerminalController : IDisposable
{
    private readonly XSlideShowController _controller;
    private readonly List<PresentationTerminalBinding> _rows=new();
    private PresentationTerminalListener? _listener;
    private long _epoch;

    /// <summary>:param controller: 本文稿实际控制器。:param advance: 原主序目录。:returns: 按实际列表最后项/最后可见项绑定，不猜文稿编号。</summary>
    internal PresentationTerminalController(XSlideShowController controller,PresentationEffectAdvance advance)
    {
        _controller=controller;
        var count=controller.getSlideCount();
        if(count<=0||count>4096)throw new InvalidDataException("实际放映页序超出可支持预算");
        var pages=new List<XDrawPage>(count);var unique=new HashSet<object>();var lastVisible=-1;
        for(var index=0;index<count;index++)
        {
            var page=controller.getSlideByIndex(index);
            if(!unique.Add(page))throw new InvalidDataException("自定义放映重复引用同一页暂不支持动态导航；请使用不重复页序或手动选择静态逐页图片/PDF。原件未修改。");
            pages.Add(page);
            var value=((XPropertySet)page).getPropertyValue("Visible").Value;
            if(value is not bool visible)throw new InvalidDataException("实际放映页可见性无法确认");
            if(visible)lastVisible=index;
        }
        Add(count-1);
        if(lastVisible>=0&&lastVisible!=count-1)Add(lastVisible);
        foreach(var row in _rows)row.Sequence.Hold();
        void Add(int index)
        {
            var sequence=advance.MainSequence(pages[index]);
            if(sequence!=null)_rows.Add(new PresentationTerminalBinding(index+1,pages[index],sequence));
        }
    }
    internal bool HasHeldAnimations=>_rows.Count>0;
    internal long TransitionStarts=>_listener?.TransitionStarts??0;

    /// <summary>:param rewind: 同模型效果倒退才继承已观察活动主序；跳页/重开从未知开始。:returns: 先retire旧对象再移除/注册新对象，不给晚回调盖新轮身份。</summary>
    internal void BeginPlaybackEpoch(bool rewind)
    {
        Drain();
        if(_listener is { } previous){previous.Retire();_controller.removeSlideShowListener(previous);_listener=null;}
        _epoch=checked(_epoch+1);
        foreach(var row in _rows)row.Evidence=rewind?row.Evidence.RenewForRewind(_epoch):new PresentationTerminalEvidence(_epoch,row.Key);
        if(_rows.Count==0)return;
        _listener=new PresentationTerminalListener(_epoch,_rows);
        _controller.addSlideShowListener(_listener);
    }

    /// <summary>:returns: owner排空纯值；任意丢失撤销本轮证明，不用计数推断动画完成。</summary>
    internal void Drain()
    {
        if(_listener==null)return;
        var batch=_listener.Drain();
        if(batch.Dropped>0)foreach(var row in _rows)row.Evidence.Apply(_epoch,0,PresentationTerminalEvent.Lost);
        foreach(var entry in batch.Entries)
        {
            var kind=entry.Kind[0];
            var model=entry.Kind.Length==1?0:long.Parse(entry.Kind.Substring(1),CultureInfo.InvariantCulture);
            var value=kind switch{'t'=>PresentationTerminalEvent.Transition,'M'=>PresentationTerminalEvent.MainBegin,
                'm'=>PresentationTerminalEvent.MainEnd,'L'=>PresentationTerminalEvent.LastBegin,'l'=>PresentationTerminalEvent.LastEnd,
                _=>PresentationTerminalEvent.Lost};
            foreach(var row in _rows)row.Evidence.Apply(entry.ObservedWindow,model,value);
        }
    }

    /// <summary>:param controller: 当前真实控制器。:param page: 实际当前模型。:returns: 只有匹配的本轮耗尽事实；不会执行播放动作。</summary>
    internal bool IsExhausted(XSlideShowController controller,XDrawPage page)
    {
        if(!_controller.Equals(controller))throw new InvalidOperationException("放映控制器已变化，末页事实不能继续使用");
        Drain();
        foreach(var row in _rows)if(row.Page.Equals(page))return row.Evidence.Exhausted;
        return false;
    }

    /// <summary>:returns: owner先retire再移除，仅本listener；失败沿原生不确定路径，不伪造清理。</summary>
    public void Dispose()
    {
        if(_listener is not { } listener)return;
        listener.Retire();_controller.removeSlideShowListener(listener);_listener=null;
    }
}
