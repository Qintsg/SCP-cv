// 官方五CLI与实际宿主/选中程序集引用闭包；允许已确认redirect，不按首DLL或最新版本猜测。
namespace ScpCv.Contracts.Presentations.Dependencies;

internal static class PresentationCliBindingPolicy
{
    internal static readonly string[] RequiredNames = ["cli_basetypes", "cli_ure", "cli_uretypes", "cli_oootypes", "cli_cppuhelper"];
    private const string OfficialToken = "ce2cb7e279207b9e";

    /// <summary>:param snapshot: 文件/清单/绑定事实。:returns: 官方CLI所有必需项及传递绑定的同一结果，不加载UNO。</summary>
    internal static IEnumerable<PresentationDependencyComponent> Evaluate(PresentationDependencySnapshot snapshot)
    {
        var failures = new Dictionary<string, string>(StringComparer.Ordinal);
        var selected = new Dictionary<string, PresentationCliCandidate>(StringComparer.Ordinal);
        var pending = new Queue<PresentationAssemblyIdentity>();
        if (snapshot.Images.TryGetValue("host", out var host)) EnqueueCliReferences(host.References, pending);
        if (snapshot.Images.TryGetValue("cli_uno", out var bridge)) EnqueueCliReferences(bridge.References, pending);
        var visited = new HashSet<PresentationAssemblyIdentity>();
        while (pending.TryDequeue(out var requested))
        {
            if (!visited.Add(requested)) continue;
            if (!RequiredNames.Contains(requested.Name, StringComparer.Ordinal))
            {
                failures["cli_bindings"] = "cli_binding_unconfirmed";
                continue;
            }
            var version = RedirectedVersion(requested, snapshot, out var confirmed);
            if (!confirmed || snapshot.BindingConfigurationUnconfirmed)
            { failures[requested.Name] = "cli_binding_unconfirmed"; continue; }
            var candidates = snapshot.CliCandidates.Where(candidate => candidate.Name == requested.Name).ToArray();
            if (candidates.Length == 0)
            { failures[requested.Name] = "cli_binding_missing"; continue; }
            var matched = candidates.Where(candidate => candidate.Image.Identity is { } identity &&
                identity.Name == requested.Name && identity.Version == version && identity.Culture == requested.Culture &&
                identity.PublicKeyToken == requested.PublicKeyToken && identity.PublicKeyToken == OfficialToken).ToArray();
            if (matched.Length != 1)
            { failures[requested.Name] = matched.Length == 0 ? "cli_binding_incompatible" : "cli_binding_ambiguous"; continue; }
            var target = matched[0];
            if (!Compatible(target)) { failures[requested.Name] = "cli_image_incompatible"; continue; }
            if (selected.TryGetValue(requested.Name, out var previous) && previous.Image.Identity != target.Image.Identity)
            { failures[requested.Name] = "cli_binding_ambiguous"; continue; }
            selected[requested.Name] = target;
            EnqueueCliReferences(target.Image.References, pending);
        }
        foreach (var name in RequiredNames)
        {
            var code = failures.GetValueOrDefault(name);
            if (code is null && !selected.ContainsKey(name)) code = snapshot.CliCandidates.Any(candidate => candidate.Name == name)
                ? "cli_binding_unconfirmed" : "cli_binding_missing";
            yield return new(name, code is null ? "installed" : "unavailable", code is null
                ? $"{name}身份、架构及实际引用绑定静态检查通过。"
                : $"{name}官方运行绑定缺失、不兼容或尚未确认，请检查官方.NET互操作安装及宿主绑定配置。",
                code ?? "cli_binding_compatible");
        }
        if (failures.TryGetValue("cli_bindings", out var closureCode))
            yield return new("cli_bindings", "unavailable", "实际引用包含未确认的CLI绑定，不能静态宣称可用。", closureCode);
    }

    /// <summary>:param references: 已选清单引用。:param pending: 当前绑定待查队列。:returns: 将CLI引用闭包纳入检查，不要求宿主直引全部五个。</summary>
    private static void EnqueueCliReferences(IEnumerable<PresentationAssemblyIdentity> references, Queue<PresentationAssemblyIdentity> pending)
    {
        foreach (var reference in references.Where(reference => reference.Name.StartsWith("cli_", StringComparison.Ordinal))) pending.Enqueue(reference);
    }

    /// <summary>:param candidate: 已按强身份选中的候选。:returns: MSIL四项与混合helper的真实x64兼容种类。</summary>
    private static bool Compatible(PresentationCliCandidate candidate)
    {
        var image = candidate.Image;
        if (candidate.FileState != PresentationDependencyFileState.Readable || !image.IsValidImage || !image.HasManagedMetadata || !image.IsDll || image.Requires32Bit) return false;
        if (candidate.Name == "cli_cppuhelper") return image.Architecture == PresentationImageArchitecture.X64 && !image.IlOnly;
        return image.IlOnly && !image.Prefers32Bit && image.Architecture is PresentationImageArchitecture.X86 or PresentationImageArchitecture.X64;
    }

    /// <summary>:param requested: CLR请求身份。:param snapshot: 已读取绑定声明。:param confirmed: 版本选择是否唯一可确认。:returns: 不猜测的实际绑定版本。</summary>
    private static string RedirectedVersion(PresentationAssemblyIdentity requested, PresentationDependencySnapshot snapshot, out bool confirmed)
    {
        confirmed = true;
        var version = requested.Version;
        foreach (var publisher in new[] { false, true })
        {
            if (publisher && snapshot.PublisherPolicyDisabled.Contains(requested.Name, StringComparer.Ordinal)) continue;
            if (!Version.TryParse(version, out var current)) { confirmed = false; return version; }
            var policies = snapshot.BindingRedirects.Where(redirect => redirect.IsPublisherPolicy == publisher &&
                redirect.Name == requested.Name && redirect.PublicKeyToken == requested.PublicKeyToken && redirect.Culture == requested.Culture).ToArray();
            if (publisher && policies.Any(redirect => !Version.TryParse(redirect.PublisherFamily, out var family) ||
                family.Build >= 0 || family.ToString(2) != redirect.PublisherFamily))
            { confirmed = false; return version; }
            var applicable = policies.Where(redirect => (!publisher || redirect.PublisherFamily == current.ToString(2)) &&
                Version.TryParse(redirect.OldVersionLow, out var low) && Version.TryParse(redirect.OldVersionHigh, out var high) && current >= low && current <= high).ToArray();
            var targets = applicable.Select(redirect => redirect.NewVersion).Distinct(StringComparer.Ordinal).ToArray();
            if (targets.Length > 1 || targets.Length == 1 && !Version.TryParse(targets[0], out _))
            { confirmed = false; return version; }
            if (targets.Length == 1) version = targets[0];
        }
        return version;
    }
}
