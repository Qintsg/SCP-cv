// CLI bindingRedirect只读解析；禁止DTD/外链且有限文本，不把不明配置猜作版本兼容。
using System.Xml;
using System.Xml.Linq;

namespace ScpCv.Contracts.Presentations.Dependencies;

internal static class PresentationBindingConfigurationReader
{
    /// <summary>:param path: 默认或测试注入的实际machine.config位置。:returns: 不存在/无CLI相关覆盖可沿CLR默认确认；CLI或全局覆盖超出静态范围则false。</summary>
    internal static bool MachineHasNoUnsupportedCliOverride(string path)
    {
        try
        {
            using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var xml = XmlReader.Create(file, new XmlReaderSettings
            { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 65_536 });
            var document = XDocument.Load(xml, LoadOptions.None);
            if (document.Root?.Name != "configuration") return false;
            foreach (var element in document.Descendants())
            {
                if (element.Name.LocalName == "assemblyIdentity" &&
                    ((string?)element.Attribute("name"))?.StartsWith("cli_", StringComparison.Ordinal) == true) return false;
                if (element.Name.LocalName is "probing" or "qualifyAssembly") return false;
                if (element.Name.LocalName == "publisherPolicy" && element.Parent?.Name.LocalName != "dependentAssembly") return false;
            }
            return true;
        }
        catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
        { return true; }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or XmlException or NotSupportedException)
        { return false; }
    }

    /// <summary>:param path: 内部配置文件。:param publisher: 是否为GAC publisher policy。:param expectedName: publisher限定的程序集。:param redirects: 本次绑定快照。:param disabled: app明确关闭的publisher项。:returns: 能否静态确认配置，无文件保持CLR默认行为。</summary>
    internal static bool Read(string path, bool publisher, string? expectedName, List<PresentationBindingRedirect> redirects, List<string> disabled, string? publisherFamily = null)
    {
        try
        {
            var initialRedirectCount = redirects.Count;
            using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var xml = XmlReader.Create(file, new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 65_536,
            });
            var document = XDocument.Load(xml, LoadOptions.None);
            XNamespace assemblyNamespace = "urn:schemas-microsoft-com:asm.v1";
            if (document.Root?.Name != "configuration") return false;
            var runtimes = document.Root.Elements("runtime").Take(2).ToArray();
            if (runtimes.Length > 1) return false;
            var runtime = runtimes.SingleOrDefault();
            if (document.Descendants().Any(element => element.Name.LocalName == "runtime" && element != runtime)) return false;
            var bindings = runtime?.Elements(assemblyNamespace + "assemblyBinding").Take(2).ToArray() ?? [];
            if (bindings.Length > 1 || document.Descendants().Any(element => element.Name.LocalName is "codeBase" or "probing" or "qualifyAssembly")) return false;
            var binding = bindings.SingleOrDefault();
            if (binding?.Attribute("appliesTo") is not null) return false;
            if (document.Descendants().Any(element => element.Name.LocalName == "assemblyBinding" && element != binding)) return false;
            if (binding is not null && binding.Elements().Any(element => element.Name != assemblyNamespace + "dependentAssembly" && element.Name != assemblyNamespace + "publisherPolicy")) return false;
            var globalPolicies = binding?.Elements(assemblyNamespace + "publisherPolicy").Take(2).ToArray() ?? [];
            if (globalPolicies.Length > 1 || publisher && globalPolicies.Length > 0) return false;
            foreach (var policy in globalPolicies)
            {
                if (policy.HasElements || policy.Attributes().Any(attribute => !attribute.IsNamespaceDeclaration && attribute.Name != "apply")) return false;
                var apply = (string?)policy.Attribute("apply");
                if (apply is not ("no" or "yes")) return false;
                if (apply == "no") disabled.AddRange(PresentationCliBindingPolicy.RequiredNames);
            }
            if (publisher && binding is null) return false;
            if (document.Descendants().Any(element => element.Name.LocalName == "dependentAssembly" && element.Parent != binding)) return false;
            foreach (var assembly in binding?.Elements(assemblyNamespace + "dependentAssembly") ?? [])
            {
                if (assembly.Elements().Any(element => element.Name != assemblyNamespace + "assemblyIdentity" &&
                    element.Name != assemblyNamespace + "bindingRedirect" && element.Name != assemblyNamespace + "publisherPolicy")) return false;
                var identities = assembly.Elements(assemblyNamespace + "assemblyIdentity").ToArray();
                if (identities.Length != 1) return false;
                var identity = identities[0];
                var name = (string?)identity.Attribute("name") ?? string.Empty;
                if (!name.StartsWith("cli_", StringComparison.Ordinal)) continue;
                if (identity.Attribute("processorArchitecture") is not null) return false;
                if (!PresentationCliBindingPolicy.RequiredNames.Contains(name, StringComparer.Ordinal) || publisher && name != expectedName) return false;
                var token = ((string?)identity.Attribute("publicKeyToken") ?? string.Empty).ToLowerInvariant();
                var culture = (string?)identity.Attribute("culture") ?? string.Empty;
                if (culture == "neutral") culture = string.Empty;
                if (!publisher && assembly.Elements(assemblyNamespace + "publisherPolicy").Any(element => (string?)element.Attribute("apply") == "no")) disabled.Add(name);
                foreach (var redirect in assembly.Elements(assemblyNamespace + "bindingRedirect"))
                {
                    var old = ((string?)redirect.Attribute("oldVersion") ?? string.Empty).Split('-');
                    var next = (string?)redirect.Attribute("newVersion") ?? string.Empty;
                    if (old.Length is < 1 or > 2 || !Version.TryParse(old[0], out var low) ||
                        !Version.TryParse(old[^1], out var high) || high < low || !Version.TryParse(next, out _)) return false;
                    redirects.Add(new(name, culture, token, old[0], old[^1], next, publisher) { PublisherFamily = publisherFamily });
                }
            }
            return !publisher || redirects.Skip(initialRedirectCount).Any(redirect => redirect.IsPublisherPolicy && redirect.Name == expectedName);
        }
        catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
        { return !publisher; }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or XmlException or NotSupportedException)
        { return false; }
    }
}
