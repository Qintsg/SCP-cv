// 接收地址只根据真实监听或显式本机配置派生，绝不导航到用户提供的远程页面。SCP-cv 2026-10-10。
using Microsoft.Extensions.Configuration;
using ScpCv.ControlHost.ScreenSharing;

namespace ScpCv.ControlHost.Tests;

public sealed class ScreenShareOptionsTests
{
    [Theory]
    [InlineData("http://0.0.0.0:18443", "http://127.0.0.1:18443/")]
    [InlineData("https://localhost:18443", "https://localhost:18443/")]
    [InlineData("http://[::]:18946", "http://[::1]:18946/")]
    public void ReceiverUsesLoopbackOfConfiguredListener(string urls, string expected)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["urls"] = urls }).Build();
        Assert.Equal(expected, ScreenShareOptions.FromConfiguration(configuration).ReceiverBaseUri.AbsoluteUri);
    }

    [Theory]
    [InlineData("https://attacker.invalid")]
    [InlineData("http://localhost/remote/")]
    [InlineData("file:///C:/test.html")]
    [InlineData("http://localhost/?token=x")]
    [InlineData("http://user@localhost/")]
    public void UnsafeExplicitReceiverAddressFailsInsteadOfWeakeningNavigationGate(string address)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["ScreenSharing:ReceiverBaseUrl"] = address }).Build();
        Assert.Throws<InvalidOperationException>(() => ScreenShareOptions.FromConfiguration(configuration));
    }
}
