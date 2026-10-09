// 只读记录父客户区和正常Frame布局，区分显示裁切与原件背景；不调整窗口或放宽预算。
using System;
using System.Runtime.InteropServices;
using ScpCv.Presentation.Protocol;
using unoidl.com.sun.star.awt;
using unoidl.com.sun.star.frame;

namespace ScpCv.LibreOfficeHost.Uno;

internal static class PresentationGeometry
{
    private static bool Enabled => Environment.GetEnvironmentVariable("SCP_CV_PRESENTATION_DIAGNOSTICS") == "1";

    /// <summary>:param message: 不含凭据的原生边界信息。:returns: 只在明确本机诊断开关开启时写入stderr。</summary>
    internal static void Note(string message)
    {
        if (Enabled) Console.Error.WriteLine(message);
    }

    /// <summary>:param frame: 当前自有Frame。:param parent: 已核验父窗口。:param phase: 布局阶段。:returns: 只读尺寸诊断，测量失败不冒充布局正确。</summary>
    internal static void Trace(XFrame frame, PresentationParentWindow parent, string phase)
    {
        if (!Enabled) return;
        var container = frame.getContainerWindow().getPosSize();
        var component = frame.getComponentWindow()?.getPosSize();
        var client = GetClientRect(new IntPtr(parent.Handle), out var rectangle)
            ? rectangle.Right + "x" + rectangle.Bottom : "unavailable";
        Console.Error.WriteLine("presentation_geometry: phase=" + phase + ";parent=" + parent.Width + "x" + parent.Height +
            ";dpi=" + parent.Dpi + ";client=" + client + ";container=" + Format(container) +
            ";component=" + (component == null ? "missing" : Format(component)));
        EnumChildWindows(new IntPtr(parent.Handle), (window, _) =>
        {
            var name = new char[128];
            var count = GetClassName(window, name, name.Length);
            if (!GetWindowRect(window, out var rect) || !GetClientRect(window, out var bounds))
            {
                Console.Error.WriteLine("presentation_geometry_child: phase=" + phase + ";geometry=unavailable");
                return true;
            }
            Console.Error.WriteLine("presentation_geometry_child: phase=" + phase + ";class=" + (count > 0 ? new string(name, 0, count) : "unavailable") + ";bounds=" +
                rect.Left + "," + rect.Top + "," + (rect.Right - rect.Left) + "x" + (rect.Bottom - rect.Top) +
                ";client=" + bounds.Right + "x" + bounds.Bottom);
            return true;
        }, IntPtr.Zero);
    }

    /// <summary>:param value: UNO位置/尺寸。:returns: 精确整数，不从截图猜测像素。</summary>
    private static string Format(Rectangle value) => value.X + "," + value.Y + "," + value.Width + "x" + value.Height;

    [StructLayout(LayoutKind.Sequential)]
    private struct ClientRectangle { internal int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetClientRect(IntPtr window, out ClientRectangle bounds);
    private delegate bool ChildWindowCallback(IntPtr window, IntPtr parameter);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumChildWindows(IntPtr window, ChildWindowCallback callback, IntPtr parameter);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(IntPtr window, out ClientRectangle bounds);
    [DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
    private static extern int GetClassName(IntPtr window, [Out] char[] name, int maximum);
}
