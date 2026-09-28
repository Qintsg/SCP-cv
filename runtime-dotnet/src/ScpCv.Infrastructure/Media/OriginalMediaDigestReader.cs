// 原件摘要的只读文件流边界，供显式旧 PPT 重试与受控外部 I/O 回归使用。
using System.Security.Cryptography;

namespace ScpCv.Infrastructure.Media;

public interface IOriginalMediaDigestReader
{
    /// <summary>
    /// 只读计算选定原件的 SHA-256，不修改原件或扫描其它媒体。
    /// :param path: 已登记原件的路径。
    /// :param cancellationToken: 取消令牌。
    /// :returns: 小写十六进制 SHA-256。
    /// </summary>
    Task<string> ReadSha256Async(string path, CancellationToken cancellationToken = default);
}

public sealed class OriginalMediaDigestReader : IOriginalMediaDigestReader
{
    /// <summary>
    /// 从共享只读流计算原件摘要，读取期间不允许其它进程写入或删除。
    /// :param path: 已登记原件的路径。
    /// :param cancellationToken: 取消令牌。
    /// :returns: 小写十六进制 SHA-256。
    /// </summary>
    public async Task<string> ReadSha256Async(string path, CancellationToken cancellationToken = default)
    {
        await using var stream = File.OpenRead(path);
        var digest = await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false);
        return Convert.ToHexString(digest).ToLowerInvariant();
    }
}
