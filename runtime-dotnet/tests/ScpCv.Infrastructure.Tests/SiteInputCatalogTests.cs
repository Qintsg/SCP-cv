// 固定设备表、别名与受信配置解析；纯CPU，不连接现场。
using ScpCv.Domain.Model;
using ScpCv.Infrastructure.Devices;
using ScpCv.Infrastructure.VideoWall;

namespace ScpCv.Infrastructure.Tests;

public sealed class SiteInputCatalogTests
{
    [Fact]
    public void ExactUserTableContainsTwentyFourWorkstationsAndNoSeventyFive()
    {
        Assert.Equal(31, SiteInputCatalog.All.Count);
        var stations = SiteInputCatalog.All.Where(item => item.Group == "workstation").ToArray();
        Assert.Equal(24, stations.Length);
        Assert.Equal(Enumerable.Range(51, 24), stations.Select(item => item.IpSuffix));
        Assert.Equal("D1-1", stations[0].Name);
        Assert.Equal("D6-4", stations[^1].Name);
        Assert.Null(SiteInputCatalog.ByAddress("192.168.5.75"));
        Assert.Equal("camera:fixed", SiteInputCatalog.ByAddress("192.168.5.76")!.Id);
        Assert.All(SiteInputCatalog.All.Where(item => item.IpSuffix != 81), item => { Assert.Equal(3840, item.Width); Assert.Equal(2160, item.Height); });
    }

    [Theory]
    [InlineData("window_1", 63)]
    [InlineData("window_2", 64)]
    [InlineData("window_3", 65)]
    [InlineData("window_4", 66)]
    [InlineData("laptop", 81)]
    public void AliasResolvesSameExplicitDescriptor(string kind, int suffix)
    {
        var options = new VideoWallProtocolOptions();
        var alias = SiteInputCatalog.ResolveDescriptor(kind, null, options)!;
        var direct = SiteInputCatalog.ResolveDescriptor("ip_stream", $"192.168.5.{suffix}", options)!;
        Assert.Equal(alias.UnicastAddress, direct.UnicastAddress);
        Assert.Equal(alias.MulticastAddress, direct.MulticastAddress);
        Assert.Equal(suffix == 81 ? 1920 : 3840, alias.Width);
        Assert.Equal(suffix == 81 ? 1080 : 2160, alias.Height);
        alias.Validate();
    }

    [Fact]
    public void UnknownAndConflictingFieldsAreNotInvented()
    {
        var options = new VideoWallProtocolOptions();
        Assert.Null(SiteInputCatalog.ResolveDescriptor("window_3", "192.168.5.70", options));
        Assert.Null(SiteInputCatalog.ResolveDescriptor("ip_stream", "192.168.5.90", options));
        Assert.Null(SiteInputCatalog.ResolveDescriptor("unknown", null, options));
        Assert.Equal(2, WindowId.Maximum);
    }

    [Fact]
    public void AdministratorDescriptorRemainsAuthoritative()
    {
        var descriptor = SiteInputCatalog.All[0].Descriptor();
        var options = new VideoWallProtocolOptions();
        options.Inputs["window_3"] = descriptor;
        Assert.Same(descriptor, SiteInputCatalog.ResolveDescriptor(new WallInput(WallInputKind.Window3), options));
    }
}
