// 文件描述：当前网页绕过缓存刷新并等待实际导航结果。
// 项目：SCP-cv；文件：PlayerRuntimeHost.WebRefresh.cs；作者：Qintsg；日期：2026-10-30。
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using ScpCv.Contracts.Ipc;

namespace ScpCv.PlayerWorker.Playback;

public sealed partial class PlayerRuntimeHost
{
    /// <summary>:param lease: 当前来源代次。:param cancellationToken: 命令寿命。:returns: 当前控件完成实际无缓存导航。</summary>
    private async Task RefreshWebAsync(CommandLeaseDto lease, CancellationToken cancellationToken)
    {
        if (_current?.Kind != "web" || _current.Surface is not WebView2 control ||
            lease.SourceGeneration != _generation || lease.SourceRevision != _sourceRevision)
            throw new InvalidOperationException("当前网页已改变，无法刷新。");

        var core = control.CoreWebView2;
        await WaitForWebStageAsync(core.CallDevToolsProtocolMethodAsync("Network.enable", "{}"), "刷新网络初始化", cancellationToken);
        await WaitForWebStageAsync(core.CallDevToolsProtocolMethodAsync("Network.setCacheDisabled", "{\"cacheDisabled\":true}"), "禁用网页缓存", cancellationToken);
        await WaitForWebStageAsync(core.CallDevToolsProtocolMethodAsync("Network.setBypassServiceWorker", "{\"bypass\":true}"), "刷新缓存策略", cancellationToken);
        var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        ulong? navigation = null;
        void Started(object? sender, CoreWebView2NavigationStartingEventArgs args) => navigation ??= args.NavigationId;
        void Completed(object? sender, CoreWebView2NavigationCompletedEventArgs args)
        {
            if (args.NavigationId != navigation) return;
            if (args.IsSuccess && args.HttpStatusCode < 400) completed.TrySetResult();
            else completed.TrySetException(new InvalidOperationException($"网页刷新失败：{args.WebErrorStatus}（HTTP {args.HttpStatusCode}）。"));
        }
        core.NavigationStarting += Started;
        core.NavigationCompleted += Completed;
        try
        {
            await WaitForWebStageAsync(core.CallDevToolsProtocolMethodAsync("Page.reload", "{\"ignoreCache\":true}"), "刷新请求", cancellationToken);
            await WaitForWebStageAsync(completed.Task, "网页刷新", cancellationToken);
            _state = "playing";
            _errorMessage = string.Empty;
        }
        finally
        {
            core.NavigationStarting -= Started;
            core.NavigationCompleted -= Completed;
        }
    }
}
