// 文件描述：共享视频策略的真实配置、登记和序列化验证。项目：SCP-cv；文件：ScreenShareVideoPolicyTests.cs；作者：Qintsg；日期：2026-10-27。
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using ScpCv.ControlHost.ScreenSharing;
using ScpCv.Infrastructure.Playback;

namespace ScpCv.ControlHost.Tests;

public sealed class ScreenShareVideoPolicyTests
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };
    [Fact]
    public void DefaultPolicyIsPublishedWithTheReservation()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        { ["urls"] = "http://127.0.0.1:18444" }).Build();
        var options = ScreenShareOptions.FromConfiguration(configuration);
        var registry = new ScreenShareRegistry(TimeProvider.System, options);
        var share = registry.Create("policy-test", new ScreenShareTargetIdentity(1, Guid.NewGuid(), 1, 1), false);
        Assert.Equal(16_000_000, share.VideoPolicy.FullHdBitrate);
        Assert.Equal(24_000_000, share.VideoPolicy.QhdBitrate);
        Assert.Equal(48_000_000, share.VideoPolicy.UltraHdBitrate);
        Assert.Equal(30, share.VideoPolicy.MaxFrameRate);
        Assert.Equal(share.VideoPolicy, registry.GetOwned("policy-test", share.Id).VideoPolicy);
        var json = JsonSerializer.Serialize(share, JsonOptions);
        using var parsed = JsonDocument.Parse(json);
        var policy = parsed.RootElement.GetProperty("video_policy");
        Assert.Equal(48_000_000, policy.GetProperty("ultra_hd_bitrate").GetInt32());
        var export = Environment.GetEnvironmentVariable("SCP_CV_POLICY_EXPORT");
        if (!string.IsNullOrWhiteSpace(export)) File.WriteAllText(export, policy.GetRawText());
    }

    [Fact]
    public void HostConfigurationOverridesAllVideoParameters()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["urls"] = "http://127.0.0.1:18444",
            ["ScreenSharing:Video:FullHdBitrate"] = "18000000",
            ["ScreenSharing:Video:QhdBitrate"] = "30000000",
            ["ScreenSharing:Video:UltraHdBitrate"] = "60000000",
            ["ScreenSharing:Video:MaxFrameRate"] = "25",
        }).Build();
        var options = ScreenShareOptions.FromConfiguration(configuration);
        Assert.Equal(new ScreenShareVideoPolicy { FullHdBitrate = 18_000_000, QhdBitrate = 30_000_000, UltraHdBitrate = 60_000_000, MaxFrameRate = 25 }, options.Video);
    }

    [Theory]
    [InlineData(0, 24_000_000, 48_000_000, 30)]
    [InlineData(16_000_000, 8_000_000, 48_000_000, 30)]
    [InlineData(16_000_000, 24_000_000, 48_000_000, 0)]
    [InlineData(16_000_000, 24_000_000, 201_000_000, 30)]
    public void InvalidPolicyFailsAtConfiguration(int fullHd, int qhd, int ultraHd, int fps) =>
        Assert.Throws<InvalidOperationException>(() => new ScreenShareVideoPolicy
        { FullHdBitrate = fullHd, QhdBitrate = qhd, UltraHdBitrate = ultraHd, MaxFrameRate = fps }.Validate());
}
