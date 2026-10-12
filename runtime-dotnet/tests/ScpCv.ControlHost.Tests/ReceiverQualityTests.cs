// 共享诊断白名单与数值上界；不能经质量字段泄漏任意数据。
using System.Text.Json;
using ScpCv.ControlHost.ScreenSharing;

namespace ScpCv.ControlHost.Tests;

public sealed class ReceiverQualityTests
{
    [Fact]
    public void MissingIsUnknownAndActualFiniteValuesRoundTrip()
    {
        using var none = JsonDocument.Parse("{}");
        Assert.Null(ReceiverQuality.Read(none.RootElement));
        using var valid = JsonDocument.Parse("{\"quality\":{\"width\":1920,\"height\":1080,\"fps\":30,\"bitrate_mbps\":7.8}}");
        Assert.Equal(new ReceiverQuality(1920, 1080, 30, 7.8), ReceiverQuality.Read(valid.RootElement));
    }
    [Theory]
    [InlineData("{\"sdp\":\"secret\"}")]
    [InlineData("{\"width\":1.2}")]
    [InlineData("{\"width\":1e40}")]
    [InlineData("{\"bitrate_mbps\":-1}")]
    [InlineData("{\"fps\":241}")]
    public void InvalidOrExtraFieldsAreRejected(string json)
    {
        using var body = JsonDocument.Parse("{\"quality\":" + json + "}");
        Assert.Throws<ScreenShareException>(() => ReceiverQuality.Read(body.RootElement));
    }
}
