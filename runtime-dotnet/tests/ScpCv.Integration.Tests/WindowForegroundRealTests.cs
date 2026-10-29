// 文件描述：真实Kestrel、Named Pipe和两块WPF窗口验证Web维护闭环。
// 项目：SCP-cv；文件：WindowForegroundRealTests.cs；作者：Qintsg；日期：2026-10-29。
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Windows.Threading;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using ScpCv.Contracts.Http;
using ScpCv.Contracts.Ipc;
using ScpCv.Contracts.Runtime;
using ScpCv.ControlHost.Ipc;
using ScpCv.ControlHost.Events;
using ScpCv.Domain.Model;
using ScpCv.Infrastructure.Runtime;
using ScpCv.PlayerWorker;
using ScpCv.PlayerWorker.Playback;

namespace ScpCv.Integration.Tests;

public sealed partial class WindowForegroundRealTests
{
    private static readonly bool[] ForegroundStates = [false, true];
    /// <summary>:returns: 经真实HTTP和认证管道隐藏、恢复两窗，并保留实际源状态。</summary>
    [Fact]
    public async Task RealHttpAndWindowsKeepVisibilitySeparateFromPlayback()
    {
        using var factory = new WindowForegroundHostFactory(); factory.UseKestrel(0);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        client.BaseAddress = new Uri(factory.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single());
        using (var anonymous = await client.PostAsJsonAsync("/api/playback/window-foreground/", new { foreground = false })) Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        using var csrf = await client.GetAsync("/api/auth/csrf/"); csrf.EnsureSuccessStatusCode();
        using var login = await client.PostAsJsonAsync("/api/auth/login/", new { username = "window-admin", password = factory.Password }); login.EnsureSuccessStatusCode();
        using (var noCsrf = await client.PostAsJsonAsync("/api/playback/window-foreground/", new { foreground = false })) Assert.Equal(HttpStatusCode.BadRequest, noCsrf.StatusCode);
        using var tokenResponse = await client.GetAsync("/api/auth/csrf/");
        using var token = await JsonDocument.ParseAsync(await tokenResponse.Content.ReadAsStreamAsync());
        client.DefaultRequestHeaders.Add("X-CSRFToken", token.RootElement.GetProperty("csrfToken").GetString());
        client.DefaultRequestHeaders.Add("Origin", client.BaseAddress!.GetLeftPart(UriPartial.Authority));
        using (var invalid = await client.PostAsJsonAsync("/api/playback/window-foreground/", new { foreground = "false" })) Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        using (var unavailable = await client.PostAsJsonAsync("/api/playback/window-foreground/", new { foreground = false })) Assert.Equal(HttpStatusCode.Conflict, unavailable.StatusCode);
        var authority = factory.Services.GetRequiredService<RuntimeAuthorityRepository>();
        var start = Guid.NewGuid(); var group = await authority.BeginStartAsync(start); await authority.ArmAsync(start, group.GroupEpoch);
        using var process = Process.GetCurrentProcess();
        using var stop = new CancellationTokenSource();
        var ready = new TaskCompletionSource<(Dispatcher Ui, PlayerWindow[] Windows, PlayerRuntimeHost[] Hosts)>(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            var ui = Dispatcher.CurrentDispatcher;
            try
            {
                var windows = Enumerable.Range(1, 2).Select(index => new PlayerWindow { ShowActivated = false, Title = $"SCP-cv Web维护验证 {index}" }).ToArray();
                foreach (var (window, index) in windows.Select((window, index) => (window, index))) { window.AssignBounds(40 + index * 340, 260, 320, 180); window.Show(); }
                ready.SetResult((ui, windows, windows.Select((window, index) => new PlayerRuntimeHost(window, index + 1)).ToArray()));
                Dispatcher.Run();
            }
            catch (Exception error) { ready.TrySetException(error); }
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA); thread.Start();
        var native = await ready.Task.WaitAsync(TimeSpan.FromSeconds(15));
        var sessions = new List<RuntimeWorkerSession>(); var loops = new List<Task>();
        var identities = new List<Guid>();
        try
        {
            var registry = factory.Services.GetRequiredService<RegisteredProcessRegistry>();
            var pipe = factory.Services.GetRequiredService<NamedPipeServer>();
            for (var index = 0; index < 2; index++)
            {
                var id = Guid.NewGuid(); var role = $"player-{index + 1}";
                identities.Add(id);
                registry.Register(new(process.Id, new DateTimeOffset(process.StartTime.ToUniversalTime(), TimeSpan.Zero), process.SessionId, role, id));
                var session = new RuntimeWorkerSession(pipe.PipeName, new(role, id, new IpcTargetDto { Kind = "display", Id = index + 1 }, ["wpf", "image"]));
                sessions.Add(session); await session.ConnectAndReadyAsync(stop.Token);
                var host = native.Hosts[index]; loops.Add(session.RunAsync(host.ExecuteAsync, host.SampleProgressAsync, stop.Token));
            }
            await WaitStateAsync(client, true);
            using (var content = new MultipartFormDataContent())
            {
                content.Add(new StringContent("窗口维护验证图片"), "name"); content.Add(new StringContent("image"), "source_type");
                var image = new ByteArrayContent(await File.ReadAllBytesAsync(Path.Combine(RepositoryRoot(), "frontend", "src", "assets", "sspu-ailab-transparent.png")));
                image.Headers.ContentType = new MediaTypeHeaderValue("image/png"); content.Add(image, "file", "ailab.png");
                using var uploaded = await client.PostAsync("/api/sources/upload/", content); uploaded.EnsureSuccessStatusCode();
                using var uploadedBody = await JsonDocument.ParseAsync(await uploaded.Content.ReadAsStreamAsync());
                var sourceId = uploadedBody.RootElement.GetProperty("source").GetProperty("id").GetInt64();
                for (var windowId = 1; windowId <= 2; windowId++)
                {
                    using var opened = await client.PostAsJsonAsync($"/api/playback/{windowId}/open/", new { source_id = sourceId, autoplay = true }); opened.EnsureSuccessStatusCode();
                }
                for (var index = 0; index < 100; index++)
                {
                    var loaded = await WaitStateAsync(client, true);
                    if (loaded.All(window => window.ActualSourceId == sourceId)) break;
                    if (index == 99) throw new TimeoutException("真实图片未打开。");
                    await Task.Delay(100);
                }
            }
            await VerifyWebAsync(client.BaseAddress!, factory.Password, "隐藏窗口");
            var oldObservation = await native.Hosts[0].SampleProgressAsync(CancellationToken.None);
            Assert.NotNull(oldObservation);
            for (var index = 0; index < 20; index++)
                foreach (var foreground in ForegroundStates)
                {
                    using var response = await client.PostAsJsonAsync("/api/playback/window-foreground/", new { foreground });
                    Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
                    using var receipt = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
                    Assert.All(receipt.RootElement.GetProperty("sessions").EnumerateArray(), window => Assert.True(window.GetProperty("submitted_command_id").GetGuid() != Guid.Empty));
                    await WaitStateAsync(client, foreground);
                    await native.Ui.InvokeAsync(() => Assert.All(native.Windows, window => Assert.Equal(foreground, window.WindowForeground)));
                    if (index == 0 && !foreground)
                    {
                        await VerifyWebAsync(client.BaseAddress!, factory.Password, "显示窗口");
                        var projections = factory.Services.GetRequiredService<RuntimeProjectionPublisher>();
                        await projections.ApplyStateReportAsync(CommandTargetKind.Display, 1, identities[0], sessions[0].OwnerEpoch,
                            new StateReportDto { SourceGeneration = oldObservation.SourceGeneration, State = oldObservation.State });
                        Assert.False((await WaitStateAsync(client, false))[0].WindowForeground);
                        var fenced = await projections.ApplyStateReportAsync(CommandTargetKind.Display, 1, Guid.NewGuid(), sessions[0].OwnerEpoch,
                            new StateReportDto { SourceGeneration = oldObservation.SourceGeneration, State = oldObservation.State });
                        Assert.False(fenced.Accepted); Assert.Equal("worker_fenced", fenced.Reason);
                        var oldOwner = await projections.ApplyStateReportAsync(CommandTargetKind.Display, 1, identities[0], sessions[0].OwnerEpoch - 1,
                            new StateReportDto { SourceGeneration = oldObservation.SourceGeneration, State = oldObservation.State });
                        Assert.False(oldOwner.Accepted); Assert.Equal("worker_fenced", oldOwner.Reason);
                    }
                }
            await native.Ui.InvokeAsync(() => native.Windows[0].SetWindowForeground(false));
            var mixed = await WaitStateAsync(client, false, mixed: true);
            Assert.False(mixed[0].WindowForeground); Assert.True(mixed[1].WindowForeground);
            using var show = await client.PostAsJsonAsync("/api/playback/window-foreground/", new { foreground = true }); show.EnsureSuccessStatusCode(); await WaitStateAsync(client, true);
            using var missing = await client.PostAsJsonAsync("/api/playback/window-foreground/", new { }); Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
        }
        finally
        {
            stop.Cancel();
            foreach (var loop in loops) { try { await loop.WaitAsync(TimeSpan.FromSeconds(10)); } catch (OperationCanceledException) { } }
            foreach (var session in sessions) await session.DisposeAsync();
            await native.Ui.InvokeAsync(async () => { foreach (var host in native.Hosts) await host.DisposeAsync(); foreach (var window in native.Windows) window.Close(); }).Task.Unwrap();
            native.Ui.InvokeShutdown(); Assert.True(thread.Join(TimeSpan.FromSeconds(10)));
        }
    }

    /// <summary>:param backend: 实际Kestrel地址。:param password: 本轮真实账号。:param expected: 实际窗口状态对应菜单。:returns: 可选的只读浏览器验收。</summary>
    private static async Task VerifyWebAsync(Uri backend, string password, string expected)
    {
        if (Environment.GetEnvironmentVariable("SCP_CV_VERIFY_WINDOW_WEB") != "1") return;
        var start = new ProcessStartInfo("node") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        start.ArgumentList.Add(Path.Combine(RepositoryRoot(), "frontend", "scripts", "verify-window-foreground-web.mjs"));
        start.Environment["SCP_WINDOW_BACKEND"] = backend.GetLeftPart(UriPartial.Authority);
        start.Environment["SCP_WINDOW_PASSWORD"] = password; start.Environment["SCP_WINDOW_EXPECTED"] = expected;
        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEndAsync(); var errors = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync().WaitAsync(TimeSpan.FromMinutes(2));
        Assert.True(process.ExitCode == 0, await output + await errors);
    }

    /// <summary>:returns: 当前实际仓库目录。</summary>
    private static string RepositoryRoot()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !Directory.Exists(Path.Combine(root.FullName, ".specify"))) root = root.Parent;
        return root?.FullName ?? throw new DirectoryNotFoundException("测试仓库目录缺失。");
    }

    /// <summary>:param client: 实际HTTP客户端。:param foreground: 预期真实窗口顺序。:param mixed: 仅第一窗隐藏。:returns: 确认后的两窗状态。</summary>
    private static async Task<PlaybackSessionDto[]> WaitStateAsync(HttpClient client, bool foreground, bool mixed = false)
    {
        for (var index = 0; index < 100; index++)
        {
            using var response = await client.GetAsync("/api/sessions/"); response.EnsureSuccessStatusCode();
            using var body = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
            var sessions = body.RootElement.GetProperty("sessions").Deserialize<PlaybackSessionDto[]>()!;
            if (sessions.Length == 2 && sessions.All(window => window.PlayerOnline && window.WindowForeground == (mixed && window.WindowId == 2 ? true : foreground))) return sessions;
            await Task.Delay(100);
        }
        throw new TimeoutException("真实窗口观测未在10秒内确认。");
    }
}
