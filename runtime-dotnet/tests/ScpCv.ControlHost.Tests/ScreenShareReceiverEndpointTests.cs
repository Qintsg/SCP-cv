// 接收HTTP只认本机高熵cap，不认发布者cookie；这些模拟帧不是原生出画证据。SCP-cv 2026-10-10。
using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using ScpCv.ControlHost.ScreenSharing;

namespace ScpCv.ControlHost.Tests;

public sealed class ScreenShareReceiverEndpointTests
{
    [Fact]
    public async Task ReceiverPageIsStaticNoStoreAndRequiresHeaderCapabilityEvenOnLoopback()
    {
        using var factory = new ControlHostApplicationFactory();
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
        using var factory = new ControlHostApplicationFactory();
        var (grant, _) = await ArrangeAsync(factory);
        Assert.Equal(404, (await ExchangeAsync(factory, grant, "offer/", "GET", remote: remote, origin: origin)).Status);
    }

    [Fact]
    public async Task TokenCannotReadOtherWindowOrGenerationAndUserCookieIsNotReceiverAuthority()
    {
        using var factory = new ControlHostApplicationFactory();
        var (grant, _) = await ArrangeAsync(factory);
        Assert.Equal(404, (await ExchangeAsync(factory, grant, "offer/", "GET", window: 2)).Status);
        Assert.Equal(404, (await ExchangeAsync(factory, grant, "offer/", "GET", generation: grant.SourceGeneration + 1)).Status);
        using var client = factory.CreateHttpsClient();
        await ScreenShareEndpointTests.LoginAsync(client);
        using var cookieOnly = await client.GetAsync($"/api/screen-share/receiver/{grant.Id}/offer/?window_id=1&source_generation={grant.SourceGeneration}");
        Assert.Equal(HttpStatusCode.NotFound, cookieOnly.StatusCode);
    }

    [Fact]
    public async Task AnswerIsBoundedAndSingleUseAndOnlyActivatedVideoFramesGrantReceivingState()
    {
        using var factory = new ControlHostApplicationFactory();
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
        using var factory = new ControlHostApplicationFactory();
        var (grant, registry) = await ArrangeAsync(factory);
        Assert.Equal(200, (await ExchangeAsync(factory, grant, "end/", "POST")).Status);
        Assert.Equal(ScreenShareState.Stopping, registry.GetOwned("test-owner", grant.Id).State);
        Assert.Equal(404, (await ExchangeAsync(factory, grant, "offer/", "GET")).Status);
        Assert.Equal(404, (await ExchangeAsync(factory, grant, "answer/", "POST", new { type = "answer", sdp = "v=0\r\n" })).Status);
    }

    [Fact]
    public async Task ChunkedHugeBodyIsRejectedAndInvalidNumericSchemaHasNoFrameSideEffect()
    {
        using var factory = new ControlHostApplicationFactory();
        var (grant, registry) = await ArrangeAsync(factory);
        Assert.Equal(413, (await ExchangeAsync(factory, grant, "answer/", "POST", new { type = "answer", sdp = new string('a', 400000) }, omitContentLength: true)).Status);
        Assert.Equal(400, (await ExchangeAsync(factory, grant, "frames/", "POST", new { frames = "1", activated = true })).Status);
        Assert.Equal(0, registry.GetOwned("test-owner", grant.Id).ActualFrames);
    }

    private static async Task<(ScreenShareReceiverGrant Grant, ScreenShareRegistry Registry)> ArrangeAsync(ControlHostApplicationFactory factory)
    {
        using var client = factory.CreateHttpsClient();
        var target = await ScreenShareEndpointTests.SeedOnlineAsync(factory);
        var registry = factory.Services.GetRequiredService<ScreenShareRegistry>();
        var share = registry.Create("test-owner", target, false);
        registry.SetOffer("test-owner", share.Id, "offer", "v=0\r\n");
        var runtime = factory.Services.GetRequiredService<ScpCv.Infrastructure.Playback.RuntimeStateService>();
        await runtime.OpenScreenShareAsync(target, share.Id, generation => registry.BindReceiver("test-owner", share.Id, generation));
        return (registry.GetReceiverGrant(share.Id, target, registry.GetOwned("test-owner", share.Id).SourceGeneration!.Value), registry);
    }

    /// <summary>显式构造传输地址，TestServer的默认null远端不能假装为loopback。</summary>
    private static async Task<ReceiverResponse> ExchangeAsync(ControlHostApplicationFactory factory, ScreenShareReceiverGrant grant,
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
}
