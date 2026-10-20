// 来源解析与共享身份回归：仅只读内存/独立测试数据库，绝不连接设备。
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ScpCv.ControlHost.StreamPreview;
using ScpCv.Infrastructure.Persistence;
using ScpCv.Infrastructure.VideoWall;

namespace ScpCv.ControlHost.Tests;

public sealed class StreamPreviewSourceTests
{
    [Fact]
    public void SharedIdentityIncludesCredentialsButNotRequestedQualityOrAlias()
    {
        var first = new PreviewSource("rtmp://operator:test-one@192.0.2.1/live/chn1", "low", 1, 1);
        var second = new PreviewSource("rtmp://operator:test-one@192.0.2.1:1935/live/chn1", "high", 2, 8);
        Assert.Equal(first.Key, second.Key);
        Assert.NotEqual(first.Key, new PreviewSource("rtmp://operator:test-two@192.0.2.1/live/chn1", "low").Key);
        Assert.NotEqual(first.Key, new PreviewSource("rtmp://operator:test-one@192.0.2.1/live/chn0", "low").Key);
        Assert.DoesNotContain("test-one", first.ToString(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("http://192.0.2.1/private")]
    [InlineData("file:///C:/secret")]
    [InlineData("rtmp://192.0.2.1/live/a#fragment")]
    public void UnsupportedTransportCannotBecomeArbitraryProxy(string uri) =>
        Assert.Throws<PreviewException>(() => new PreviewSource(uri, "low"));

    [Fact]
    public async Task KnownWallInputsUseRequestedChannelAndReservedOutputsNeverGuessAddressesAsync()
    {
        using var factory = new ControlHostApplicationFactory();
        var resolver = new PreviewSourceResolver(factory.Services.GetRequiredService<IDbContextFactory<ControlDbContext>>(), new VideoWallProtocolOptions(), new PreviewOptions());
        Assert.Equal("rtmp://192.168.5.63:1935/live/chn1", (await resolver.ResolveAsync(new(Input: new("window_1")))).UpstreamUri);
        Assert.Equal("rtmp://192.168.5.64:1935/live/chn0", (await resolver.ResolveAsync(new(Input: new("window_2"), Quality: "high"))).UpstreamUri);
        await Assert.ThrowsAsync<PreviewException>(() => resolver.ResolveAsync(new(Input: new("window_3"))));
        await Assert.ThrowsAsync<PreviewException>(() => resolver.ResolveAsync(new(Input: new("ip_stream", "127.0.0.1"))));
        await Assert.ThrowsAsync<PreviewException>(() => resolver.ResolveAsync(new(SourceId: 1, Input: new("window_1"))));
        await Assert.ThrowsAsync<PreviewException>(() => resolver.ResolveAsync(new(Input: new("window_1", "192.0.2.1"))));
    }

    [Fact]
    public void OptionsLimitResourcesAndManagementApiToLoopback()
    {
        new PreviewOptions().Validate();
        Assert.Throws<InvalidOperationException>(() => new PreviewOptions { ApiBase = "http://192.0.2.1:9997" }.Validate());
        Assert.Throws<InvalidOperationException>(() => new PreviewOptions { MaximumLeases = 100000 }.Validate());
        Assert.Throws<InvalidOperationException>(() => new PreviewOptions { EgressKilobitsPerSecond = 0 }.Validate());
    }
}
