// 每次共享独占WebView控制器与隐私profile；授权只用于本机接收，关闭不进入网页保温池。
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Windows;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace ScpCv.PlayerWorker.ScreenSharing;

internal sealed class ScreenShareSurface : IScreenShareSurface
{
    private readonly PlayerWindow _window;
    private readonly WebView2 _view;
    private bool _navigated;
    private Task? _disposal;
    private readonly Action? _released;
    public FrameworkElement Surface => _view;
    public ScreenSharePlaybackState State { get; }

    private ScreenShareSurface(PlayerWindow window, ScreenShareReceiverIdentity identity, Action? released)
    {
        _window = window; _released = released; State = new(identity); _view = new WebView2();
    }

    /// <summary>:param window: 所属Player窗口。:param identity: 已认证lease。:param token: 端到端OPEN寿命。:returns: 独立资源；首帧门禁仍由Host等待。</summary>
    internal static async Task<IScreenShareSurface> CreateAsync(PlayerWindow window, ScreenShareReceiverIdentity identity, CancellationToken token, Action? released = null)
    {
        token.ThrowIfCancellationRequested();
        var resource = new ScreenShareSurface(window, identity, released);
        window.PrepareSurface(resource.Surface);
        try
        {
            var userData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "SCP-cv", "WebView2", "screen-share", $"player-{identity.WindowId}");
            var environment = await CoreWebView2Environment.CreateAsync(userDataFolder: userData).WaitAsync(token);
            var options = environment.CreateCoreWebView2ControllerOptions();
            options.IsInPrivateModeEnabled = true; options.ProfileName = "share-" + identity.ShareId.ToString("N");
            await resource._view.EnsureCoreWebView2Async(environment, options).WaitAsync(token);
            token.ThrowIfCancellationRequested();
            resource.Configure();
            var request = environment.CreateWebResourceRequest(identity.Address.AbsoluteUri, "GET", null,
                "Authorization: " + identity.Authorization + "\r\n");
            // 首次文档也须cap授权；Fragment不进入HTTP，后续脚本fetch使用同一内存header。
            resource._view.CoreWebView2.NavigateWithWebResourceRequest(request);
            return resource;
        }
        catch (Exception error)
        {
            await resource.DisposeAsync();
            if (error is OperationCanceledException) throw;
            // SDK异常可能含导航URI，不能直接转发到公开状态或日志。
            throw new IOException("屏幕共享WebView初始化失败：" + error.GetType().Name);
        }
    }

    /// <summary>:returns: 外部导航、新窗、采集、自动填充及宿主对象均禁用；消息来源和schema另外校验。</summary>
    private void Configure()
    {
        var core = _view.CoreWebView2;
        core.Settings.AreDevToolsEnabled = false; core.Settings.AreDefaultContextMenusEnabled = false;
        core.Settings.AreBrowserAcceleratorKeysEnabled = false; core.Settings.AreHostObjectsAllowed = false;
        core.Settings.AreDefaultScriptDialogsEnabled = false; core.Settings.IsStatusBarEnabled = false;
        core.Settings.IsGeneralAutofillEnabled = false; core.Settings.IsPasswordAutosaveEnabled = false;
        core.NavigationStarting += OnNavigation; core.NewWindowRequested += OnNewWindow;
        core.PermissionRequested += OnPermission; core.WebMessageReceived += OnMessage;
        core.ProcessFailed += OnProcessFailed; core.NavigationCompleted += OnNavigationCompleted;
        core.WebResourceResponseReceived += OnResponse;
    }

    /// <summary>:param sender: 原生控件。:param args: 首次授权文档以外的导航必须拒绝，不允许接收页被替换后继续向Host发消息。</summary>
    private void OnNavigation(object? sender, CoreWebView2NavigationStartingEventArgs args)
    {
        args.Cancel = _navigated || args.Uri != State.Identity.Address.AbsoluteUri;
        if (!args.Cancel) _navigated = true;
    }

    /// <summary>:param sender: 原生控件。:param args: 接收端不能打开外部窗口。</summary>
    private static void OnNewWindow(object? sender, CoreWebView2NewWindowRequestedEventArgs args) => args.Handled = true;

    /// <summary>:param sender: 原生控件。:param args: 接收端不获取屏幕、摄像头或麦克风权限。</summary>
    private static void OnPermission(object? sender, CoreWebView2PermissionRequestedEventArgs args) => args.State = CoreWebView2PermissionState.Deny;

    /// <summary>:param sender: 原生控件。:param args: 仅当前接收文档的固定纯值消息，不信任页面传入的身份或帧计数类型。</summary>
    private void OnMessage(object? sender, CoreWebView2WebMessageReceivedEventArgs args)
    { State.Accept(args.Source, args.WebMessageAsJson); }

    /// <summary>:param sender: 原生控件。:param args: 导航成功不授予playing，失败解除首帧等待。</summary>
    private void OnNavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs args)
    { if (!args.IsSuccess) State.Fail("share_receiver_navigation_failed"); }

    /// <summary>:param sender: 原生控件。:param args: 404等文档仍可能NavigationCompleted成功；必须显式拒绝HTTP失败。</summary>
    private void OnResponse(object? sender, CoreWebView2WebResourceResponseReceivedEventArgs args)
    { if (State.Identity.IsDocument(args.Request.Uri) && args.Response.StatusCode >= 400) State.Fail("share_receiver_http_failed"); }

    /// <summary>:param sender: 原生控件。:param args: 原生进程故障不当作正常关闭，也不自动导航重试。</summary>
    private void OnProcessFailed(object? sender, CoreWebView2ProcessFailedEventArgs args) => State.Fail("share_receiver_process_failed");

    /// <summary>:param volume: 窗口音量。:param muted: 窗口静音。:param token: OPEN寿命。:returns: 明确激活后再核真实新帧。</summary>
    public async Task ActivateAsync(int volume, bool muted, CancellationToken token)
    {
        State.BeginActivation();
        var script = string.Create(CultureInfo.InvariantCulture, $"window.scpScreenShare.activate({Math.Clamp(volume, 0, 100)},{(muted ? "true" : "false")})");
        var result = await _view.CoreWebView2.ExecuteScriptAsync(script).WaitAsync(token);
        using var document = JsonDocument.Parse(result);
        if (document.RootElement.ValueKind != JsonValueKind.Object ||
            !document.RootElement.TryGetProperty("accepted", out var accepted) || accepted.ValueKind != JsonValueKind.True)
            throw new IOException("屏幕共享接收端未接受激活。");
        await State.WaitActivatedAsync(token);
        token.ThrowIfCancellationRequested();
    }

    /// <summary>:returns: 一次自有关闭终态；页面closed只是媒体清理报告，不代替控件Dispose完成。</summary>
    public ValueTask DisposeAsync() => new(_disposal ??= DisposeCoreAsync());

    /// <summary>:returns: 先退役消息/停peer和轨道，再关闭控制器；失败终态由同一Task保留，不假报释放。</summary>
    private async Task DisposeCoreAsync()
    {
        State.Retire();
        var core = _view.CoreWebView2;
        if (core is not null)
        {
            core.NavigationStarting -= OnNavigation; core.NewWindowRequested -= OnNewWindow;
            core.PermissionRequested -= OnPermission; core.WebMessageReceived -= OnMessage;
            core.ProcessFailed -= OnProcessFailed; core.NavigationCompleted -= OnNavigationCompleted;
            core.WebResourceResponseReceived -= OnResponse;
            try { await core.ExecuteScriptAsync("window.scpScreenShare?.stop()").WaitAsync(TimeSpan.FromSeconds(3)); }
            catch (Exception error) when (error is InvalidOperationException or ObjectDisposedException or TimeoutException or System.Runtime.InteropServices.COMException)
            { /* 原生故障/初始化未完成时脚本可能无法执行；最终控制器Dispose仍必须完成。 */ }
        }
        _view.Dispose();
        _window.RemovePendingSurface(Surface);
        _released?.Invoke();
    }
}
