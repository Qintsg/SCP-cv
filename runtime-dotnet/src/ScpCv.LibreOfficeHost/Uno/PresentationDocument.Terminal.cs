// 实际无后继与本轮动画耗尽共同确认末页；边界返回不执行播放动作，不能按页码猜测。
using ScpCv.Presentation.Protocol;

namespace ScpCv.LibreOfficeHost.Uno;

internal sealed partial class PresentationDocument
{
    /// <summary>:param request: 已授权前向动作。:returns: 无主序或本轮原末组已耗尽且实际没有后继时的只读观测；不resize/goto/推进效果。</summary>
    internal PresentationObservation? ObserveTerminalBoundary(PresentationRequest request)
    {
        if (request.Operation is not ("next_slide" or "next_effect") || _advance == null) return null;
        var controller = Controller();
        var page = controller.getSlideByIndex(controller.getCurrentSlideIndex());
        var next = controller.getNextSlideIndex();
        if (next >= 0 && next < controller.getSlideCount()) return null;
        var animated=_advance.HasMainEffects(page);
        if(animated&&_terminal?.IsExhausted(controller,page)!=true)return null;
        PresentationGeometry.Note("presentation_terminal_noop: no_main_effects=" + (!animated) + ";operation=" + request.Operation);
        return Observe();
    }
}
