// 播放/转换共用的明确依赖路径发现；发布优先同目录，开发仅查已知项目输出。
namespace ScpCv.Contracts.Presentations;

public static class PresentationRuntimePaths
{
    /// <summary>:param instance: 本次独占引擎身份。:returns: 与媒体路径解耦的全新profile位置，不创建目录。</summary>
    public static string EngineProfile(Guid instance) => EngineProfile(Path.GetTempPath(), instance);

    /// <summary>:param tempRoot: OS临时目录。:param instance: 本次引擎身份。:returns: 保守短路径，预留LibreOffice内部扩展目录空间。</summary>
    internal static string EngineProfile(string tempRoot, Guid instance)
    {
        if (instance == Guid.Empty) throw new ArgumentException("引擎实例身份不能为空。", nameof(instance));
        var profile = Path.GetFullPath(Path.Combine(tempRoot, "SCP-cv", "presentations", instance.ToString("N")));
        // 本机长profile启动已失败；120是保守工程预算，不是LibreOffice的通用路径上限。
        if (profile.Length > 120) throw new InvalidOperationException("LibreOffice临时profile路径过长，请将TMP/TEMP配置到短的本机目录。");
        return profile;
    }

    /// <summary>:returns: 官方安装目录，可用本地环境变量覆盖。</summary>
    public static string OfficeProgram => Environment.GetEnvironmentVariable("SCP_CV_LIBREOFFICE_PROGRAM")
        ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "LibreOffice", "program");

    /// <summary>:returns: 已构建Framework宿主，缺失时给可读失败。</summary>
    public static string HostExecutable()
    {
        var configured = Environment.GetEnvironmentVariable("SCP_CV_LIBREOFFICE_HOST");
        if (!string.IsNullOrWhiteSpace(configured)) return Path.GetFullPath(configured);
        var isolated = Path.Combine(AppContext.BaseDirectory, "libreoffice", "ScpCv.LibreOfficeHost.exe");
        if (File.Exists(isolated)) return isolated;
        var direct = Path.Combine(AppContext.BaseDirectory, "ScpCv.LibreOfficeHost.exe");
        if (File.Exists(direct)) return direct;
        for (var parent = new DirectoryInfo(AppContext.BaseDirectory); parent is not null; parent = parent.Parent)
        {
            var candidate = Path.Combine(parent.FullName, "src", "ScpCv.LibreOfficeHost", "bin", "Release", "net48", "ScpCv.LibreOfficeHost.exe");
            if (File.Exists(candidate)) return candidate;
            var repositoryCandidate = Path.Combine(parent.FullName, "runtime-dotnet", "src", "ScpCv.LibreOfficeHost", "bin", "Release", "net48", "ScpCv.LibreOfficeHost.exe");
            if (File.Exists(repositoryCandidate)) return repositoryCandidate;
        }
        throw new FileNotFoundException("未构建LibreOffice Framework宿主，请设置SCP_CV_LIBREOFFICE_HOST。", direct);
    }
}
