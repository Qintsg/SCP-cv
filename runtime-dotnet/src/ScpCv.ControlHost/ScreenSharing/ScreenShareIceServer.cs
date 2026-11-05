// 文件描述：共享连接的标准 ICE 服务配置。项目：SCP-cv；文件：ScreenShareIceServer.cs；作者：Qintsg；日期：2026-11-05。
using System.Text.Json.Serialization;

namespace ScpCv.ControlHost.ScreenSharing;

public sealed class ScreenShareIceServer
{
    public string[] Urls { get; init; } = [];
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Username { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Credential { get; init; }

    /// <summary>:returns: 在主机启动时拒绝无效配置，不输出凭据。</summary>
    public void Validate()
    {
        if (Urls is null || Urls.Length is < 1 or > 4 || Urls.Any(url => url is null || url.Length > 512 ||
            !Uri.TryCreate(url, UriKind.Absolute, out var address) || address.Scheme is not ("stun" or "stuns" or "turn" or "turns")))
            throw new InvalidOperationException("屏幕共享 ICE 服务地址无效。");
        if (Username?.Length > 256 || Credential?.Length > 512 ||
            (string.IsNullOrWhiteSpace(Username) != string.IsNullOrWhiteSpace(Credential)))
            throw new InvalidOperationException("屏幕共享 ICE 服务凭据配置无效。");
    }
}
