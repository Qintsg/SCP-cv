// 每个候选文稿使用独立原生子窗口；未切入前保持父窗口隐藏，不覆盖当前画面。
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace ScpCv.PlayerWorker.Presentations;

internal sealed class PresentationViewport : IDisposable
{
    private readonly PlayerWindow _window;
    internal nint Handle { get; }
    internal int Width { get; private set; }
    internal int Height { get; private set; }
    private bool _disposed;

    /// <summary>:param window: 唯一播放输出窗口。:returns: 自有隐藏候选容器，尺寸为物理像素。</summary>
    internal PresentationViewport(PlayerWindow window)
    {
        _window = window;
        if (!GetClientRect(window.NativeHandle, out var bounds)) throw new Win32Exception();
        Width = Math.Max(1, bounds.Right); Height = Math.Max(1, bounds.Bottom);
        Handle = CreateWindowEx(0, "STATIC", "ScpCvPresentationViewport", 0x40000000 | 0x02000000 | 0x04000000,
            0, 0, Width, Height, window.NativeHandle, 0, 0, 0);
        if (Handle == 0) throw new Win32Exception();
        window.SizeChanged += Resize;
    }

    /// <summary>:returns: 只有打开及旧资源正常关闭成功后才显示候选窗口。</summary>
    internal void Activate()
    {
        // 输出窗口不承接编辑/保存快捷键；翻页和动画仅由已授权控制台命令触发。
        EnableWindow(Handle, false);
        if (!SetWindowPos(Handle, 0, 0, 0, Width, Height, 0x0010 | 0x0040)) throw new Win32Exception();
    }

    private void Resize(object sender, System.Windows.SizeChangedEventArgs args)
    {
        if (_disposed || !GetClientRect(_window.NativeHandle, out var bounds)) return;
        Width = Math.Max(1, bounds.Right); Height = Math.Max(1, bounds.Bottom);
        if (!SetWindowPos(Handle, 0, 0, 0, Width, Height, 0x0010 | 0x0004)) throw new Win32Exception();
    }

    /// <summary>:returns: 原生引擎退出后才销毁自有容器，释放布局订阅。</summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true; _window.SizeChanged -= Resize;
        if (!DestroyWindow(Handle)) throw new Win32Exception();
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Rectangle { internal int Left, Top, Right, Bottom; }
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern nint CreateWindowEx(uint extended, string className, string name, uint style,
        int x, int y, int width, int height, nint parent, nint menu, nint instance, nint parameter);
    [DllImport("user32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetClientRect(nint window, out Rectangle bounds);
    [DllImport("user32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(nint window, nint after, int x, int y, int width, int height, uint flags);
    [DllImport("user32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyWindow(nint window);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnableWindow(nint window, bool enable);
}
