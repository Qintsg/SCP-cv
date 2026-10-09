// 纯值依赖判定与外部快照读取分开；运行诊断不要求SDK或Framework targeting pack。
namespace ScpCv.Contracts.Presentations.Dependencies;

public sealed class PresentationDependencyProbe(IPresentationDependencySnapshotReader snapshotReader) : IPresentationDependencyProbe
{
    /// <summary>:returns: 同一外部快照的组件和整体结果，不触发安装或进程启动。</summary>
    public PresentationDependencyReport Inspect() => Evaluate(snapshotReader.Read());

    /// <summary>:param snapshot: 外部只读事实。:returns: 不泄配置值的文件门禁报告。</summary>
    public static PresentationDependencyReport Evaluate(PresentationDependencySnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var provided = snapshot.Files.ToLookup(file => file.Id, StringComparer.Ordinal);
        var files = PresentationDependencyRequirements.Files.Select(required =>
        {
            var matches = provided[required.Id].Take(2).ToArray();
            if (matches.Length == 0) return new PresentationDependencyComponent(required.Id, "unavailable",
                $"缺少{required.Label}运行依赖事实，无法只读确认。", "dependency_fact_missing");
            if (matches.Length != 1) return new PresentationDependencyComponent(required.Id, "unavailable",
                $"{required.Label}运行依赖事实重复，无法只读确认。", "dependency_fact_ambiguous");
            var file = matches[0] with { Label = required.Label };
            return file.State == PresentationDependencyFileState.Readable && PresentationRuntimeDependencyPolicy.Image(file, snapshot) is { } image
                ? image : FileComponent(file);
        });
        return new PresentationDependencyReport(files.Concat(PresentationRuntimeDependencyPolicy.Runtime(snapshot))
            .Concat(PresentationCliBindingPolicy.Evaluate(snapshot)));
    }

    /// <summary>:param file: 外部文件读取结果。:returns: 仅从文件事实生成组件原因，不泄漏路径。</summary>
    private static PresentationDependencyComponent FileComponent(PresentationDependencyFile file) => new(
            file.Id,
            file.State == PresentationDependencyFileState.Readable ? "installed" : "unavailable",
            file.State switch
            {
                PresentationDependencyFileState.Readable => $"{file.Label}可读取。",
                PresentationDependencyFileState.Missing => $"缺少{file.Label}，请检查本机LibreOffice安装及宿主配置。",
                PresentationDependencyFileState.Empty => $"{file.Label}为空文件，请检查本机安装。",
                PresentationDependencyFileState.Unreadable => $"{file.Label}无法读取，请检查访问权限。",
                _ => $"{file.Label}配置路径无效，请检查SCP_CV_LIBREOFFICE_HOST/SCP_CV_LIBREOFFICE_PROGRAM。",
            },
            file.State switch
            {
                PresentationDependencyFileState.Readable => "file_readable",
                PresentationDependencyFileState.Missing => "file_missing",
                PresentationDependencyFileState.Empty => "file_empty",
                PresentationDependencyFileState.Unreadable => "file_unreadable",
                _ => "invalid_path",
            });
}
