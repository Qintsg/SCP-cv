// 静态接收资源嵌入发布包；CSP哈希随自有脚本变化，无用户脚本/信令插值。SCP-cv 2026-10-10。
using System.Security.Cryptography;
using System.Text;

namespace ScpCv.ControlHost.ScreenSharing;

internal static class ReceiverPage
{
    private const string Style = "html,body{margin:0;width:100%;height:100%;overflow:hidden;background:#000}video{display:block;width:100%;height:100%;object-fit:contain}";
    private static readonly string Script = ReadResource("receiver.js");
    internal static string Html { get; } = ReadResource("ReceiverPage.html").Replace("{{style}}", Style, StringComparison.Ordinal)
        .Replace("{{script}}", Script, StringComparison.Ordinal);
    internal static string ContentSecurityPolicy { get; } = $"default-src 'none'; script-src 'sha256-{Hash(Script)}'; " +
        $"style-src 'sha256-{Hash(Style)}'; connect-src 'self'; media-src blob:; base-uri 'none'; form-action 'none'; frame-ancestors 'none'";

    private static string ReadResource(string name)
    {
        using var stream = typeof(ReceiverPage).Assembly.GetManifestResourceStream("ScpCv.ControlHost.ScreenSharing." + name)
            ?? throw new InvalidOperationException("分享接收资源未打包。");
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    private static string Hash(string value) => Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
