// 自动播放/转换不显示密码、修复、保存或外链确认对话框；只选择终止。
using unoidl.com.sun.star.beans;
using unoidl.com.sun.star.task;
using System.Threading;

namespace ScpCv.LibreOfficeHost.Uno;

internal sealed class RejectInteractiveRequest : XInteractionHandler
{
    private int _aborted;
    internal bool Aborted => Volatile.Read(ref _aborted) != 0;
    /// <summary>:param request: 引擎请求的交互。:returns: 仅终止，不批准修复/保存/网络副作用。</summary>
    public void handle(XInteractionRequest request)
    {
        foreach (var continuation in request.getContinuations())
            if (continuation is XInteractionAbort abort) { abort.select(); Interlocked.Exchange(ref _aborted, 1); return; }
    }

    internal PropertyValue Descriptor => new PropertyValue
    {
        Name = "InteractionHandler", Value = new uno.Any(typeof(XInteractionHandler), this),
    };
    internal static PropertyValue Property => new RejectInteractiveRequest().Descriptor;
}
