// 接收租约的不可变身份；仅允许已授权的本机文档，不回显私有URI或cap。
using System.Globalization;
using System.IO;

namespace ScpCv.PlayerWorker.ScreenSharing;

internal sealed class ScreenShareReceiverIdentity
{
    private ScreenShareReceiverIdentity(Uri address, Guid shareId, int windowId, long generation)
    { Address = address; ShareId = shareId; WindowId = windowId; SourceGeneration = generation; }

    internal Uri Address { get; }
    internal Guid ShareId { get; }
    internal int WindowId { get; }
    internal long SourceGeneration { get; }
    internal string Authorization => "Bearer " + Address.Fragment[1..];

    /// <summary>:param address: 仅来自已认证lease的私有地址。:param shareId: 持久分享ID。:param windowId: 当前Worker窗口。:param generation: 租约代次。:returns: 严格匹配的接收身份。</summary>
    internal static ScreenShareReceiverIdentity Parse(string address, Guid shareId, int windowId, long generation)
    {
        if (shareId == Guid.Empty || windowId is not (1 or 2) || generation <= 0 ||
            !Uri.TryCreate(address, UriKind.Absolute, out var uri) || !uri.IsLoopback ||
            uri.Scheme is not ("http" or "https") || uri.UserInfo.Length != 0 ||
            uri.AbsolutePath != $"/api/screen-share/receiver/{shareId:D}/" ||
            uri.Query != string.Create(CultureInfo.InvariantCulture, $"?window_id={windowId}&source_generation={generation}") ||
            uri.Fragment.Length != 65 || uri.Fragment[1..].Any(c => c is not (>= '0' and <= '9' or >= 'a' and <= 'f')))
            throw new InvalidDataException("屏幕共享接收身份无效或越界。");
        return new(uri, shareId, windowId, generation);
    }

    /// <summary>:param source: WebMessage的原生来源。:returns: 只允许同一接收文档；容许脚本移除原fragment，不允许换路由/查询/来源。</summary>
    internal bool IsDocument(string source) => Uri.TryCreate(source, UriKind.Absolute, out var uri) &&
        uri.UserInfo.Length == 0 && (uri.Fragment.Length == 0 || uri.Fragment == Address.Fragment) &&
        Uri.Compare(Address, uri, UriComponents.SchemeAndServer | UriComponents.PathAndQuery, UriFormat.UriEscaped, StringComparison.Ordinal) == 0;

    /// <summary>:returns: 日志仅含非秘密身份；不得使用默认Uri字符串输出授权。</summary>
    public override string ToString() => $"ScreenShareReceiver({ShareId:D}, window={WindowId}, generation={SourceGeneration})";
}
