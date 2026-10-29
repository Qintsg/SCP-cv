// 文件描述：管理播放器窗口落位、置顶状态及可见视觉树。项目：SCP-cv；文件：PlayerWindow.xaml.cs；作者：Qintsg；日期：2026-10-29。
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace ScpCv.PlayerWorker;

/// <summary>单实例 PlayerWorker 的无边框输出窗口；显示器坐标由 Supervisor 提供。</summary>
public partial class PlayerWindow : Window
{
    private (int X, int Y, int Width, int Height)? _pendingBounds;
    /// <summary>实际目标显示器物理像素，仅用于渲染尺寸，不修改系统显示设置。</summary>
    public (int Width, int Height) OutputPixelSize => _pendingBounds is { } bounds ? (bounds.Width, bounds.Height) : (3840, 2160);

    public PlayerWindow()
    {
        InitializeComponent();
        SourceInitialized += (_, _) =>
        {
            ApplyPerMonitorDpiAwareness();
            ApplyPendingBounds();
        };
        Loaded += (_, _) => ApplyPendingBounds();
    }

    /// <summary>
    /// 用物理像素把无边框窗口铺满目标显示器。WPF 的 Left/Top/Width/Height 是设备无关单位，
    /// 直接填入 <see cref="System.Windows.Forms.Screen"/> 的物理像素，在非 100% 缩放的显示器上
    /// 会留下未被窗口覆盖的桌面边条，因此这里改用 Win32 SetWindowPos。
    /// </summary>
    public void AssignBounds(int x, int y, int width, int height)
    {
        _pendingBounds = (x, y, width, height);
        ApplyPendingBounds();
    }

    private void ApplyPendingBounds()
    {
        if (_pendingBounds is not { } bounds) return;
        var handle = NativeHandle;
        if (handle == nint.Zero) return;
        if (!SetWindowPos(handle, Topmost ? HwndTopmost : HwndBottom, bounds.X, bounds.Y, bounds.Width, bounds.Height, SwpNoActivate))
            throw new Win32Exception(Marshal.GetLastWin32Error());
    }

    /// <summary>原生窗口是否处于置顶显示状态；尚未创建HWND时未知。</summary>
    public bool? WindowForeground => NativeHandle == 0 ? null : (GetWindowLong(NativeHandle, -20) & 0x0008) != 0;

    /// <summary>:param foreground: true恢复置顶，false移到其他窗口后方。保持资源、位置及焦点。</summary>
    public void SetWindowForeground(bool foreground)
    {
        Dispatcher.VerifyAccess();
        var handle = NativeHandle;
        if (handle == 0) throw new InvalidOperationException("播放器窗口尚未创建。");
        Topmost = foreground;
        if (!SetWindowPos(handle, foreground ? HwndTopmost : HwndBottom, 0, 0, 0, 0, SwpNoActivate | 0x0001 | 0x0002))
            throw new Win32Exception(Marshal.GetLastWin32Error());
        if (WindowForeground != foreground) throw new InvalidOperationException("播放器窗口置顶状态未生效。");
    }

    private const uint SwpNoActivate = 0x0010;
    private static readonly nint HwndTopmost = new(-1);
    private static readonly nint HwndBottom = new(1);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(nint hWnd, nint hWndInsertAfter, int x, int y, int cx, int cy, uint flags);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongW")]
    private static extern int GetWindowLong(nint window, int index);

    public void SetSurface(FrameworkElement surface)
    {
        ArgumentNullException.ThrowIfNull(surface);
        if (!SurfaceHost.Children.Contains(surface))
        {
            SurfaceHost.Children.Clear();
            SurfaceHost.Children.Add(surface);
            return;
        }

        for (var index = SurfaceHost.Children.Count - 1; index >= 0; index--)
        {
            if (!ReferenceEquals(SurfaceHost.Children[index], surface))
                SurfaceHost.Children.RemoveAt(index);
        }
    }

    /// <summary>将待切入画面放在当前画面后面，使 WebView2 能在已加载的视觉树内初始化。</summary>
    public void PrepareSurface(FrameworkElement surface)
    {
        ArgumentNullException.ThrowIfNull(surface);
        if (!SurfaceHost.Children.Contains(surface)) SurfaceHost.Children.Insert(0, surface);
    }

    /// <summary>预备失败时移除尚未切入的画面，保留当前画面。</summary>
    public void RemovePendingSurface(FrameworkElement surface)
    {
        ArgumentNullException.ThrowIfNull(surface);
        SurfaceHost.Children.Remove(surface);
    }

    public nint NativeHandle => new WindowInteropHelper(this).Handle;

    public int NativeDpi => NativeHandle == 0 ? 0 : checked((int)GetDpiForWindow(NativeHandle));

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(nint hWnd);

    private static void ApplyPerMonitorDpiAwareness()
    {
        // WPF 使用系统 DPI；真实混合 DPI 映射由 Supervisor 的拓扑快照和 AttachSurface 再校验。
    }
}
