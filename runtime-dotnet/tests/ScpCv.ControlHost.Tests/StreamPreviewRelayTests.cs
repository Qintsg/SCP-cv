// 按需中继API合同：假回环HTTP处理器，不启动MediaMTX或触碰摄像机。
using System.Net;
using System.Text.Json;
using ScpCv.ControlHost.StreamPreview;

namespace ScpCv.ControlHost.Tests;

public sealed class StreamPreviewRelayTests
{
    [Fact]
    public async Task RegistersOnlyOwnedOnDemandPathAndDisposesExactPathAsync()
    {
        using var handler = new RelayHandler(); using var http = new HttpClient(handler);
        var relay = new MediaMtxPreviewClient(http, new());
        var path = await relay.RegisterAsync(new("rtmp://operator:fake-secret@192.0.2.1/live/chn1", "low"), CancellationToken.None);
        var first = Assert.Single(handler.Calls);
        Assert.Equal("POST", first.Method); Assert.StartsWith("/v3/config/paths/add/scp-preview-", first.Path, StringComparison.Ordinal);
        using var body = JsonDocument.Parse(first.Body);
        Assert.True(body.RootElement.GetProperty("sourceOnDemand").GetBoolean());
        Assert.Equal("1s", body.RootElement.GetProperty("sourceOnDemandCloseAfter").GetString());
        Assert.DoesNotContain("fake-secret", path.LocalRtspUri, StringComparison.Ordinal);
        Assert.DoesNotContain("192.0.2.1", path.LocalRtspUri, StringComparison.Ordinal);
        await relay.CleanupPendingAsync(); Assert.Single(handler.Calls);
        await path.DisposeAsync(); Assert.Equal(0, relay.OwnedPathCount);
        Assert.Equal(first.Path.Replace("/add/", "/delete/", StringComparison.Ordinal), handler.Calls[1].Path);
        await path.DisposeAsync(); Assert.Equal(2, handler.Calls.Count);
    }

    [Fact]
    public async Task UncertainRegistrationCleansOnlyItsNonceAndReturnsFixedErrorAsync()
    {
        using var handler = new RelayHandler { FailPost = true }; using var http = new HttpClient(handler);
        var relay = new MediaMtxPreviewClient(http, new());
        var failure = await Assert.ThrowsAsync<PreviewException>(() => relay.RegisterAsync(new("rtmp://operator:fake-secret@192.0.2.1/live/chn1", "low"), CancellationToken.None));
        Assert.Equal("preview_relay_unavailable", failure.Code);
        Assert.DoesNotContain("fake-secret", failure.ToString(), StringComparison.Ordinal);
        Assert.Equal(2, handler.Calls.Count); Assert.Equal(0, relay.OwnedPathCount);
    }

    [Fact]
    public async Task FailedDeleteStaysPendingAndNeverDeletesActivePathAsync()
    {
        using var handler = new RelayHandler(); using var http = new HttpClient(handler);
        var relay = new MediaMtxPreviewClient(http, new());
        var one = await relay.RegisterAsync(new("rtmp://192.0.2.1/live/one", "low"), CancellationToken.None);
        var two = await relay.RegisterAsync(new("rtmp://192.0.2.1/live/two", "low"), CancellationToken.None);
        handler.FailDelete = true;
        await Assert.ThrowsAsync<PreviewException>(async () => await one.DisposeAsync());
        Assert.Equal(2, relay.OwnedPathCount); handler.FailDelete = false;
        await relay.CleanupPendingAsync(); Assert.Equal(1, relay.OwnedPathCount);
        await two.DisposeAsync(); Assert.Equal(0, relay.OwnedPathCount);
    }

    [Fact]
    public async Task MissingDynamicPathAfterRelayRestartOnlyRecreatesSameOwnedNonceAsync()
    {
        using var handler = new RelayHandler(); using var http = new HttpClient(handler);
        using var relay = new MediaMtxPreviewClient(http, new());
        var source = new PreviewSource("rtmp://192.0.2.1/live/one", "low");
        await using var path = await relay.RegisterAsync(source, CancellationToken.None);
        handler.PathMissing = true; await path.EnsureAsync(source, CancellationToken.None);
        Assert.Equal(3, handler.Calls.Count); Assert.Equal("GET", handler.Calls[1].Method);
        Assert.Equal(handler.Calls[0].Path, handler.Calls[2].Path); Assert.Equal(1, relay.OwnedPathCount);
    }

    private sealed class RelayHandler : HttpMessageHandler
    {
        public bool FailPost { get; set; }
        public bool FailDelete { get; set; }
        public bool PathMissing { get; set; }
        public List<(string Method, string Path, string Body)> Calls { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls.Add((request.Method.Method, request.RequestUri!.AbsolutePath,
                request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken)));
            if (request.Method == HttpMethod.Post && FailPost) throw new HttpRequestException("合成网络失败");
            if (request.Method == HttpMethod.Get && PathMissing) return new HttpResponseMessage(HttpStatusCode.NotFound);
            return new HttpResponseMessage(request.Method == HttpMethod.Delete && FailDelete ? HttpStatusCode.ServiceUnavailable : HttpStatusCode.OK);
        }
    }
}
