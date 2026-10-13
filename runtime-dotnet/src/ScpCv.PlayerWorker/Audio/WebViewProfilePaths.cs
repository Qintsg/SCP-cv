// 可信启动目录下按两个窗口和共享用途隔离资料；不接受用户API路径或静默回退既有资料。
using System.IO;

namespace ScpCv.PlayerWorker.Audio;

internal static class WebViewProfilePaths
{
    internal const string EnvironmentVariable = "SCP_CV_WEBVIEW_USER_DATA_ROOT";

    /// <summary>:param windowId: 两块大屏之一。:param shared: 独立共享环境。:param configuredRoot: 可选可信本地绝对目录。:returns: 仅路径，不创建目录或打开浏览器。</summary>
    internal static string For(int windowId, bool shared, string? configuredRoot)
    {
        if (windowId is not (1 or 2)) throw new ArgumentOutOfRangeException(nameof(windowId));
        var root = configuredRoot is null
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SCP-cv", "WebView2")
            : Validate(configuredRoot);
        return shared ? Path.Combine(root, "screen-share", $"player-{windowId}") : Path.Combine(root, $"player-{windowId}");
    }

    /// <summary>:param configured: 管理员提供的启动配置。:returns: 本地普通目录；相对、网络、根盘及链接拒绝，不回退。</summary>
    private static string Validate(string configured)
    {
        if (string.IsNullOrWhiteSpace(configured) || !Path.IsPathFullyQualified(configured) ||
            configured.Length < 3 || configured[1] != ':' || configured[2] is not ('/' or '\\'))
            throw new InvalidOperationException("WebView资料根目录必须为本地绝对目录。");
        var root = Path.GetFullPath(configured).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (root == Path.GetPathRoot(root)?.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
            throw new InvalidOperationException("WebView资料根目录不能为根盘。");
        for (var directory = new DirectoryInfo(root); directory is not null; directory = directory.Parent)
            if (directory.Exists && (directory.Attributes & FileAttributes.ReparsePoint) != 0)
                throw new InvalidOperationException("WebView资料目录包含链接，拒绝写入。");
        return root;
    }

    /// <summary>:param configuredRoot: 显式可信配置或默认模式。:param expected: 已选择目录。:param actual: SDK实际目录。:returns: 显式配置被外部SDK覆盖时拒绝加载内容。</summary>
    internal static void Verify(string? configuredRoot, string expected, string actual)
    {
        if (configuredRoot is null) return;
        if (!string.Equals(Path.GetFullPath(expected).TrimEnd('\\', '/'), Path.GetFullPath(actual).TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("WebView实际资料目录与可信配置不匹配，未加载内容。");
    }
}
