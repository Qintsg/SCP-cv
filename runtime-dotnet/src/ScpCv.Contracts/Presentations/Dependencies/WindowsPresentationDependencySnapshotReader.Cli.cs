// GAC候选与签名身份publisher policy只读采集；按实际metadata读取，不取首DLL或执行配置。
using System.Security;

namespace ScpCv.Contracts.Presentations.Dependencies;

public sealed partial class WindowsPresentationDependencySnapshotReader
{
    private sealed record CliFacts(IReadOnlyList<PresentationCliCandidate> Candidates, IReadOnlyList<PresentationBindingRedirect> Redirects,
        IReadOnlyList<string> Disabled, bool Unconfirmed);

    /// <summary>:param host: 明确宿主位置。:returns: 当前候选及绑定声明；仅读取有限GAC版本与CLI相关配置。</summary>
    private static CliFacts ReadCli(string? host)
    {
        var candidates = new List<PresentationCliCandidate>();
        var redirects = new List<PresentationBindingRedirect>();
        var disabled = new List<string>();
        var confirmed = host is null || PresentationBindingConfigurationReader.Read(host + ".config", false, null, redirects, disabled);
        if (!OperatingSystem.IsWindows()) return new(candidates, redirects, disabled, !confirmed);
        var assemblyRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Microsoft.NET", "assembly");
        foreach (var name in PresentationCliBindingPolicy.RequiredNames)
        {
            var gac = Path.Combine(assemblyRoot, name == "cli_cppuhelper" ? "GAC_64" : "GAC_MSIL");
            try
            {
                var root = Path.Combine(gac, name);
                if (Directory.Exists(root))
                {
                    var versions = Directory.EnumerateDirectories(root, "*", SearchOption.TopDirectoryOnly).Take(65).ToArray();
                    if (versions.Length > 64) confirmed = false;
                    foreach (var version in versions.Take(64))
                    {
                        var path = Path.Combine(version, name + ".dll");
                        var file = ReadFile(name, name, path);
                        candidates.Add(new(name, file.State, file.State == PresentationDependencyFileState.Readable
                            ? PresentationDependencyImageReader.Read(path) : new()));
                    }
                }
                if (disabled.Contains(name, StringComparer.Ordinal)) continue;
                var policyRoots = Directory.Exists(gac)
                    ? Directory.EnumerateDirectories(gac, $"policy.*.{name}", SearchOption.TopDirectoryOnly).Take(33).ToArray() : [];
                if (policyRoots.Length > 32) confirmed = false;
                foreach (var policyRoot in policyRoots.Take(32))
                {
                    var versions = Directory.EnumerateDirectories(policyRoot, "*", SearchOption.TopDirectoryOnly).Take(33).ToArray();
                    if (versions.Length > 32) confirmed = false;
                    foreach (var version in versions.Take(32))
                    {
                        var policyName = Path.GetFileName(policyRoot);
                        var policyImage = PresentationDependencyImageReader.Read(Path.Combine(version, policyName + ".dll"));
                        if (!policyImage.IsValidImage || !policyImage.HasManagedMetadata || !policyImage.IsDll ||
                            policyImage.Identity?.Name != policyName || policyImage.Identity.PublicKeyToken != "ce2cb7e279207b9e")
                        { confirmed = false; continue; }
                        var linkedConfig = policyImage.PublisherConfigurationFile;
                        if (linkedConfig is null) { confirmed = false; continue; }
                        var family = policyName[7..(policyName.Length - name.Length - 1)];
                        if (!Version.TryParse(family, out var parsedFamily) || parsedFamily.Build >= 0 || parsedFamily.ToString(2) != family)
                        { confirmed = false; continue; }
                        confirmed &= PresentationBindingConfigurationReader.Read(Path.Combine(version, linkedConfig), true, name, redirects, disabled, family);
                    }
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or SecurityException or ArgumentException or NotSupportedException)
            { confirmed = false; }
        }
        return new(candidates, redirects, disabled, !confirmed);
    }
}
