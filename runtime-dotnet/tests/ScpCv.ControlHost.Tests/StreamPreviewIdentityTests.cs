// 共享身份只公开服务器私钥HMAC，完整登录/来源围栏与凭据不得简化为公开URI摘要。
using ScpCv.ControlHost.StreamPreview;

namespace ScpCv.ControlHost.Tests;

public sealed class StreamPreviewIdentityTests
{
    [Fact]
    public void IdentitySharesActualUriOnlyInsideCompleteOwnerAndOrigin()
    {
        using var keys = new PreviewSharingIdentity();
        var first = new PreviewSource("rtmp://fixture:test-only@192.0.2.1/live/chn1", "low", 1, 2);
        var alias = new PreviewSource("rtmp://fixture:test-only@192.0.2.1:1935/live/chn1", "high", 3, 9);
        var key = keys.Create("alice-cookie1", "https://console.test", first);
        Assert.Matches("^[0-9a-f]{64}$", key);
        Assert.Equal(key, keys.Create("alice-cookie1", "https://console.test", alias));
        Assert.NotEqual(first.Key, key);
        Assert.NotEqual(key, keys.Create("alice-cookie2", "https://console.test", alias));
        Assert.NotEqual(key, keys.Create("alice-cookie1", "https://different.test", alias));
        Assert.NotEqual(key, keys.Create("alice-cookie1", "https://console.test", new("rtmp://fixture:other-only@192.0.2.1/live/chn1", "low")));
        Assert.NotEqual(key, keys.Create("alice-cookie1", "https://console.test", new("rtmp://fixture:test-only@192.0.2.1/live/chn0", "low")));
        using var restarted = new PreviewSharingIdentity();
        Assert.NotEqual(key, restarted.Create("alice-cookie1", "https://console.test", first));
    }
}
