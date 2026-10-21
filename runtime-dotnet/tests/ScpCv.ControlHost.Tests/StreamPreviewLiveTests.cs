// 文件描述：真实MediaMTX、编码与两种FLV输出验证；项目：SCP-cv；文件：StreamPreviewLiveTests.cs；作者：Qintsg；日期：2026-10-21。
using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ScpCv.ControlHost.StreamPreview;
using ScpCv.Domain.Model;
using ScpCv.Infrastructure.Persistence;

namespace ScpCv.ControlHost.Tests;

[Trait("Category", "Physical")]
public sealed class StreamPreviewLiveTests
{
    [Fact]
    public async Task LoopbackRtmpSharesUpstreamAndDecodesBothTransportsAcrossQualityChangeAsync()
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("本显式验证只支持Windows");
        var ffmpeg = Required("SCP_PREVIEW_NATIVE_FFMPEG"); var mtx = Required("SCP_PREVIEW_NATIVE_MTX");
        var ffprobe = Path.Combine(Path.GetDirectoryName(ffmpeg)!, "ffprobe.exe"); Assert.True(File.Exists(ffprobe));
        var artifactRoot = Required("SCP_PREVIEW_ARTIFACTS");
        var root = Path.Combine(artifactRoot, Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        var ports = Ports(); var api = $"http://127.0.0.1:{ports[0]}/";
        var config = Path.Combine(root, "mediamtx.yml");
        await File.WriteAllTextAsync(config, FormattableString.Invariant($"""
            logLevel: error
            logDestinations: [stdout]
            api: true
            apiAddress: 127.0.0.1:{ports[0]}
            rtsp: true
            rtspAddress: 127.0.0.1:{ports[1]}
            rtspTransports: [tcp]
            rtmp: true
            rtmpAddress: 127.0.0.1:{ports[2]}
            hls: false
            webrtc: false
            srt: false
            playback: false
            authInternalUsers:
            - user: any
              ips: ['127.0.0.1', '::1']
              permissions:
              - action: api
              - action: publish
              - action: read
            paths:
              all_others:
                source: publisher
            """));
        await using var relay = PreviewProcess.Start(mtx, [config]);
        using var handler = new SocketsHttpHandler { UseProxy = false, AllowAutoRedirect = false };
        using var relayHttp = new HttpClient(handler) { BaseAddress = new Uri(api), Timeout = TimeSpan.FromSeconds(2) };
        await WaitAsync(async () =>
        {
            try { using var ready = await relayHttp.GetAsync("v3/config/global/get"); return ready.IsSuccessStatusCode; }
            catch (HttpRequestException) { return false; }
        });
        var originalUri = $"rtmp://127.0.0.1:{ports[2]}/live/one";
        await using var publisher = PreviewProcess.Start(ffmpeg,
            ["-nostdin", "-hide_banner", "-loglevel", "error", "-re", "-f", "lavfi", "-i", "testsrc2=size=1280x720:rate=20", "-an", "-c:v", "libx264",
                "-threads", "1", "-preset", "ultrafast", "-tune", "zerolatency", "-g", "20", "-b:v", "3000k", "-f", "flv", originalUri]);
        await WaitAsync(async () => { using var ready = await relayHttp.GetAsync("v3/paths/get/live/one"); return ready.IsSuccessStatusCode; });
        using var originalFactory = new ControlHostApplicationFactory();
        using var factory = originalFactory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("SafetyMode", "Hardware"); builder.UseSetting("StreamPreview:ApiBase", api);
            builder.UseSetting("StreamPreview:LocalRtspPort", ports[1].ToString(CultureInfo.InvariantCulture)); builder.UseSetting("StreamPreview:FfmpegPath", ffmpeg);
        });
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), HandleCookies = false, AllowAutoRedirect = false });
        using var login = await client.PostAsJsonAsync("/api/auth/login/", new { username = "operator", password = "Old-password-123" }); login.EnsureSuccessStatusCode();
        var cookies = login.Headers.GetValues("Set-Cookie").Select(value => value.Split(';')[0]).ToArray();
        var cookie = string.Join("; ", cookies); var csrf = cookies.Single(value => value.StartsWith("csrftoken=", StringComparison.Ordinal))["csrftoken=".Length..];
        client.DefaultRequestHeaders.Add("Cookie", cookie); client.DefaultRequestHeaders.Add("X-CSRFToken", csrf); client.DefaultRequestHeaders.Add("Origin", "https://localhost");
        var sourceId = await SeedAsync(factory, originalUri);
        var registry = factory.Services.GetRequiredService<StreamPreviewRegistry>();
        Assert.IsType<PreviewProducer>(factory.Services.GetRequiredService<IPreviewProducer>());
        var initial = await CreateAsync(client, sourceId); Assert.Equal(0, registry.FeedCount);
        var responses = new List<HttpResponseMessage>(); var leases = new List<Guid> { initial };
        using var captureStop = new CancellationTokenSource(); Task? capture = null;
        using var socket = new ClientWebSocket();
        var file = Path.Combine(root, "http-quality-change.flv");
        var wsFile = Path.Combine(root, "websocket.flv");
        try
        {
            using var firstRequest = new HttpRequestMessage(HttpMethod.Get, $"/api/stream-previews/{initial}/stream.flv");
            var first = await client.SendAsync(firstRequest, HttpCompletionOption.ResponseHeadersRead); responses.Add(first);
            Assert.Equal(HttpStatusCode.OK, first.StatusCode); Assert.Equal("video/x-flv", first.Content.Headers.ContentType!.MediaType);
            var warmed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            capture = CaptureAsync(await first.Content.ReadAsStreamAsync(), file, warmed, captureStop.Token);
            await warmed.Task.WaitAsync(TimeSpan.FromSeconds(12));
            // 真实TestServer WebSocket管线；认证Cookie／Origin与HTTP保持一致，不模拟浏览器输入。
            var webSocketClient = factory.Server.CreateWebSocketClient();
            webSocketClient.ConfigureRequest = request => { request.Headers.Cookie = cookie; request.Headers.Origin = "https://localhost"; };
            var wsId = await CreateAsync(client, sourceId); leases.Add(wsId);
            using var ws = await webSocketClient.ConnectAsync(new Uri($"wss://localhost/api/stream-previews/{wsId}/stream.ws"), CancellationToken.None);
            var wsCapture = CaptureWebSocketAsync(ws, wsFile, captureStop.Token);
            var extra = await Task.WhenAll(Enumerable.Range(0, 63).Select(async _ =>
            {
                var id = await CreateAsync(client, sourceId);
                using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/stream-previews/{id}/stream.flv");
                return (id, response: await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead));
            }));
            foreach (var (id, response) in extra) { leases.Add(id); responses.Add(response); Assert.Equal(HttpStatusCode.OK, response.StatusCode); }
            Assert.Equal(65, registry.LeaseCount); Assert.Equal(1, registry.FeedCount);
            Assert.Equal(1, await ConnectionsAsync(relayHttp, "rtmpconns", "read"));
            Assert.Equal(1, await ConnectionsAsync(relayHttp, "rtspsessions", "read"));
            for (var round = 0; round < 3; round++)
            {
                await Task.Delay(TimeSpan.FromSeconds(5));
                foreach (var id in new[] { initial, wsId })
                { using var heartbeat = await client.PostAsync($"/api/stream-previews/{id}/heartbeat/", null); heartbeat.EnsureSuccessStatusCode(); }
            }
            Assert.False(capture.IsCompleted); Assert.False(wsCapture.IsCompleted);
            await captureStop.CancelAsync(); await capture; await wsCapture;
            var httpDimensions = await DecodeAsync(ffprobe, file);
            var websocketDimensions = await DecodeAsync(ffprobe, wsFile);
            Assert.Contains(1280, httpDimensions); Assert.Contains(320, httpDimensions);
            Assert.NotEmpty(websocketDimensions);
            await File.WriteAllTextAsync(Path.Combine(root, "result.json"), JsonSerializer.Serialize(new
            { viewers = 65, upstreams = 1, encoders = 1, http_widths = httpDimensions, websocket_widths = websocketDimensions }));
        }
        finally
        {
            await captureStop.CancelAsync();
            if (capture is not null) { try { await capture; } catch (OperationCanceledException) { } }
            foreach (var response in responses) response.Dispose();
            foreach (var id in leases) { using var deleted = await client.DeleteAsync($"/api/stream-previews/{id}/"); }
        }
        await WaitAsync(async () => registry.FeedCount == 0 && await ConnectionsAsync(relayHttp, "rtmpconns", "read") == 0);
        await using var database = factory.Services.GetRequiredService<IDbContextFactory<ControlDbContext>>().CreateDbContext(); Assert.Empty(database.CommandRecords);
    }

    private static async Task<long> SeedAsync(WebApplicationFactory<Program> factory, string uri)
    {
        await using var database = factory.Services.GetRequiredService<IDbContextFactory<ControlDbContext>>().CreateDbContext();
        var source = new MediaSource { Name = "合成预览", EntryName = "synthetic-preview", SourceType = MediaSourceType.CustomStream, Uri = uri,
            IsAvailable = true, SourceRevision = 1, CreatedAt = DateTimeOffset.UtcNow };
        database.MediaSources.Add(source); await database.SaveChangesAsync(); return source.Id;
    }

    private static async Task<Guid> CreateAsync(HttpClient client, long sourceId)
    {
        using var response = await client.PostAsJsonAsync("/api/stream-previews/", new { source_id = sourceId, quality = "high" });
        response.EnsureSuccessStatusCode(); using var body = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync()); return body.RootElement.GetProperty("id").GetGuid();
    }

    private static async Task CaptureAsync(Stream input, string file, TaskCompletionSource warmed, CancellationToken cancellationToken)
    {
        await using var output = File.Create(file); var count = 0;
        try
        {
            var bytes = new byte[16384];
            while (true)
            {
                var read = await input.ReadAsync(bytes, cancellationToken); if (read == 0) break;
                await output.WriteAsync(bytes.AsMemory(0, read), cancellationToken);
                count += read; if (count >= 32768) warmed.TrySetResult();
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
    }

    private static async Task CaptureWebSocketAsync(WebSocket socket, string file, CancellationToken cancellationToken)
    {
        await using var output = File.Create(file); var bytes = new byte[16384];
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            { var result = await socket.ReceiveAsync(bytes.AsMemory(), cancellationToken); if (result.MessageType == WebSocketMessageType.Close) break; await output.WriteAsync(bytes.AsMemory(0, result.Count), cancellationToken); }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
    }

    private static async Task<int[]> DecodeAsync(string ffprobe, string file)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException();
        await using var decoder = PreviewProcess.Start(ffprobe, ["-v", "error", "-select_streams", "v:0", "-show_frames", "-show_entries", "frame=width,height,best_effort_timestamp_time", "-of", "json", file]);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        using var json = await JsonDocument.ParseAsync(decoder.Output, cancellationToken: timeout.Token);
        var frames = json.RootElement.GetProperty("frames").EnumerateArray().ToArray(); Assert.True(frames.Length > 4);
        var times = frames.Where(frame => frame.TryGetProperty("best_effort_timestamp_time", out _)).Select(frame => double.Parse(frame.GetProperty("best_effort_timestamp_time").GetString()!, CultureInfo.InvariantCulture)).ToArray();
        Assert.True(times.Zip(times.Skip(1)).All(pair => pair.First <= pair.Second));
        return frames.Select(frame => frame.GetProperty("width").GetInt32()).Distinct().ToArray();
    }

    private static async Task<int> ConnectionsAsync(HttpClient client, string kind, string state)
    {
        using var response = await client.GetAsync($"v3/{kind}/list"); response.EnsureSuccessStatusCode();
        using var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        return json.RootElement.GetProperty("items").EnumerateArray().Count(item => item.GetProperty("state").GetString() == state);
    }

    private static async Task WaitAsync(Func<Task<bool>> condition)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        while (!await condition()) await Task.Delay(200, timeout.Token);
    }

    private static int[] Ports()
    {
        var listeners = Enumerable.Range(0, 3).Select(_ => new TcpListener(IPAddress.Loopback, 0)).ToArray();
        try { foreach (var listener in listeners) listener.Start(); return listeners.Select(listener => ((IPEndPoint)listener.LocalEndpoint).Port).ToArray(); }
        finally { foreach (var listener in listeners) listener.Stop(); }
    }

    private static string Required(string name) => Environment.GetEnvironmentVariable(name) ?? throw new InvalidOperationException("请显式设置原生验证配置：" + name);
}
