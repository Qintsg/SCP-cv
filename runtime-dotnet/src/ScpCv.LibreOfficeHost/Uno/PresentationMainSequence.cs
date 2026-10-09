// owner预绑定主序、原末组及自有空尾；保持时只移除自有节点，不按属性猜测删除用户动画。
using unoidl.com.sun.star.animations;

namespace ScpCv.LibreOfficeHost.Uno;

internal sealed class PresentationMainSequence
{
    private readonly XTimeContainer _container;
    private readonly XAnimationNode _tail;
    private bool _held;

    /// <summary>:param main: 原主序。:param container: 同一顺序容器。:param last: 原始最后直属组。:param tail: 仅本宿主追加的空尾。</summary>
    internal PresentationMainSequence(XAnimationNode main, XTimeContainer container, XAnimationNode last, XAnimationNode tail)
    { Main=main; _container=container; Last=last; _tail=tail; }
    internal XAnimationNode Main { get; }
    internal XAnimationNode Last { get; }

    /// <summary>:returns: 在实际末页模型保持活动，原有叶子/重复属性不删不改；随后必须取得真实重显示。</summary>
    internal void Hold()
    {
        if (_held) return;
        _container.removeChild(_tail);
        Main.End=new uno.Any(typeof(Event),new Event { Trigger=EventTrigger.NONE });
        _held=true;
    }
}
