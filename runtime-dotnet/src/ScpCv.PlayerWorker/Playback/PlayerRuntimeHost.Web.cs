// 普通网页保温与阶段超时独立维护；屏幕共享不使用此资源池。
using System.IO;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using ScpCv.PlayerWorker.Audio;

namespace ScpCv.PlayerWorker.Playback;

public sealed partial class PlayerRuntimeHost
{
    /// <summary>:param sourceId: 普通网页媒体ID。:param sourceRevision: 源修订。:param uri: 网页地址。:param cancellationToken: OPEN寿命。:returns: 按普通网页规则保温的资源；不得用于屏幕共享。</summary>
    private async Task<SurfaceResource> OpenWebAsync(
        long sourceId,
        long sourceRevision,
        string uri,
        CancellationToken cancellationToken)
    {
        var key = $"{sourceId}:{sourceRevision}:{uri}";
        if (_warmWebResources.TryGetValue(key, out var existing))
        {
            if (existing.IsHealthy) return existing;
            _warmWebResources.Remove(key);
            await existing.DisposeAsync();
        }
        var control = new WebView2();
        _window.PrepareSurface(control);
        try
        {
            var userData = WebViewProfilePaths.For(_windowId, shared: false, webViewUserDataRoot);
            Directory.CreateDirectory(userData);
            var browser = WebViewAudioOutputBinding.Browser(_windowId, audioOutputDeviceId);
            var environment = await WaitForWebStageAsync(
                CoreWebView2Environment.CreateAsync(browserExecutableFolder: browser, userDataFolder: userData), "环境创建", cancellationToken);
            WebViewProfilePaths.Verify(webViewUserDataRoot, userData, environment.UserDataFolder);
            await WaitForWebStageAsync(control.EnsureCoreWebView2Async(environment), "控件初始化", cancellationToken);
            control.CoreWebView2.IsMuted = true; // 候选和保温页不出声，只有切入当前输出才解除。
            await WebViewAudioOutputBinding.PrepareAsync(control.CoreWebView2, environment, browser, audioOutputDeviceId, cancellationToken);
            control.CoreWebView2.Settings.AreDevToolsEnabled = false;
            control.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
            control.CoreWebView2.PermissionRequested += (_, args) => args.State = CoreWebView2PermissionState.Deny;
            var navigated = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var healthy = true;
            void Completed(object? _, CoreWebView2NavigationCompletedEventArgs args)
            {
                if (args.IsSuccess) navigated.TrySetResult();
                else navigated.TrySetException(new InvalidOperationException($"WebView2 导航失败：{args.WebErrorStatus}"));
            }
            void Failed(object? _, CoreWebView2ProcessFailedEventArgs __) => healthy = false;
            control.NavigationCompleted += Completed;
            control.CoreWebView2.ProcessFailed += Failed;
            control.Source = new Uri(uri, UriKind.Absolute);
            await WaitForWebStageAsync(navigated.Task, "页面导航", cancellationToken);
            control.NavigationCompleted -= Completed;
            var resource = new SurfaceResource("web", control, async () =>
            {
                control.NavigationCompleted -= Completed;
                control.CoreWebView2.ProcessFailed -= Failed;
                control.Dispose();
                await ValueTask.CompletedTask;
            }, health: () => healthy, audible: enabled => control.CoreWebView2.IsMuted = !enabled);
            _warmWebResources[key] = resource;
            return resource;
        }
        catch
        {
            _window.RemovePendingSurface(control);
            control.Dispose();
            throw;
        }
    }

    /// <summary>:param operation: 普通网页预备。:param stage: 阶段说明。:param cancellationToken: OPEN寿命。:returns: 此阶段有界结果，不是共享真实视频帧证明。</summary>
    private static async Task<T> WaitForWebStageAsync<T>(
        Task<T> operation,
        string stage,
        CancellationToken cancellationToken)
    {
        try
        {
            return await operation.WaitAsync(WebViewStageTimeout, cancellationToken);
        }
        catch (TimeoutException exception)
        {
            throw new TimeoutException($"WebView2 {stage}超时。", exception);
        }
    }

    /// <summary>:param operation: 普通网页预备。:param stage: 阶段说明。:param cancellationToken: OPEN寿命。:returns: 此阶段有界完成；共享另用端到端预算。</summary>
    private static async Task WaitForWebStageAsync(
        Task operation,
        string stage,
        CancellationToken cancellationToken)
    {
        try
        {
            await operation.WaitAsync(WebViewStageTimeout, cancellationToken);
        }
        catch (TimeoutException exception)
        {
            throw new TimeoutException($"WebView2 {stage}超时。", exception);
        }
    }
}
