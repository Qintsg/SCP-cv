// 私有音频引擎的可信启动边界；无显式输出配置不加载副本，缺失不回退系统默认。
using System;
using System.IO;

namespace ScpCv.Runtime.Audio;

internal static class PrivateAudioEnginePaths
{
    internal const string EnvironmentVariable = "SCP_CV_PRIVATE_AUDIO_ENGINE_ROOT";
    internal const string MarkerName = "private-audio-engines.marker";
    internal const string Marker = "SCP-cv private audio engines v1";

    /// <summary>:param windowId: 仅大屏1/2。:param endpoint: 明确输出。:returns: 未配置为null，已配置必须找到专用浏览器。</summary>
    internal static string? WebView(int windowId, string? endpoint)
    {
        if (string.IsNullOrWhiteSpace(endpoint)) return null;
        var root = Root();
        if (windowId != 1 && windowId != 2) throw new InvalidOperationException("私有声音引擎仅支持窗口1/2。");
        var folder = Path.Combine(root, "window-" + windowId, "webview");
        ValidateImage(Path.Combine(folder, "msedgewebview2.exe"), root);
        return Path.GetFullPath(folder);
    }

    /// <summary>:param windowId: 仅大屏1/2。:param endpoint: 明确输出。:returns: 未配置为null，否则对应窗口的私有Office。</summary>
    internal static string? Office(int windowId, string? endpoint) =>
        Office(windowId, endpoint, Environment.GetEnvironmentVariable(EnvironmentVariable));

    /// <summary>:param windowId: 窗口身份。:param endpoint: 明确输出。:param configured: 可信根或测试独立根。:returns: 完整私有program目录，不回退安装目录。</summary>
    internal static string? Office(int windowId, string? endpoint, string? configured)
    {
        if (string.IsNullOrWhiteSpace(endpoint)) return null;
        if (windowId != 1 && windowId != 2) throw new InvalidOperationException("私有声音引擎仅支持窗口1/2。");
        var root = Root(configured);
        var folder = Path.Combine(root, "window-" + windowId, "libreoffice", "program");
        foreach (var name in new[] { "soffice.exe", "soffice.bin", "uno.ini" }) ValidateImage(Path.Combine(folder, name), root);
        return Path.GetFullPath(folder);
    }

    /// <summary>:returns: 可信绝对根与生成标记；不接受链接到系统安装目录的替身。</summary>
    internal static string Root() => Root(Environment.GetEnvironmentVariable(EnvironmentVariable));

    /// <summary>:param configured: 可信启动配置；独立参数便于不改全局环境的边界测试。:returns: 标记匹配的私有根。</summary>
    internal static string Root(string? configured)
    {
        // Path.IsPathRooted仍接受E:目录和\目录；限定盘符绝对路径，同时拒绝正斜杠UNC，首个文件系统访问前完成。
        if (configured is null || string.IsNullOrWhiteSpace(configured) || configured.Length < 3 ||
            !((configured[0] >= 'A' && configured[0] <= 'Z') || (configured[0] >= 'a' && configured[0] <= 'z')) ||
            configured[1] != ':' || (configured[2] != '\\' && configured[2] != '/'))
            throw new InvalidOperationException("明确声音输出需要配置项目专用引擎根目录。");
        var root = Path.GetFullPath(configured);
        ValidatePlainPath(root);
        var marker = Path.Combine(root, MarkerName);
        if (!File.Exists(marker) || new FileInfo(marker).Length > 4096) throw new InvalidOperationException("缺少项目专用引擎标记。");
        ValidatePlainPath(marker);
        using var reader = new StreamReader(marker);
        if (reader.ReadLine() != Marker) throw new InvalidOperationException("项目专用引擎标记不匹配。");
        return root;
    }

    /// <summary>:param image: 预期原生映像。:param root: 私有根。:returns: 普通文件且完全位于专用目录下。</summary>
    internal static void ValidateImage(string image, string root)
    {
        var path = Path.GetFullPath(image);
        var boundary = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!path.StartsWith(boundary, StringComparison.OrdinalIgnoreCase) || !File.Exists(path))
            throw new InvalidOperationException("私有声音引擎文件缺失或超出受信根。");
        ValidatePlainPath(path);
    }

    /// <summary>:param path: 可信普通路径。:returns: 逐级拒绝重解析链接，不跟随到原安装。</summary>
    private static void ValidatePlainPath(string path)
    {
        string? current = Path.GetFullPath(path);
        while (!string.IsNullOrEmpty(current))
        {
            if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                throw new InvalidOperationException("私有声音引擎目录不能包含重解析链接。");
            current = Path.GetDirectoryName(current);
        }
    }
}
