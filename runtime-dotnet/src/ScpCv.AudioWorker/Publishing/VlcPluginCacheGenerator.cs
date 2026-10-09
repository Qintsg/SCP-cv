// 显式发布阶段的官方插件索引生成；仅当前独占包，不播放、不使用用户配置、不覆盖已有索引。
using System.Security.Cryptography;
using LibVLCSharp.Shared;

namespace ScpCv.AudioWorker.Publishing;

public sealed record VlcPluginCacheResult(string RelativePath, long Bytes, int PluginDlls, string Sha256);

public static class VlcPluginCacheGenerator
{
    /// <summary>:param packageRoot: 必须为本工具所在发布根。:returns: 当前插件索引摘要，插件DLL与时间戳须保持。</summary>
    public static VlcPluginCacheResult Generate(string packageRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packageRoot);
        var root = Path.GetFullPath(packageRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var current = Path.GetFullPath(AppContext.BaseDirectory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (!string.Equals(root, current, StringComparison.OrdinalIgnoreCase))
            throw new IOException("插件索引只能在当前发布包目录生成。");
        if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("VLC_PLUGIN_PATH")))
            throw new IOException("存在额外插件搜索路径，拒绝写入未知目录。");
        var native = Path.Combine(root, "libvlc", "win-x64");
        var plugins = Path.Combine(native, "plugins"); var cache = Path.Combine(plugins, "plugins.dat");
        if (!Directory.Exists(plugins) || !File.Exists(Path.Combine(native, "libvlc.dll"))
            || !File.Exists(Path.Combine(native, "libvlccore.dll"))) throw new IOException("当前发布包缺少x64 LibVLC与插件目录。");
        AssertNoReparsePoints(root, native);
        if (File.Exists(cache)) throw new IOException("当前发布包已有插件索引，拒绝覆盖。");
        var before = CapturePlugins(plugins);
        if (before.Length == 0) throw new IOException("当前发布包没有插件DLL。");
        Core.Initialize(native);
        // 官方引擎仅生成所选包索引，ignore-config避免读取或自动保存用户VLC配置。
        using (var library = new LibVLC("--ignore-config", "--reset-plugins-cache")) { }
        var after = CapturePlugins(plugins);
        if (!before.SequenceEqual(after)) throw new IOException("索引生成期间插件DLL集合、内容或时间戳变化。");
        if (!File.Exists(cache) || new FileInfo(cache).Length == 0) throw new IOException("官方引擎未生成有效非空插件索引。");
        return new VlcPluginCacheResult(Path.GetRelativePath(root, cache), new FileInfo(cache).Length, before.Length, Hash(cache));
    }

    /// <summary>:param package: 当前包根。:param native: 固定原生树。:returns: 所选树不存在跳到包外的重解析点。</summary>
    private static void AssertNoReparsePoints(string package, string native)
    {
        foreach (var path in new[] { package, Path.Combine(package, "libvlc"), native })
            if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0) throw new IOException("当前发布包含重解析路径，拒绝生成索引。");
        var pending = new Stack<string>(); pending.Push(native);
        while (pending.TryPop(out var directory))
            foreach (var item in new DirectoryInfo(directory).EnumerateFileSystemInfos())
            {
                if ((item.Attributes & FileAttributes.ReparsePoint) != 0) throw new IOException("原生文件树含重解析路径，拒绝生成索引。");
                if ((item.Attributes & FileAttributes.Directory) != 0) pending.Push(item.FullName);
            }
    }

    /// <summary>:param root: 自有插件树。:returns: 相对名、UTC时间、长度与完整摘要，顺序稳定。</summary>
    private static string[] CapturePlugins(string root) => Directory.EnumerateFiles(root, "*.dll", SearchOption.AllDirectories)
        .OrderBy(x => x, StringComparer.Ordinal).Select(path =>
        {
            var file = new FileInfo(path);
            return $"{Path.GetRelativePath(root, path)}:{file.LastWriteTimeUtc.Ticks}:{file.Length}:{Hash(path)}";
        }).ToArray();

    /// <summary>:param path: 自有发布文件。:returns: 流式SHA256，不保留文件句柄。</summary>
    private static string Hash(string path)
    {
        using var file = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(file));
    }
}
