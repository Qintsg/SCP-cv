// 运行依赖的外部只读适配；读取已存在文件，不创建profile、不执行EXE、不加载UNO程序集。
using System.Security;
using System.Runtime.InteropServices;

namespace ScpCv.Contracts.Presentations.Dependencies;

public sealed partial class WindowsPresentationDependencySnapshotReader : IPresentationDependencySnapshotReader
{
    /// <summary>:param machineConfigurationPath: 可替换的外部只读文件位置；HTTP不提供此入口，默认实际Framework64配置。</summary>
    public WindowsPresentationDependencySnapshotReader(string? machineConfigurationPath = null)
    {
        MachineConfigurationPath = machineConfigurationPath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Microsoft.NET", "Framework64", "v4.0.30319", "Config", "machine.config");
    }

    public string MachineConfigurationPath { get; }

    private static readonly (string Id, string RelativePath)[] OfficeFiles =
    [
        ("soffice_exe", "soffice.exe"), ("soffice_bin", "soffice.bin"), ("uno_ini", "uno.ini"),
        ("cli_uno", "cli_uno.dll"), ("types_rdb", "types.rdb"), ("services_rdb", "services.rdb"),
        ("offapi_rdb", Path.Combine("types", "offapi.rdb")),
    ];

    /// <summary>:returns: 当前已配置运行文件的读取快照，不打印原配置或路径。</summary>
    public PresentationDependencySnapshot Read()
    {
        var files = new List<PresentationDependencyFile>();
        var images = new Dictionary<string, PresentationDependencyImage>();
        string? host = null;
        try
        {
            host = PresentationRuntimePaths.HostExecutable();
            AddFile("host", "Framework文稿宿主", host, files, images);
        }
        catch (FileNotFoundException) { files.Add(new("host", "Framework文稿宿主", PresentationDependencyFileState.Missing)); }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or SecurityException)
        { files.Add(new("host", "Framework文稿宿主", PresentationDependencyFileState.InvalidPath)); }
        if (host is null) files.Add(new("protocol", "Framework文稿协议", PresentationDependencyFileState.Missing));
        else AddFile("protocol", "Framework文稿协议", Path.Combine(Path.GetDirectoryName(host)!, "ScpCv.Presentation.Protocol.dll"), files, images);
        var program = PresentationRuntimePaths.OfficeProgram;
        foreach (var (id, relativePath) in OfficeFiles)
        {
            try { AddFile(id, relativePath, Path.Combine(program, relativePath), files, images); }
            catch (Exception exception) when (exception is ArgumentException or NotSupportedException or SecurityException)
            { files.Add(new(id, relativePath, PresentationDependencyFileState.InvalidPath)); }
        }
        var cli = ReadCli(host);
        return new PresentationDependencySnapshot(files.ToArray())
        {
            Images = images,
            Platform = new(OperatingSystem.IsWindows(), RuntimeInformation.OSArchitecture == Architecture.X64,
                RuntimeInformation.ProcessArchitecture == Architecture.X64),
            Framework = OperatingSystem.IsWindows() ? ReadFramework() : new(null, false),
            CliCandidates = cli.Candidates, BindingRedirects = cli.Redirects,
            PublisherPolicyDisabled = cli.Disabled, BindingConfigurationUnconfirmed = cli.Unconfirmed ||
                !PresentationBindingConfigurationReader.MachineHasNoUnsupportedCliOverride(MachineConfigurationPath),
        };
    }

    /// <summary>:param id: 固定ID。:param label: 固定显示名。:param path: 内部路径。:param files: 当前读取快照。:param images: 当前PE快照。:returns: 读取文件，不执行或跨请求缓存。</summary>
    private static void AddFile(string id, string label, string path, List<PresentationDependencyFile> files, Dictionary<string, PresentationDependencyImage> images)
    {
        var file = ReadFile(id, label, path);
        files.Add(file);
        if (file.State == PresentationDependencyFileState.Readable && (id is "host" or "protocol" or "soffice_exe" or "soffice_bin" or "cli_uno"))
            images[id] = PresentationDependencyImageReader.Read(path);
    }

    /// <summary>:param id: 固定组件ID。:param label: 固定安装文件名。:param path: 仅内部使用的配置路径。:returns: 不携带路径的读取结果。</summary>
    private static PresentationDependencyFile ReadFile(string id, string label, string path)
    {
        try
        {
            using var file = new FileStream(Path.GetFullPath(path), FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            return new(id, label, file.Length > 0 && file.ReadByte() >= 0
                ? PresentationDependencyFileState.Readable : PresentationDependencyFileState.Empty);
        }
        catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
        { return new(id, label, PresentationDependencyFileState.Missing); }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or SecurityException)
        { return new(id, label, PresentationDependencyFileState.Unreadable); }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException)
        { return new(id, label, PresentationDependencyFileState.InvalidPath); }
    }
}
