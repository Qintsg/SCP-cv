// 当前 COM 对象的 HWND 读取入口，读取之外不创建、切换或搜索任何 Office 对象。
using PowerPoint = Microsoft.Office.Interop.PowerPoint;

namespace ScpCv.PowerPointHost.Interop;

/// <summary>读取调用方给定对象的 HWND；不枚举窗口，不推断进程所有权。</summary>
public static class PowerPointWindowHandleReader
{
    /// <summary>
    /// 读取给定 Application 的 HWND。
    /// :param application: 当前 STA 持有的同一 Application 对象。
    /// :returns: Office 返回的 32 位 HWND；不代表进程所有权已验证。
    /// </summary>
    public static int ReadApplication(object application) => ((PowerPoint._Application)application).HWND;

    /// <summary>
    /// 读取给定 Run 结果的 HWND。
    /// :param slideShowWindow: 本次 Presentation.Run 返回的同一窗口对象。
    /// :returns: Office 返回的 32 位 HWND；不搜索其他放映。
    /// </summary>
    public static int ReadSlideShow(object slideShowWindow) => ((PowerPoint.SlideShowWindow)slideShowWindow).HWND;
}
