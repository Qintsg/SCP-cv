// 文件描述：真实主机、管道和接收窗口验证共享后帧及网页刷新入队。
// 项目：SCP-cv；文件：MediaSharingRealTests.cs；作者：Qintsg；日期：2026-10-30。
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text.Json;
using System.Windows.Threading;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using ScpCv.Contracts.Ipc;
using ScpCv.Contracts.Runtime;
using ScpCv.ControlHost.Ipc;
using ScpCv.Infrastructure.Runtime;
using ScpCv.Infrastructure.Playback;
using ScpCv.PlayerWorker;
using ScpCv.PlayerWorker.Playback;

namespace ScpCv.Integration.Tests;

public sealed class MediaSharingRealTests
{
    /// <summary>:returns: 实际认证控制接口允许网页刷新并拒绝其它来源。</summary>
    [Fact]
    public async Task RefreshUsesRealQueueAndRejectsOtherSources()
    {
        using var factory = new WindowForegroundHostFactory(); factory.UseKestrel(0);
        using var client = factory.CreateClient();
        client.BaseAddress = new Uri(factory.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single());
        await Login(client, factory.Password);
        using var created = await client.PostAsJsonAsync("/api/sources/web/", new { url = "http://127.0.0.1:19232/web-content.html", name = "刷新验证网页" }); created.EnsureSuccessStatusCode();
        using var value = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        var id = value.RootElement.GetProperty("source").GetProperty("id").GetInt64();
        using var opened = await client.PostAsJsonAsync("/api/playback/1/open/", new { source_id = id }); opened.EnsureSuccessStatusCode();
        using var refreshed = await client.PostAsJsonAsync("/api/playback/1/control/", new { action = "refresh" }); refreshed.EnsureSuccessStatusCode();
        using var receipt = JsonDocument.Parse(await refreshed.Content.ReadAsStringAsync());
        var first = receipt.RootElement.GetProperty("sessions").EnumerateArray().Single(item => item.GetProperty("window_id").GetInt32() == 1);
        var commandId = first.GetProperty("submitted_command_id").GetGuid(); Assert.NotEqual(Guid.Empty, commandId);
        using var commandResponse = await client.GetAsync($"/api/playback/1/commands/{commandId}/"); commandResponse.EnsureSuccessStatusCode();
        using var command = JsonDocument.Parse(await commandResponse.Content.ReadAsStringAsync());
        Assert.Equal("REFRESH", command.RootElement.GetProperty("command").GetProperty("command").GetString());
        using var empty = await client.PostAsJsonAsync("/api/playback/2/control/", new { action = "refresh" }); Assert.Equal(HttpStatusCode.BadRequest, empty.StatusCode);
        using var content = new MultipartFormDataContent();
        content.Add(new ByteArrayContent(await File.ReadAllBytesAsync(Path.Combine(RepositoryRoot(), "frontend/src/assets/sspu-ailab-transparent.png"))), "file", "image.png");
        using var image = await client.PostAsync("/api/sources/upload/", content); image.EnsureSuccessStatusCode();
        using var imageValue = JsonDocument.Parse(await image.Content.ReadAsStringAsync());
        using var imageOpened = await client.PostAsJsonAsync("/api/playback/1/open/", new { source_id = imageValue.RootElement.GetProperty("source").GetProperty("id").GetInt64() }); imageOpened.EnsureSuccessStatusCode();
        using var rejected = await client.PostAsJsonAsync("/api/playback/1/control/", new { action = "refresh" }); Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
    }

    /// <summary>:returns: 实际文件采集、浏览器Publisher、正式WebView接收和停止释放；不代替屏幕许可验证。</summary>
    [Fact]
    [Trait("Category", "Physical")]
    public async Task ActualVideoCaptureSurvivesViewChangesAndReleases()
    {
        using var socket = new TcpListener(IPAddress.Loopback, 0); socket.Start(); var port = ((IPEndPoint)socket.LocalEndpoint).Port; socket.Stop();
        var topology = new WindowsDisplayTopologyProvider().GetCurrent(); Assert.True(topology.Available, topology.Detail);
        var display = topology.Targets[0].Name;
        using var factory = new WindowForegroundHostFactory { ReceiverBaseUrl = $"http://127.0.0.1:{port}/", Window1Display = display }; factory.UseKestrel(port);
        using var client = factory.CreateClient(); client.BaseAddress = new Uri($"http://127.0.0.1:{port}");
        await Login(client, factory.Password);
        var authority = factory.Services.GetRequiredService<RuntimeAuthorityRepository>();
        var startId = Guid.NewGuid(); var group = await authority.BeginStartAsync(startId); await authority.ArmAsync(startId, group.GroupEpoch);
        using var process = Process.GetCurrentProcess(); var instance = Guid.NewGuid();
        factory.Services.GetRequiredService<RegisteredProcessRegistry>().Register(new(process.Id, new DateTimeOffset(process.StartTime.ToUniversalTime(), TimeSpan.Zero), process.SessionId, "player-1", instance));
        var pipe = factory.Services.GetRequiredService<NamedPipeServer>();
        await using var session = new RuntimeWorkerSession(pipe.PipeName, new("player-1", instance, new IpcTargetDto { Kind = "display", Id = 1 }, ["wpf", "webview2", "image"]));
        using var stop = new CancellationTokenSource(); await session.ConnectAndReadyAsync(stop.Token);
        var ready = new TaskCompletionSource<(Dispatcher Ui, PlayerWindow Window, PlayerRuntimeHost Host)>(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            var ui = Dispatcher.CurrentDispatcher;
            try
            {
                var window = new PlayerWindow { ShowActivated = false, Topmost = false, Title = "SCP-cv 共享接收验证" }; window.AssignBounds(20, 440, 480, 270); window.Show();
                var profile = Path.Combine(Path.GetTempPath(), "scp-cv-sharing-030", Guid.NewGuid().ToString("N"));
                ready.SetResult((ui, window, new PlayerRuntimeHost(window, 1, session, webViewUserDataRoot: profile)));
                Dispatcher.Run();
            }
            catch (Exception error) { ready.TrySetException(error); }
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA); thread.Start();
        var native = await ready.Task.WaitAsync(TimeSpan.FromSeconds(15));
        var loop = session.RunAsync(native.Host.ExecuteAsync, native.Host.SampleProgressAsync, stop.Token);
        try
        {
            using var displays = await client.GetAsync("/api/displays/");
            var displayBody = await displays.Content.ReadAsStringAsync();
            using var selected = await client.PostAsJsonAsync("/api/displays/select/", new { window_id = 1, display_mode = "single", target_label = display });
            Assert.True(selected.IsSuccessStatusCode, await selected.Content.ReadAsStringAsync() + displayBody);
            var runtime = factory.Services.GetRequiredService<RuntimeStateService>();
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            while (true)
            {
                try { await runtime.GetScreenShareTargetAsync(1, deadline.Token); break; }
                catch (PlaybackServiceException) { await Task.Delay(100, deadline.Token); }
            }
            var launch = new ProcessStartInfo("node") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true, WorkingDirectory = RepositoryRoot() };
            launch.ArgumentList.Add(Path.Combine(RepositoryRoot(), "frontend/scripts/verify-media-sharing-real.mjs"));
            launch.Environment["SCP_MEDIA_BACKEND"] = client.BaseAddress.GetLeftPart(UriPartial.Authority); launch.Environment["SCP_MEDIA_PASSWORD"] = factory.Password;
            using var child = Process.Start(launch)!; var output = child.StandardOutput.ReadToEndAsync(); var errors = child.StandardError.ReadToEndAsync();
            await child.WaitForExitAsync().WaitAsync(TimeSpan.FromMinutes(3));
            var standardOutput = await output; var standardError = await errors;
            await File.WriteAllTextAsync(Path.Combine(RepositoryRoot(), ".validation/media-030/share-child.json"), JsonSerializer.Serialize(new { child.ExitCode, standardOutput, standardError }));
            Assert.True(child.ExitCode == 0, $"Node退出码{child.ExitCode}：{standardOutput}{standardError}");
        }
        finally
        {
            await stop.CancelAsync(); try { await loop.WaitAsync(TimeSpan.FromSeconds(10)); } catch (OperationCanceledException) { }
            await native.Ui.InvokeAsync(async () => { await native.Host.DisposeAsync(); native.Window.Close(); }).Task.Unwrap();
            native.Ui.InvokeShutdown(); Assert.True(thread.Join(TimeSpan.FromSeconds(10)));
        }
    }

    /// <summary>:param client: 实际HTTP客户端。:param password: 本轮随机口令。:returns: 登录和CSRF配置完成。</summary>
    private static async Task Login(HttpClient client, string password)
    {
        using var csrf = await client.GetAsync("/api/auth/csrf/"); csrf.EnsureSuccessStatusCode();
        using var login = await client.PostAsJsonAsync("/api/auth/login/", new { username = "window-admin", password }); login.EnsureSuccessStatusCode();
        using var response = await client.GetAsync("/api/auth/csrf/"); using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        client.DefaultRequestHeaders.Add("X-CSRFToken", body.RootElement.GetProperty("csrfToken").GetString()); client.DefaultRequestHeaders.Add("Origin", client.BaseAddress!.GetLeftPart(UriPartial.Authority));
    }

    /// <summary>:returns: 当前仓库目录。</summary>
    private static string RepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !Directory.Exists(Path.Combine(current.FullName, ".specify"))) current = current.Parent;
        return current?.FullName ?? throw new DirectoryNotFoundException("仓库目录缺失。");
    }
}
