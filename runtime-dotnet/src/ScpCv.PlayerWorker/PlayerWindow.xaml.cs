// 管理真实播放器窗口的画面落位及预备资源；预备资源必须先进入可见窗口的视觉树。
using System.Windows;
using System.Windows.Interop;

namespace ScpCv.PlayerWorker;

/// <summary>单实例 PlayerWorker 的无边框输出窗口；显示器坐标由 Supervisor 提供。</summary>
public partial class PlayerWindow : Window
{
    private (int X, int Y, int Width, int Height)? _pendingBounds;

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
        // HWND_TOPMOST(-1)：任务栏本身是置顶窗口，播放窗口必须同样置顶才能盖住它。
        _ = SetWindowPos(handle, HwndTopmost, bounds.X, bounds.Y, bounds.Width, bounds.Height, SwpNoActivate);
    }

    private const uint SwpNoActivate = 0x0010;
    private static readonly nint HwndTopmost = new(-1);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool SetWindowPos(nint hWnd, nint hWndInsertAfter, int x, int y, int cx, int cy, uint flags);

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
