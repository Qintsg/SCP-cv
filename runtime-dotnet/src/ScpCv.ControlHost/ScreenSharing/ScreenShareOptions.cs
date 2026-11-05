// 文件描述：共享接收地址、画质及 ICE 服务配置。项目：SCP-cv；文件：ScreenShareOptions.cs；作者：Qintsg；日期：2026-11-05。
namespace ScpCv.ControlHost.ScreenSharing;

public sealed class ScreenShareOptions
{
    public Uri ReceiverBaseUri { get; init; } = new("https://localhost:18443");
    public ScreenShareVideoPolicy Video { get; init; } = new();
    public ScreenShareIceServer[] IceServers { get; init; } = [];
    public int LocalStunPort { get; init; }

    /// <summary>沿用运行脚本的urls；显式覆盖仍必须是纯loopback HTTP(S)地址。</summary>
    public static ScreenShareOptions FromConfiguration(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var configured = configuration["ScreenSharing:ReceiverBaseUrl"];
        if (string.IsNullOrWhiteSpace(configured))
            configured = (configuration["urls"] ?? "https://localhost:18443").Split(';')[0].Trim();
        configured = configured.Replace("0.0.0.0", "127.0.0.1", StringComparison.Ordinal)
            .Replace("[::]", "[::1]", StringComparison.Ordinal)
            .Replace("://+:", "://127.0.0.1:", StringComparison.Ordinal)
            .Replace("://*:", "://127.0.0.1:", StringComparison.Ordinal);
        var options = new ScreenShareOptions { ReceiverBaseUri = new Uri(configured, UriKind.Absolute),
            Video = configuration.GetSection("ScreenSharing:Video").Get<ScreenShareVideoPolicy>() ?? new(),
            IceServers = configuration.GetSection("ScreenSharing:IceServers").Get<ScreenShareIceServer[]>() ?? [],
            LocalStunPort = configuration.GetValue<int>("ScreenSharing:LocalStunPort") };
        options.Validate();
        return options;
    }

    public void Validate()
    {
        Video.Validate();
        if (LocalStunPort is < 0 or > 65535) throw new InvalidOperationException("屏幕共享本机 STUN 端口无效。");
        if (IceServers is null || IceServers.Length > (LocalStunPort > 0 ? 3 : 4)) throw new InvalidOperationException("屏幕共享 ICE 服务数量无效。");
        foreach (var server in IceServers) server.Validate();
        if (!ReceiverBaseUri.IsAbsoluteUri || !ReceiverBaseUri.IsLoopback ||
            ReceiverBaseUri.Scheme is not ("http" or "https") || ReceiverBaseUri.AbsolutePath != "/" ||
            ReceiverBaseUri.UserInfo.Length != 0 || ReceiverBaseUri.Query.Length != 0 || ReceiverBaseUri.Fragment.Length != 0)
            throw new InvalidOperationException("屏幕共享接收地址必须是主机已监听的纯loopback HTTP(S)地址。 ");
    }

    /// <summary>:param host: 已通过主机过滤的实际访问主机名。:returns: 同一访问网络中的 STUN 地址及显式 ICE 配置。</summary>
    public ScreenShareIceServer[] ForHost(string? host)
    {
        if (LocalStunPort == 0) return IceServers;
        var name = host?.Trim('[', ']');
        var type = Uri.CheckHostName(name);
        if (type == UriHostNameType.Unknown) throw new InvalidOperationException("屏幕共享访问主机名无效。");
        if (type == UriHostNameType.IPv6) name = "[" + name + "]";
        return [.. IceServers, new() { Urls = [$"stun:{name}:{LocalStunPort}"] }];
    }
}
