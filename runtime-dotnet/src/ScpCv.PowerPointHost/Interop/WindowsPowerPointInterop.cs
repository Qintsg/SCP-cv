// 通过既有完整 PIA 的 Dual ABI 读取指定 COM 窗口，拒绝全局猜测 HWND。
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using PowerPoint = Microsoft.Office.Interop.PowerPoint;

namespace ScpCv.PowerPointHost.Interop;

/// <summary>PowerPoint COM 与 Win32 的真实外部 Adapter；只在 Office STA 上调用。</summary>
internal sealed class WindowsPowerPointInterop : IPowerPointInterop
{
    /// <summary>当前窗口取证失败的受控 JSON；STA 串行操作每次重新清空。</summary>
    public string FailureDetail { get; private set; } = string.Empty;

    /// <summary>
    /// 创建 PowerPoint COM Application。
    /// :returns: 外部 Application；缺少 Office 或创建失败时抛出异常。
    /// </summary>
    public object CreateApplication()
    {
        var applicationType = Type.GetTypeFromProgID("PowerPoint.Application")
            ?? throw new InvalidOperationException("未安装 PowerPoint COM Automation 类型。");
        return Activator.CreateInstance(applicationType)
            ?? throw new InvalidOperationException("无法创建 PowerPoint COM Application。");
    }

    /// <summary>
    /// 通过既有完整 PIA 的 Dual 接口读取当前 Application HWND，避免受限制的 IDispatch 成员。
    /// :param application: PowerPoint Application COM 对象。
    /// :returns: 当前精确进程身份；无法读取时返回 null。
    /// </summary>
    public PowerPointWindowEvidence? GetApplicationWindow(object application)
    {
        const string source = "application";
        FailureDetail = string.Empty;
        var stage = "com_interface";
        try
        {
            var applicationWindow = (PowerPoint._Application)application;
            stage = "com_hwnd";
            var rawHandle = PowerPointWindowHandleReader.ReadApplication(applicationWindow);
            WriteDiagnostic(source, stage, new { dispid = 2031, raw_hwnd = rawHandle });
            stage = "native_inspection";
            var evidence = InspectWindow(rawHandle, false, source);
            if (evidence is not null) FailureDetail = string.Empty;
            return evidence;
        }
        catch (Exception exception) when (IsUnavailable(exception))
        {
            WriteExceptionDiagnostic(source, stage, exception);
            return null;
        }
    }

    /// <summary>
    /// 通过既有完整 PIA 的 Dual 接口读取 Run 返回窗口，保持其完整 ABI 布局。
    /// :param slideShowWindow: 当前文稿 Run 返回的 COM 窗口。
    /// :returns: 可见放映窗口的精确进程身份；无法读取时返回 null。
    /// </summary>
    public PowerPointWindowEvidence? GetSlideShowWindow(object slideShowWindow)
    {
        const string source = "slideshow";
        FailureDetail = string.Empty;
        var stage = "com_interface";
        try
        {
            var currentWindow = (PowerPoint.SlideShowWindow)slideShowWindow;
            stage = "com_hwnd";
            var rawHandle = PowerPointWindowHandleReader.ReadSlideShow(currentWindow);
            WriteDiagnostic(source, stage, new { dispid = 2010, raw_hwnd = rawHandle });
            stage = "native_inspection";
            var evidence = InspectWindow(rawHandle, true, source);
            if (evidence is not null) FailureDetail = string.Empty;
            return evidence;
        }
        catch (Exception exception) when (IsUnavailable(exception))
        {
            WriteExceptionDiagnostic(source, stage, exception);
            return null;
        }
    }

    /// <summary>
    /// 联合检查指定 HWND、放映窗口类及当前进程启动时间。
    /// :param rawHandle: Office 返回的 32 位 HWND 位模式。
    /// :param isSlideShow: 是否要求为可见 screenClass 放映窗口。
    /// :param source: Application 或放映对象的固定诊断标签。
    /// :returns: 当前窗口证据；窗口或进程消失时返回 null。
    /// </summary>
    private PowerPointWindowEvidence? InspectWindow(int rawHandle, bool isSlideShow, string source)
    {
        // HWND 在 Office 类型库中是有符号 long；高位为 1 仍是有效的句柄位模式。
        var handle = (nint)unchecked((uint)rawHandle);
        var stage = "is_window";
        uint processId = 0;
        try
        {
            var isWindow = handle != 0 && IsWindow(handle);
            WriteDiagnostic(source, stage, new { raw_hwnd = rawHandle, hwnd = handle.ToInt64(), is_window = isWindow });
            if (!isWindow) return null;
            if (isSlideShow)
            {
                stage = "slideshow_visible";
                var isVisible = IsWindowVisible(handle);
                WriteDiagnostic(source, stage, new { hwnd = handle.ToInt64(), is_visible = isVisible });
                if (!isVisible) return null;
                stage = "window_class";
                var className = new char[64];
                var length = GetClassName(handle, className, className.Length);
                var isSlideShowClass = length > 0 && string.Equals(
                    new string(className, 0, length), "screenClass", StringComparison.Ordinal);
                WriteDiagnostic(source, stage, new { hwnd = handle.ToInt64(), class_length = length, is_slideshow_class = isSlideShowClass });
                if (!isSlideShowClass) return null;
            }
            stage = "window_process_id";
            var threadId = GetWindowThreadProcessId(handle, out processId);
            WriteDiagnostic(source, stage, new { hwnd = handle.ToInt64(), thread_id = threadId, process_id = processId });
            if (threadId == 0 || processId == 0) return null;
            stage = "process_lookup";
            using var process = Process.GetProcessById(checked((int)processId));
            stage = "process_has_exited";
            var hasExited = process.HasExited;
            WriteDiagnostic(source, stage, new { process_id = processId, has_exited = hasExited });
            if (hasExited) return null;
            stage = "process_start_time";
            var start = new DateTimeOffset(process.StartTime.ToUniversalTime(), TimeSpan.Zero);
            WriteDiagnostic(source, stage, new { process_id = processId, process_start = start });
            return new(handle, (int)processId, start);
        }
        catch (Exception exception) when (IsUnavailable(exception))
        {
            WriteDiagnostic(source, stage, new
            {
                raw_hwnd = rawHandle,
                hwnd = handle.ToInt64(),
                process_id = processId,
                exception_type = exception.GetType().FullName,
                hresult = $"0x{exception.HResult:X8}",
            });
            return null;
        }
    }

    /// <summary>
    /// 记录证据失败的阶段和异常编号，不输出可能包含用户文稿路径的异常消息。
    /// :param source: 固定外部对象标签。
    /// :param stage: 当前取证阶段。
    /// :param exception: 外部对象或进程查询异常。
    /// :returns: 无返回值；诊断不能改变安全门禁结果。
    /// </summary>
    private void WriteExceptionDiagnostic(string source, string stage, Exception exception) =>
        WriteDiagnostic(source, stage, new
        {
            exception_type = exception.GetType().FullName,
            hresult = $"0x{exception.HResult:X8}",
        });

    /// <summary>
    /// 输出不包含文稿内容的结构化取证；即使诊断输出不可写也不放宽或改变门禁。
    /// :param source: Application 或放映对象的固定标签。
    /// :param stage: 读取或校验证据的阶段。
    /// :param details: 只含 HWND、PID、时间或异常类型/编号的证据。
    /// :returns: 无返回值。
    /// </summary>
    private void WriteDiagnostic(string source, string stage, object details)
    {
        // 诊断必须随失败结果传回 IPC；隐藏工作进程的控制台不保证有可见接收方。
        FailureDetail = JsonSerializer.Serialize(new
        {
            component = "powerpoint_window",
            source,
            stage,
            details,
        });
        try
        {
            Console.Error.WriteLine(FailureDetail);
        }
        catch (Exception exception) when (exception is System.IO.IOException or ObjectDisposedException)
        {
            // 诊断失败不改变现有的 fail closed 结果或触发额外 Office 操作。
        }
    }

    /// <summary>
    /// 外部对象消失、拒绝访问或 Office 不暴露对应接口时按无法证明归属处理。
    /// :param exception: 外部互操作异常。
    /// :returns: 是否为可诊断的证据不可用。
    /// </summary>
    private static bool IsUnavailable(Exception exception) =>
        exception is COMException or InvalidCastException or ArgumentException or InvalidOperationException
            or System.ComponentModel.Win32Exception or OverflowException;

    [DllImport("user32.dll")] private static extern bool IsWindow(nint window);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(nint window);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(nint window, out uint processId);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassName(nint window, char[] className, int maximum);
}
