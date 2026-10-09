// 正常Frame只读播出；结束通过Office主线程dispatch，避免跨线程原生窗口销毁锁等待。
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using ScpCv.Presentation.Protocol;
using unoidl.com.sun.star.awt;
using unoidl.com.sun.star.beans;
using unoidl.com.sun.star.container;
using unoidl.com.sun.star.drawing;
using unoidl.com.sun.star.frame;
using unoidl.com.sun.star.lang;
using unoidl.com.sun.star.presentation;
using unoidl.com.sun.star.util;
using static ScpCv.LibreOfficeHost.Uno.UnoConnection;

namespace ScpCv.LibreOfficeHost.Uno;

internal sealed partial class PresentationDocument : IDisposable
{
    private readonly UnoConnection _connection;
    private XComponent? _document;
    private XFrame? _frame;
    private XPresentation2? _presentation;
    private PresentationScope? _scope;
    private PresentationEventDiagnostics? _events;
    private XSlideShowController? _eventController;
    private PresentationEffectAdvance? _advance;
    private PresentationTerminalController? _terminal;

    /// <summary>:param connection: 唯一线程的官方连接。</summary>
    internal PresentationDocument(UnoConnection connection) => _connection = connection;

    /// <summary>:param request: 已验证OS/原件/租约的请求。:returns: 真实页码观测。</summary>
    internal PresentationObservation Open(PresentationRequest request)
    {
        Close();
        var package = new PresentationPackageReader(request.SourcePath);
        var parent = request.ParentWindow ?? throw new InvalidDataException("缺少父窗口");
        var toolkit = (XSystemChildFactory)_connection.Context.getServiceManager().createInstanceWithContext("com.sun.star.awt.Toolkit", _connection.Context);
        var window = (XWindow)toolkit.createSystemChild(new uno.Any(typeof(long), parent.Handle), Array.Empty<byte>(), 1);
        window.setPosSize(0, 0, parent.Width, parent.Height, 15);
        window.setVisible(true);
        _frame = (XFrame)_connection.Context.getServiceManager().createInstanceWithContext("com.sun.star.frame.Frame", _connection.Context);
        _frame.initialize(window);
        PresentationGeometry.Trace(_frame, parent, "initialized");
        _frame.setName("ScpCvPresentation");
        _connection.Desktop.getFrames().append(_frame);
        _document = ((XComponentLoader)_frame).loadComponentFromURL(new Uri(request.SourcePath).AbsoluteUri, "_self", 0,
            new[] { Property("Hidden", false), Property("ReadOnly", true), Property("MacroExecutionMode", (short)0), Property("UpdateDocMode", (short)0), RejectInteractiveRequest.Property });
        if (_document == null) throw new InvalidDataException("LibreOffice未返回文稿");
        PresentationGeometry.Trace(_frame, parent, "loaded");
        if (!((XStorable)_document).isReadonly()) throw new InvalidOperationException("LibreOffice没有只读加载");
        if (_document is not XPresentationSupplier) throw new InvalidDataException("源文件并非可放映文稿");
        package.ApplyLayout(_document);
        PresentationTextLayout.Apply(_document, request.FitText);
        _presentation = (XPresentation2)((XPresentationSupplier)_document).getPresentation();
        ((XPropertySet)_presentation).setPropertyValue("StartWithNavigator", new uno.Any(false));
        var layout = (XLayoutManager)((XPropertySet)_frame).getPropertyValue("LayoutManager").Value;
        layout.setVisible(false);
        PresentationGeometry.Trace(_frame, parent, "layout_hidden");
        RejectExternalMedia(_document);
        _advance = new PresentationEffectAdvance(_connection.Context, _document);
        _presentation.startWithArguments(new[] { Property("IsFullScreen", false), Property("IsAlwaysOnTop", false), Property("AllowAnimations", true), Property("IsEndless", false) });
        _scope = request.Scope;
        _terminal=new PresentationTerminalController(Controller(),_advance);
        _terminal.BeginPlaybackEpoch(false);
        if (Environment.GetEnvironmentVariable("SCP_CV_PRESENTATION_DIAGNOSTICS") == "1")
        {
            var nodes = Environment.GetEnvironmentVariable("SCP_CV_PRESENTATION_NODE_DIAGNOSTICS") == "1"
                ? new PresentationAnimationCatalog(_document) : null;
            _events = new PresentationEventDiagnostics(request.Scope, nodes);
            _eventController = Controller();
            _events.BeginWindow(request);
            _eventController.addSlideShowListener(_events);
        }
        GotoSlideOnUi(request.TargetSlide,_terminal.HasHeldAnimations);
        PresentationGeometry.Trace(_frame, parent, "slideshow_started");
        return Observe();
    }

    /// <summary>:param request: 当前文稿动作。:returns: 实际观测。</summary>
    internal PresentationObservation Control(PresentationRequest request)
    {
        PresentationGeometry.Note("presentation_native_enter: op=" + request.Operation + ";target=" + request.TargetSlide);
        if(request.Operation is "goto" or "previous_slide" or "previous_effect")
            _terminal?.BeginPlaybackEpoch(request.Operation=="previous_effect");
        _events?.BeginWindow(request);
        if (request.Operation != "observe" && _frame != null && request.ParentWindow != null)
        {
            PresentationGeometry.Note("presentation_resize_enter");
            _frame.getContainerWindow().setPosSize(0, 0, request.ParentWindow.Width, request.ParentWindow.Height, 15);
            PresentationGeometry.Note("presentation_resize_return");
            PresentationGeometry.Trace(_frame, request.ParentWindow, "control_resized");
        }
        PresentationGeometry.Note("presentation_controller_action_enter");
        switch (request.Operation)
        {
            case "close": return Close();
            case "goto": GotoSlideOnUi(request.TargetSlide,_terminal?.HasHeldAnimations==true); break;
            // 相邻整页与Player保持同一路径；不从MTA直调视频页销毁，也不在末页进入黑色结束屏。
            case "next_slide": GotoSlideOnUi(Math.Min(Controller().getSlideCount(), Controller().getCurrentSlideIndex() + 2)); break;
            case "previous_slide": GotoSlideOnUi(Math.Max(1, Controller().getCurrentSlideIndex())); break;
            case "next_effect": StepEffect(true); break;
            case "previous_effect": StepEffect(false); break;
            case "observe": break;
            default: throw new InvalidDataException("文稿动作未支持");
        }
        PresentationGeometry.Note("presentation_controller_action_return");
        _events?.MarkReturned();
        return Observe();
    }

    /// <summary>:returns: 实际页码；统一媒体时钟不可得，保持null。</summary>
    internal PresentationObservation Observe()
    {
        _terminal?.Drain();
        PresentationGeometry.Note("presentation_observe_enter");
        var current = Controller().getCurrentSlideIndex() + 1;
        PresentationGeometry.Note("presentation_current_page_return=" + current);
        var total = Controller().getSlideCount();
        PresentationGeometry.Note("presentation_total_pages_return=" + total);
        _events?.Flush("observe");
        return new PresentationObservation { Confirmed = true, SourceId = _scope?.SourceId, CurrentSlide = current, TotalSlides = total };
    }

    /// <summary>:returns: 已结束/关闭的无源观测。失败仍保留文稿占用。</summary>
    internal PresentationObservation Close()
    {
        _terminal?.Dispose();
        _terminal=null;
        if (_events is { } events)
        {
            events.Retire();
            events.Flush("before_close");
            // 只由MTA owner向捕获的旧controller移除；不在回调中重入，不跨新文稿复用。
            _eventController?.removeSlideShowListener(events);
            _events = null;
            _eventController = null;
        }
        if (_presentation != null && _frame != null && _presentation.getController() is { } running && running.isRunning())
        {
            var command = new URL { Complete = ".uno:PresentationEnd" };
            var transformer = (XURLTransformer)_connection.Context.getServiceManager().createInstanceWithContext("com.sun.star.util.URLTransformer", _connection.Context);
            if (!transformer.parseStrict(ref command)) throw new InvalidOperationException("无法解析结束放映命令");
            var dispatch = ((XDispatchProvider)_frame).queryDispatch(command, "", 0)
                ?? throw new InvalidOperationException("结束放映命令不可用");
            dispatch.dispatch(command, new[] { Property("SynchronMode", false) });
            var timer = Stopwatch.StartNew();
            while (true)
            {
                var controller = _presentation.getController();
                if (controller == null || !controller.isRunning()) break;
                if (timer.Elapsed.TotalSeconds >= 10) throw new TimeoutException("放映未在时限内结束");
                Thread.Sleep(50);
            }
        }
        if (_document != null) ((XCloseable)_document).close(true);
        _document = null;
        _presentation = null;
        _frame = null;
        _scope = null;
        _advance = null;
        return new PresentationObservation { Confirmed = true, DocumentClosed = true };
    }

    /// <summary>:returns: 当前真实控制器，缺失时失败。</summary>
    private XSlideShowController Controller() => _presentation?.getController() ?? throw new InvalidOperationException("没有当前放映");

    /// <summary>:param forward: 下一步/上一步。:returns: 无主序效果的页复用有界UI换页；有主序才推进原生效果，不吞刚打开后的点击。</summary>
    private void StepEffect(bool forward)
    {
        var controller = Controller();
        var current = controller.getCurrentSlideIndex();
        var advance = _advance ?? throw new InvalidOperationException("动画模型尚未绑定");
        if (!advance.HasMainEffects(controller.getSlideByIndex(current)) &&
            (forward || current == 0 || !advance.HasMainEffects(controller.getSlideByIndex(current - 1))))
        {
            GotoSlideOnUi(forward ? Math.Min(controller.getSlideCount(), current + 2) : Math.Max(1, current));
            return;
        }
        if (forward) controller.gotoNextEffect();
        else controller.gotoPreviousEffect();
    }

    /// <summary>:param target: 1起始页码。:returns: Office UI异步切页后真实页码匹配；受理不等同完成。</summary>
    private void GotoSlideOnUi(int target,bool forceReplay=false)
    {
        var controller = Controller();
        if (!forceReplay&&controller.getCurrentSlideIndex() + 1 == target) return;
        var transition=_terminal?.TransitionStarts??0;
        // 控制器页序可能不同于文稿页序（隐藏页/自定义放映），必须按当前放映列表定位。
        var page = controller.getSlideByIndex(target - 1);
        var bookmark = ((XNamed)page).getName();
        var command = new URL { Complete = ".uno:JumpToMark" };
        var transformer = (XURLTransformer)_connection.Context.getServiceManager().createInstanceWithContext("com.sun.star.util.URLTransformer", _connection.Context);
        if (!transformer.parseStrict(ref command)) throw new InvalidOperationException("无法解析切页命令");
        var dispatch = ((XDispatchProvider)(_frame ?? throw new InvalidOperationException("没有当前Frame"))).queryDispatch(command, "", 0)
            ?? throw new InvalidOperationException("切页命令不可用");
        dispatch.dispatch(command, new[] { Property("Bookmark", Uri.EscapeDataString(bookmark)), Property("SynchronMode", false) });
        var budget = Stopwatch.StartNew();
        while (Controller().getCurrentSlideIndex() + 1 != target||
            (forceReplay&&(_terminal?.TransitionStarts??0)<=transition))
        {
            if (budget.Elapsed.TotalSeconds >= 5) throw new TimeoutException("异步切页未在预算内取得实际页码");
            Thread.Sleep(10);
        }
    }

    /// <summary>:param document: 只读文稿。:returns: 拒绝自动外链媒体；递归检查组形状。</summary>
    private static void RejectExternalMedia(XComponent document)
    {
        var pages = ((XDrawPagesSupplier)document).getDrawPages();
        for (var page = 0; page < pages.getCount(); page++) CheckShapes((XShapes)pages.getByIndex(page).Value);
    }

    /// <summary>:param shapes: 页面或组。:returns: 外链媒体拒绝。</summary>
    private static void CheckShapes(XShapes shapes)
    {
        for (var index = 0; index < shapes.getCount(); index++)
        {
            var shape = (XShape)shapes.getByIndex(index).Value;
            if (shape is XShapes nested) CheckShapes(nested);
            if (!shape.getShapeType().EndsWith(".MediaShape", StringComparison.Ordinal)) continue;
            var media = (string)((XPropertySet)shape).getPropertyValue("MediaURL").Value;
            if (!media.StartsWith("vnd.sun.star.Package:", StringComparison.Ordinal)) throw new InvalidDataException("文稿含自动外链媒体");
        }
    }

    /// <summary>:returns: 自有文稿正常关闭，异常不吞掉。</summary>
    public void Dispose() => Close();
}
