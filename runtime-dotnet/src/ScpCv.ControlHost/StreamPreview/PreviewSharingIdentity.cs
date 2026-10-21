// 对外共享键只使用进程私钥HMAC；不暴露可供离线猜凭据的实际URI摘要。
using System.Security.Cryptography;
using System.Text;

namespace ScpCv.ControlHost.StreamPreview;

public sealed class PreviewSharingIdentity : IDisposable
{
    private readonly byte[] _secret = RandomNumberGenerator.GetBytes(32);

    /// <summary>:param owner: 完整登录票据归属。:param origin: 精确调用来源。:param source: 已授权实际流。:returns: 本进程/本身份范围内的非可逆共享键，不含清晰度提示或别名。</summary>
    public string Create(string owner, string origin, PreviewSource source)
    {
        ArgumentException.ThrowIfNullOrEmpty(owner); ArgumentException.ThrowIfNullOrEmpty(origin); ArgumentNullException.ThrowIfNull(source);
        var message = Encoding.UTF8.GetBytes($"{owner.Length}:{owner}{origin.Length}:{origin}{source.UpstreamUri}");
        try { return Convert.ToHexString(HMACSHA256.HashData(_secret, message)).ToLowerInvariant(); }
        finally { CryptographicOperations.ZeroMemory(message); }
    }

    /// <summary>:returns: 进程作用域结束后抹除自有密钥。</summary>
    public void Dispose() => CryptographicOperations.ZeroMemory(_secret);
}
