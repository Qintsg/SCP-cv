// 文稿运行依赖的纯值快照与诊断接口；公开报告不携带安装路径或环境配置值。
namespace ScpCv.Contracts.Presentations.Dependencies;

public enum PresentationDependencyFileState { Readable, Missing, Empty, Unreadable, InvalidPath }

public sealed record PresentationDependencyFile(string Id, string Label, PresentationDependencyFileState State);
public sealed record PresentationDependencySnapshot(IReadOnlyList<PresentationDependencyFile> Files)
{
    public PresentationRuntimePlatform? Platform { get; init; }
    public PresentationFrameworkRuntime? Framework { get; init; }
    public IReadOnlyDictionary<string, PresentationDependencyImage> Images { get; init; } = new Dictionary<string, PresentationDependencyImage>();
    public IReadOnlyList<PresentationCliCandidate> CliCandidates { get; init; } = [];
    public IReadOnlyList<PresentationBindingRedirect> BindingRedirects { get; init; } = [];
    public IReadOnlyList<string> PublisherPolicyDisabled { get; init; } = [];
    public bool BindingConfigurationUnconfirmed { get; init; }
}

public sealed record PresentationCliCandidate(string Name, PresentationDependencyFileState FileState, PresentationDependencyImage Image);
public sealed record PresentationBindingRedirect(string Name, string Culture, string PublicKeyToken,
    string OldVersionLow, string OldVersionHigh, string NewVersion, bool IsPublisherPolicy)
{
    public string? PublisherFamily { get; init; }
}

public enum PresentationImageArchitecture { X86, X64, Other }
public sealed record PresentationRuntimePlatform(bool IsWindows, bool IsX64OperatingSystem, bool IsX64Process);
public sealed record PresentationFrameworkRuntime(int? Release, bool ClrReadable, string ReadFailureCode = "");
public sealed record PresentationAssemblyIdentity(string Name, string Version, string Culture, string PublicKeyToken);

public sealed record PresentationDependencyImage
{
    public bool IsValidImage { get; init; }
    public PresentationImageArchitecture Architecture { get; init; }
    public bool HasManagedMetadata { get; init; }
    public bool IlOnly { get; init; }
    public bool Requires32Bit { get; init; }
    public bool Prefers32Bit { get; init; }
    public bool IsDll { get; init; }
    public bool IsExecutableImage { get; init; }
    public bool HasNativeEntryPoint { get; init; }
    public bool HasManagedEntryPoint { get; init; }
    public string TargetFramework { get; init; } = string.Empty;
    public int? ProtocolVersion { get; init; }
    public string? PublisherConfigurationFile { get; init; }
    public PresentationAssemblyIdentity? Identity { get; init; }
    public IReadOnlyList<PresentationAssemblyIdentity> References { get; init; } = [];
}
public sealed record PresentationDependencyComponent(string Id, string Status, string Detail, string ReasonCode);

public sealed class PresentationDependencyReport
{
    /// <summary>:param components: 本次只读诊断组件。:returns: 固定快照，后续读取不会受外部列表变更影响。</summary>
    public PresentationDependencyReport(IEnumerable<PresentationDependencyComponent> components)
    {
        ArgumentNullException.ThrowIfNull(components);
        Components = Array.AsReadOnly(components.ToArray());
    }

    public IReadOnlyList<PresentationDependencyComponent> Components { get; }
    public bool Available => Components.Count > 0 && Components.All(component => component.Status == "installed");
    public string Detail => Available
        ? "运行依赖静态检查通过；尚未验证UNO连接、具体文稿或实际出画。"
        : string.Join("；", Components.Where(component => component.Status != "installed").Select(component => component.Detail));
}

public interface IPresentationDependencyProbe
{
    /// <summary>:returns: 不启动Office、不加载UNO的同一次静态依赖报告。</summary>
    PresentationDependencyReport Inspect();
}

public interface IPresentationDependencySnapshotReader
{
    /// <summary>:returns: 只读外部文件/平台信息快照；测试可替换该外部适配，不替换判断策略。</summary>
    PresentationDependencySnapshot Read();
}
