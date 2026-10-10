// 接收页使用主机实际监听的本机地址；不允许转向远程网页。SCP-cv 2026-10-10。
namespace ScpCv.ControlHost.ScreenSharing;

public sealed class ScreenShareOptions
{
    public Uri ReceiverBaseUri { get; init; } = new("https://localhost:18443");

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
        var options = new ScreenShareOptions { ReceiverBaseUri = new Uri(configured, UriKind.Absolute) };
        options.Validate();
        return options;
    }

    public void Validate()
    {
        if (!ReceiverBaseUri.IsAbsoluteUri || !ReceiverBaseUri.IsLoopback ||
            ReceiverBaseUri.Scheme is not ("http" or "https") || ReceiverBaseUri.AbsolutePath != "/" ||
            ReceiverBaseUri.UserInfo.Length != 0 || ReceiverBaseUri.Query.Length != 0 || ReceiverBaseUri.Fragment.Length != 0)
            throw new InvalidOperationException("屏幕共享接收地址必须是主机已监听的纯loopback HTTP(S)地址。 ");
    }
}
