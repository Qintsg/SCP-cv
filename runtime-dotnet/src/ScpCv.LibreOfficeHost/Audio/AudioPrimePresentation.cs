// 仅自有内存文稿及有限PCM零音；正常Frame承载，不打开用户原件或循环旧默认声音流。
using System;
using System.Diagnostics;
using System.Threading;
using ScpCv.LibreOfficeHost.Uno;
using ScpCv.Presentation.Protocol;
using unoidl.com.sun.star.awt;
using unoidl.com.sun.star.beans;
using unoidl.com.sun.star.drawing;
using unoidl.com.sun.star.frame;
using unoidl.com.sun.star.lang;
using unoidl.com.sun.star.presentation;
using unoidl.com.sun.star.util;
using static ScpCv.LibreOfficeHost.Uno.UnoConnection;

namespace ScpCv.LibreOfficeHost.Audio;

internal sealed class AudioPrimePresentation(UnoConnection connection) : IDisposable
{
    private XComponent? _document;
    private XFrame? _frame;
    private XPresentation2? _presentation;

    /// <summary>:param parent: 已由OS核对的隐藏候选容器。:param silence: 本引擎profile内有限零音。:returns: 真实GUI声音登记，不加载用户内容。</summary>
    internal void Open(PresentationParentWindow parent, string silence)
    {
        var services = connection.Context.getServiceManager();
        var toolkit = (XSystemChildFactory)services.createInstanceWithContext("com.sun.star.awt.Toolkit", connection.Context);
        var window = (XWindow)toolkit.createSystemChild(new uno.Any(typeof(long), parent.Handle), Array.Empty<byte>(), 1);
        window.setPosSize(0, 0, 320, 180, 15); window.setVisible(true);
        _frame = (XFrame)services.createInstanceWithContext("com.sun.star.frame.Frame", connection.Context);
        _frame.initialize(window); connection.Desktop.getFrames().append(_frame);
        _document = ((XComponentLoader)_frame).loadComponentFromURL("private:factory/simpress", "_self", 0,
            new[] { Property("Hidden", false), Property("MacroExecutionMode", (short)0), Property("UpdateDocMode", (short)0) });
        if (_document is null) throw new InvalidOperationException("自有零音文稿未就绪。");
        var page = (XShapes)((XDrawPagesSupplier)_document).getDrawPages().getByIndex(0).Value;
        var shape = (XShape)((XMultiServiceFactory)_document).createInstance("com.sun.star.drawing.MediaShape");
        page.add(shape); shape.setSize(new Size { Width = 10000, Height = 10000 });
        var media = (XPropertySet)shape;
        media.setPropertyValue("MediaURL", new uno.Any(new Uri(silence).AbsoluteUri));
        media.setPropertyValue("Loop", new uno.Any(false));
        ((XModifiable)_document).setModified(false);
        _presentation = (XPresentation2)((XPresentationSupplier)_document).getPresentation();
        ((XPropertySet)_presentation).setPropertyValue("StartWithNavigator", new uno.Any(false));
        ((XLayoutManager)((XPropertySet)_frame).getPropertyValue("LayoutManager").Value).setVisible(false);
        _presentation.startWithArguments(new[] { Property("IsFullScreen", false), Property("IsAlwaysOnTop", false), Property("IsEndless", false) });
    }

    /// <summary>:returns: 零音自然结束后，在Office UI结束放映但保留内存文稿，避免引擎提前退出。</summary>
    internal void Quiesce()
    {
        if (_presentation is null || _frame is null || _presentation.getController() is not { } running || !running.isRunning()) return;
        var command = new URL { Complete = ".uno:PresentationEnd" };
        var transformer = (XURLTransformer)connection.Context.getServiceManager().createInstanceWithContext("com.sun.star.util.URLTransformer", connection.Context);
        if (!transformer.parseStrict(ref command)) throw new InvalidOperationException("零音结束命令无效。");
        var dispatch = ((XDispatchProvider)_frame).queryDispatch(command, "", 0)
            ?? throw new InvalidOperationException("零音结束命令不可用。");
        dispatch.dispatch(command, new[] { Property("SynchronMode", false) });
        var budget = Stopwatch.StartNew();
        while (_presentation.getController() is { } controller && controller.isRunning())
        {
            if (budget.Elapsed.TotalSeconds >= 5) throw new TimeoutException("零音放映结束未确认。");
            Thread.Sleep(50);
        }
    }

    /// <summary>:returns: 关闭只读用户文稿以外的自有内存文稿；失败不能抹掉原生占用。</summary>
    public void Dispose()
    {
        if (_document is null) return;
        var controller = _presentation?.getController();
        Console.Error.WriteLine("audio_primer_close: running=" + controller?.isRunning() + ";pages=" + controller?.getSlideCount());
        Quiesce(); ((XModifiable)_document).setModified(false); ((XCloseable)_document).close(true);
        RetireExitedEngine();
    }

    /// <summary>:returns: 原生正常退出已证明后本地退休引用，不再向死桥RPC。</summary>
    internal void RetireExitedEngine() { _document = null; _frame = null; _presentation = null; }
}
