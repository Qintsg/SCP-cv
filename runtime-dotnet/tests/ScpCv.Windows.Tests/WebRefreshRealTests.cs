// 文件描述：真实WebView2、HTTP缓存和正式播放器刷新验证。
// 项目：SCP-cv；文件：WebRefreshRealTests.cs；作者：Qintsg；日期：2026-10-30。
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Web.WebView2.Wpf;
using ScpCv.Contracts.Ipc;
using ScpCv.PlayerWorker;
using ScpCv.PlayerWorker.Playback;

namespace ScpCv.Windows.Tests;

public sealed class WebRefreshRealTests
{
    /// <summary>:returns: 长缓存页面、脚本和数据均更新；控件、地址及Cookie保持，失败导航明确失败。</summary>
    [Fact]
    [Trait("Category", "Physical")]
    public async Task RefreshReloadsActualCachedPageScriptAndData()
    {
        using var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start(); var port = ((IPEndPoint)probe.LocalEndpoint).Port; probe.Stop();
        var origin = $"http://127.0.0.1:{port}/";
        using var listener = new HttpListener(); listener.Prefixes.Add(origin); listener.Start();
        using var ended = new CancellationTokenSource();
        var version = 1; var requests = 0;
        var server = Task.Run(async () =>
        {
            while (!ended.IsCancellationRequested)
            {
                var context = await listener.GetContextAsync().WaitAsync(ended.Token);
                Interlocked.Increment(ref requests);
                var current = Volatile.Read(ref version);
                var path = context.Request.Url!.AbsolutePath;
                context.Response.Headers["Cache-Control"] = "public,max-age=86400";
                context.Response.ContentType = path == "/app.js" ? "text/javascript" : path == "/data" ? "application/json" : "text/html";
                context.Response.StatusCode = current == 3 ? 500 : 200;
                var body = path switch
                {
                    "/app.js" => $"window.scriptVersion={current};fetch('/data').then(r=>r.json()).then(d=>{{window.dataVersion=d.version;}});",
                    "/data" => $"{{\"version\":{current}}}",
                    _ => $"<!doctype html><html><body data-version='{current}'><script src='/app.js'></script></body></html>",
                };
                var bytes = Encoding.UTF8.GetBytes(body);
                context.Response.ContentLength64 = bytes.Length;
                await context.Response.OutputStream.WriteAsync(bytes, ended.Token); context.Response.Close();
            }
        }, CancellationToken.None);
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var root = Path.Combine(Path.GetTempPath(), "scp-cv-refresh-030", Guid.NewGuid().ToString("N"));
        var thread = new Thread(() =>
        {
            var dispatcher = Dispatcher.CurrentDispatcher;
            dispatcher.BeginInvoke(async () =>
            {
                var window = new PlayerWindow { ShowActivated = false, Topmost = false, Title = "SCP-cv 网页刷新验证" };
                var host = new PlayerRuntimeHost(window, 1, webViewUserDataRoot: root);
                try
                {
                    window.AssignBounds(20, 20, 640, 360); window.Show();
                    await host.ExecuteAsync(Lease("OPEN", new { source_id = 1, source_type = "web", uri = origin, autoplay = true }), CancellationToken.None);
                    var web = FindWeb(window) ?? throw new InvalidOperationException("正式WebView未创建。");
                    var handle = window.NativeHandle; await WaitData(web, 1);
                    await web.ExecuteScriptAsync("document.cookie='refresh_check=preserved;path=/';");
                    var count = Volatile.Read(ref requests);
                    Volatile.Write(ref version, 2);
                    var result = await host.ExecuteAsync(Lease("REFRESH", new { }), CancellationToken.None);
                    Assert.Equal("completed", result.Status); await WaitData(web, 2);
                    Assert.Same(web, FindWeb(window)); Assert.Equal(handle, window.NativeHandle);
                    Assert.Equal(origin, web.Source.AbsoluteUri); Assert.Contains("refresh_check=preserved", await web.ExecuteScriptAsync("document.cookie"), StringComparison.Ordinal);
                    Assert.True(Volatile.Read(ref requests) >= count + 3, "页面、脚本、数据应分别重新请求。");
                    await Assert.ThrowsAsync<InvalidOperationException>(() => host.ExecuteAsync(Lease("REFRESH", new { }) with { SourceGeneration = 2 }, CancellationToken.None));
                    Volatile.Write(ref version, 3);
                    await Assert.ThrowsAsync<InvalidOperationException>(() => host.ExecuteAsync(Lease("REFRESH", new { }), CancellationToken.None));
                    completion.SetResult();
                }
                catch (Exception error) { completion.SetException(error); }
                finally { await host.DisposeAsync(); window.Close(); dispatcher.InvokeShutdown(); }
            });
            Dispatcher.Run();
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA); thread.Start();
        try { await completion.Task.WaitAsync(TimeSpan.FromSeconds(90)); }
        finally
        {
            Assert.True(thread.Join(TimeSpan.FromSeconds(15)), "网页验证线程未退出。");
            await ended.CancelAsync(); listener.Stop();
            try { await server; } catch (OperationCanceledException) when (ended.IsCancellationRequested) { }
        }
    }

    /// <summary>:param node: 实际视觉树。:returns: 正式窗口持有的WebView控件。</summary>
    private static WebView2? FindWeb(DependencyObject node)
    {
        if (node is WebView2 web) return web;
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(node); index++)
            if (FindWeb(VisualTreeHelper.GetChild(node, index)) is { } child) return child;
        return null;
    }

    /// <summary>:param web: 正式控件。:param version: 服务器实际版本。:returns: 页面异步请求已读到对应正文。</summary>
    private static async Task WaitData(WebView2 web, int version)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        while (true)
        {
            var value = await web.ExecuteScriptAsync($"document.body.dataset.version==='{version}' && window.scriptVersion==={version} && window.dataVersion==={version}");
            if (value == "true") return;
            await Task.Delay(100, deadline.Token);
        }
    }

    /// <summary>:param command: 正式命令。:param args: 实际来源参数。:returns: 同一源修订和代次的租约。</summary>
    private static CommandLeaseDto Lease(string command, object args) => new()
    {
        CommandId = Guid.NewGuid(), ClaimToken = Guid.NewGuid(), Command = command, SourceGeneration = 1, SourceRevision = 1,
        Args = JsonSerializer.SerializeToElement(args).EnumerateObject().ToDictionary(property => property.Name, property => property.Value),
    };
}
