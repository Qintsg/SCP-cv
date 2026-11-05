// 文件描述：实际 ICE 配置绑定、预约快照及序列化验证。项目：SCP-cv；文件：ScreenShareIceConfigurationTests.cs；作者：Qintsg；日期：2026-11-05。
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using ScpCv.ControlHost.ScreenSharing;
using ScpCv.Infrastructure.Playback;

namespace ScpCv.ControlHost.Tests;

public sealed class ScreenShareIceConfigurationTests
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };
    /// <summary>:returns: 主机设置经实际配置绑定和 Registry 进入授权发布者快照。</summary>
    [Fact]
    public void ConfiguredServersReachReservationWithoutChangingSdp()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["urls"] = "http://127.0.0.1:18444",
            ["ScreenSharing:IceServers:0:Urls:0"] = "stun:10.88.50.194:3478",
            ["ScreenSharing:IceServers:1:Urls:0"] = "turn:10.88.50.194:3478?transport=udp",
            ["ScreenSharing:IceServers:1:Username"] = "configuration-test",
            ["ScreenSharing:IceServers:1:Credential"] = "configuration-value",
        }).Build();
        var options = ScreenShareOptions.FromConfiguration(configuration);
        var registry = new ScreenShareRegistry(TimeProvider.System, options);
        var share = registry.Create("ice-configuration", new ScreenShareTargetIdentity(1, Guid.NewGuid(), 1, 1), false);
        Assert.Equal(2, share.IceServers.Length);
        Assert.Equal("stun:10.88.50.194:3478", share.IceServers[0].Urls[0]);
        Assert.Equal("configuration-test", registry.GetOwned("ice-configuration", share.Id).IceServers[1].Username);
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(share, JsonOptions));
        var servers = json.RootElement.GetProperty("ice_servers");
        Assert.False(servers[0].TryGetProperty("credential", out _));
        Assert.Equal("configuration-value", servers[1].GetProperty("credential").GetString());
    }

    /// <summary>:returns: 空配置保留原有局域网连接行为。</summary>
    [Fact]
    public void UnconfiguredServersRemainEmpty()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["urls"] = "http://127.0.0.1:18444" }).Build();
        Assert.Empty(ScreenShareOptions.FromConfiguration(configuration).IceServers);
    }

    /// <summary>:returns: 组网、现场及本机接收分别使用实际访问地址，不混用不可达网卡。</summary>
    [Theory]
    [InlineData("10.88.50.194", "stun:10.88.50.194:3478")]
    [InlineData("192.168.5.194", "stun:192.168.5.194:3478")]
    [InlineData("127.0.0.1", "stun:127.0.0.1:3478")]
    public void LocalDiscoveryUsesTheAccessNetwork(string host, string address)
    {
        var options = new ScreenShareOptions { LocalStunPort = 3478 };
        options.Validate();
        var registry = new ScreenShareRegistry(TimeProvider.System, options);
        var share = registry.Create("ice-local", new ScreenShareTargetIdentity(1, Guid.NewGuid(), 1, 1), false, host);
        Assert.Equal(address, Assert.Single(Assert.Single(share.IceServers).Urls));
    }

    /// <summary>:param address: 不支持的服务地址。:returns: 在启动配置校验时明确失败。</summary>
    [Theory]
    [InlineData("https://10.88.50.194/")]
    [InlineData("relative-address")]
    [InlineData("")]
    public void InvalidServerAddressIsRejected(string address) => Assert.Throws<InvalidOperationException>(() =>
        new ScreenShareOptions { IceServers = [new ScreenShareIceServer { Urls = [address] }] }.Validate());
}
