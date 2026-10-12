// 接收HTTP只认本机高熵cap，不认发布者cookie；这些模拟帧不是原生出画证据。SCP-cv 2026-10-10。
using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ScpCv.ControlHost.ScreenSharing;

namespace ScpCv.ControlHost.Tests;

public sealed class ScreenShareReceiverEndpointTests
{
    private readonly ReceiverClock _clock = new();

    [Fact]
    public async Task ReceiverFixtureArrangementAndConcurrentFormalCleanupKeepGrantValid()
    {
        // 强制清理与准备相交：必须经过正式Service的同窗口门禁，不能关闭后台或增大期限。
        for (var round = 0; round < 10; round++)
        {
            using var root = new ControlHostApplicationFactory();
            using var factory = ReceiverHost(root);
            var service = factory.Services.GetRequiredService<ScreenShareService>();
            using var stop = new CancellationTokenSource();
            var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var cleanup = Task.Run(async () =>
            {
                ready.SetResult();
                while (!stop.IsCancellationRequested)
                {
                    await service.CleanupOnceAsync(stop.Token);
                    await Task.Delay(1, stop.Token);
                }
            });
            try
            {
                await ready.Task.WaitAsync(TimeSpan.FromSeconds(5));
                var (grant, _) = await ArrangeAsync(factory).WaitAsync(TimeSpan.FromSeconds(10));
                Assert.Equal(200, (await ExchangeAsync(factory, grant, "offer/", "GET")).Status);
            }
            finally
            {
                await stop.CancelAsync();
                try { await cleanup.WaitAsync(TimeSpan.FromSeconds(5)); }
                catch (OperationCanceledException) when (stop.IsCancellationRequested) { }
            }
        }
    }

    [Fact]
    public async Task ReceiverContractFixtureKeepsSeededWorkerOnlineUntilTestClockAdvances()
    {
        using var root = new ControlHostApplicationFactory();
        using var factory = ReceiverHost(root);
        var (grant, _) = await ArrangeAsync(factory);
        // 合同测试没有真实Worker发心跳，调度延迟不应自行改变被测授权前提。
        await Task.Delay(TimeSpan.FromSeconds(6));
        Assert.Equal(200, (await ExchangeAsync(factory, grant, "offer/", "GET")).Status);
        _clock.Advance(TimeSpan.FromSeconds(6));
        await factory.Services.GetRequiredService<ScreenShareService>().CleanupOnceAsync();
        Assert.Equal(404, (await ExchangeAsync(factory, grant, "offer/", "GET")).Status);
    }

    [Fact]
    public async Task ReceiverPageIsStaticNoStoreAndRequiresHeaderCapabilityEvenOnLoopback()
    {
        using var root = new ControlHostApplicationFactory();
        using var factory = ReceiverHost(root);
        var (grant, _) = await ArrangeAsync(factory);
        var accepted = await ExchangeAsync(factory, grant, "", "GET");
        Assert.Equal(200, accepted.Status);
        Assert.Contains("requestVideoFrameCallback", accepted.Body, StringComparison.Ordinal);
        Assert.Contains("default-src 'none'", accepted.Headers["Content-Security-Policy"], StringComparison.Ordinal);
        Assert.Contains("sha256-", accepted.Headers["Content-Security-Policy"], StringComparison.Ordinal);
        Assert.Contains("no-store", accepted.Headers["Cache-Control"], StringComparison.Ordinal);
        Assert.DoesNotContain(grant.Token, accepted.Body, StringComparison.Ordinal);
        Assert.Equal(404, (await ExchangeAsync(factory, grant, "", "GET", token: "")).Status);
        Assert.Equal(404, (await ExchangeAsync(factory, grant, "", "GET", token: "", extraQuery: "&token=" + grant.Token)).Status);
    }

    [Theory]
    [InlineData("192.168.5.194", "")]
    [InlineData("127.0.0.1", "https://attacker.invalid")]
    [InlineData("127.0.0.1", "null")]
    public async Task ValidCapabilityStillCannotBypassPeerOrOriginGate(string remote, string origin)
    {
        using var root = new ControlHostApplicationFactory();
        using var factory = ReceiverHost(root);
        var (grant, _) = await ArrangeAsync(factory);
        Assert.Equal(404, (await ExchangeAsync(factory, grant, "offer/", "GET", remote: remote, origin: origin)).Status);
    }

    [Fact]
    public async Task TokenCannotReadOtherWindowOrGenerationAndUserCookieIsNotReceiverAuthority()
    {
        using var root = new ControlHostApplicationFactory();
        using var factory = ReceiverHost(root);
        var (grant, _) = await ArrangeAsync(factory);
        Assert.Equal(404, (await ExchangeAsync(factory, grant, "offer/", "GET", window: 2)).Status);
        Assert.Equal(404, (await ExchangeAsync(factory, grant, "offer/", "GET", generation: grant.SourceGeneration + 1)).Status);
        using var client = CreateHttpsClient(factory);
        await ScreenShareEndpointTests.LoginAsync(client);
        using var cookieOnly = await client.GetAsync($"/api/screen-share/receiver/{grant.Id}/offer/?window_id=1&source_generation={grant.SourceGeneration}");
        Assert.Equal(HttpStatusCode.NotFound, cookieOnly.StatusCode);
    }

    [Fact]
    public async Task AnswerIsBoundedAndSingleUseAndOnlyActivatedVideoFramesGrantReceivingState()
    {
        using var root = new ControlHostApplicationFactory();
        using var factory = ReceiverHost(root);
        var (grant, registry) = await ArrangeAsync(factory);
        var offer = await ExchangeAsync(factory, grant, "offer/", "GET");
        Assert.Equal(200, offer.Status);
        Assert.Equal(400, (await ExchangeAsync(factory, grant, "answer/", "POST", new { type = "offer", sdp = "v=0\r\n" })).Status);
        Assert.Equal(413, (await ExchangeAsync(factory, grant, "answer/", "POST", new { type = "answer", sdp = "v=0\r\n" + new string('a', 65536) })).Status);
        Assert.Equal(200, (await ExchangeAsync(factory, grant, "answer/", "POST", new { type = "answer", sdp = "v=0\r\n" })).Status);
        Assert.Equal(409, (await ExchangeAsync(factory, grant, "answer/", "POST", new { type = "answer", sdp = "v=0\r\n" })).Status);
        Assert.Equal(400, (await ExchangeAsync(factory, grant, "frames/", "POST", new { frames = 1, has_audio = false, activated = false })).Status);
        Assert.Equal(ScreenShareState.Negotiating, registry.GetOwned("test-owner", grant.Id).State);
        Assert.Equal(200, (await ExchangeAsync(factory, grant, "frames/", "POST", new { frames = 2, has_audio = false, activated = true })).Status);
        Assert.Equal(ScreenShareState.Receiving, registry.GetOwned("test-owner", grant.Id).State);
        Assert.Equal(400, (await ExchangeAsync(factory, grant, "frames/", "POST", new { frames = 2, has_audio = false, activated = true })).Status);
    }

    [Fact]
    public async Task ReceiverEndRevokesGrantButDoesNotFalselyConfirmNativeRelease()
    {
        using var root = new ControlHostApplicationFactory();
        using var factory = ReceiverHost(root);
        var (grant, registry) = await ArrangeAsync(factory);
        Assert.Equal(200, (await ExchangeAsync(factory, grant, "end/", "POST")).Status);
        Assert.Equal(ScreenShareState.Stopping, registry.GetOwned("test-owner", grant.Id).State);
        Assert.Equal(404, (await ExchangeAsync(factory, grant, "offer/", "GET")).Status);
        Assert.Equal(404, (await ExchangeAsync(factory, grant, "answer/", "POST", new { type = "answer", sdp = "v=0\r\n" })).Status);
    }

    [Fact]
    public async Task ChunkedHugeBodyIsRejectedAndInvalidNumericSchemaHasNoFrameSideEffect()
    {
        using var root = new ControlHostApplicationFactory();
        using var factory = ReceiverHost(root);
        var (grant, registry) = await ArrangeAsync(factory);
        Assert.Equal(413, (await ExchangeAsync(factory, grant, "answer/", "POST", new { type = "answer", sdp = new string('a', 400000) }, omitContentLength: true)).Status);
        Assert.Equal(400, (await ExchangeAsync(factory, grant, "frames/", "POST", new { frames = "1", activated = true })).Status);
        Assert.Equal(0, registry.GetOwned("test-owner", grant.Id).ActualFrames);
    }

    /// <summary>:param root: 原Simulation主机。:returns: 仅替换时间seam；认证、SQLite、接收路由及真实后台清理都保持。</summary>
    private WebApplicationFactory<Program> ReceiverHost(ControlHostApplicationFactory root) =>
        root.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        { services.RemoveAll<TimeProvider>(); services.AddSingleton<TimeProvider>(_clock); }));

    /// <summary>:param factory: 当前有序时钟主机。:returns: 原认证Cookie和正常HTTPS路由的测试客户端。</summary>
    private static HttpClient CreateHttpsClient(WebApplicationFactory<Program> factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions
        { AllowAutoRedirect = false, BaseAddress = new Uri("https://localhost"), HandleCookies = true });

    private static async Task<(ScreenShareReceiverGrant Grant, ScreenShareRegistry Registry)> ArrangeAsync(WebApplicationFactory<Program> factory)
    {
        using var client = CreateHttpsClient(factory);
        var target = await ScreenShareEndpointTests.SeedOnlineAsync(factory);
        var registry = factory.Services.GetRequiredService<ScreenShareRegistry>();
        // 直接跨Registry/Runtime准备会跳过正式窗口门禁，让周期清理观察到提交中间态。
        var service = factory.Services.GetRequiredService<ScreenShareService>();
        var share = await service.CreateAsync("test-owner", target.WindowId, false);
        share = await service.OfferAsync("test-owner", share.Id, "offer", "v=0\r\n");
        return (registry.GetReceiverGrant(share.Id, target, registry.GetOwned("test-owner", share.Id).SourceGeneration!.Value), registry);
    }

    /// <summary>显式构造传输地址，TestServer的默认null远端不能假装为loopback。</summary>
    private static async Task<ReceiverResponse> ExchangeAsync(WebApplicationFactory<Program> factory, ScreenShareReceiverGrant grant,
        string suffix, string method, object? body = null, string? token = null, string remote = "127.0.0.1", string origin = "",
        int? window = null, long? generation = null, string extraQuery = "", bool omitContentLength = false)
    {
        using var payload = new MemoryStream(body is null ? [] : JsonSerializer.SerializeToUtf8Bytes(body));
        var context = await factory.Server.SendAsync(context =>
        {
            context.Connection.RemoteIpAddress = IPAddress.Parse(remote);
            context.Request.Scheme = "https";
            context.Request.Host = new HostString("localhost");
            context.Request.Method = method;
            context.Request.Path = $"/api/screen-share/receiver/{grant.Id}/{suffix}";
            context.Request.QueryString = new QueryString($"?window_id={window ?? grant.WindowId}&source_generation={generation ?? grant.SourceGeneration}{extraQuery}");
            context.Request.Headers.Authorization = "Bearer " + (token ?? grant.Token);
            if (origin.Length != 0) context.Request.Headers.Origin = origin;
            context.Request.ContentType = "application/json";
            if (!omitContentLength) context.Request.ContentLength = payload.Length;
            context.Request.Body = payload;
        });
        using var reader = new StreamReader(context.Response.Body);
        return new(context.Response.StatusCode, context.Response.Headers.ToDictionary(item => item.Key, item => item.Value.ToString(), StringComparer.OrdinalIgnoreCase),
            await reader.ReadToEndAsync());
    }

    private sealed record ReceiverResponse(int Status, Dictionary<string, string> Headers, string Body);

    private sealed class ReceiverClock : TimeProvider
    {
        private DateTimeOffset _now = new(2026, 10, 11, 0, 0, 0, TimeSpan.Zero);
        /// <summary>:returns: 仅由测试推进的业务时钟，不放宽生产超时。</summary>
        public override DateTimeOffset GetUtcNow() => _now;
        /// <summary>:param duration: 明确过期量。:returns: 不禁用真实后台清理。</summary>
        internal void Advance(TimeSpan duration) => _now += duration;
    }
}
