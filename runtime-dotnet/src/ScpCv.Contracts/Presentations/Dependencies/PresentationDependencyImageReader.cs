// 内置PE/元数据只读解析；从文件读取TFM/AssemblyRefs，不执行或Assembly.Load任何UNO代码。
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;

namespace ScpCv.Contracts.Presentations.Dependencies;

internal static class PresentationDependencyImageReader
{
    /// <summary>:param path: 仅内部使用的文件位置。:returns: 文件的纯值映像快照；不可解析则明确无效。</summary>
    internal static PresentationDependencyImage Read(string path)
    {
        try
        {
            using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var pe = new PEReader(file);
            if (pe.PEHeaders.PEHeader is null) return new();
            var architecture = pe.PEHeaders.CoffHeader.Machine switch
            {
                Machine.Amd64 => PresentationImageArchitecture.X64,
                Machine.I386 => PresentationImageArchitecture.X86,
                _ => PresentationImageArchitecture.Other,
            };
            var cor = pe.PEHeaders.CorHeader;
            var flags = cor?.Flags ?? 0;
            var result = new PresentationDependencyImage
            {
                IsValidImage = true, Architecture = architecture, HasManagedMetadata = pe.HasMetadata,
                IlOnly = (flags & CorFlags.ILOnly) != 0, Requires32Bit = (flags & CorFlags.Requires32Bit) != 0,
                Prefers32Bit = (flags & CorFlags.Prefers32Bit) != 0,
                IsDll = (pe.PEHeaders.CoffHeader.Characteristics & Characteristics.Dll) != 0,
                IsExecutableImage = (pe.PEHeaders.CoffHeader.Characteristics & Characteristics.ExecutableImage) != 0,
                HasNativeEntryPoint = pe.PEHeaders.PEHeader.AddressOfEntryPoint > 0,
            };
            if (!pe.HasMetadata) return result;
            var metadata = pe.GetMetadataReader();
            if (!metadata.IsAssembly) return new();
            var definition = metadata.GetAssemblyDefinition();
            var identity = Identity(metadata.GetString(definition.Name), definition.Version,
                metadata.GetString(definition.Culture), metadata.GetBlobBytes(definition.PublicKey), fullKey: true);
            var references = metadata.AssemblyReferences.Select(handle =>
            {
                var reference = metadata.GetAssemblyReference(handle);
                return Identity(metadata.GetString(reference.Name), reference.Version, metadata.GetString(reference.Culture),
                    metadata.GetBlobBytes(reference.PublicKeyOrToken), (reference.Flags & AssemblyFlags.PublicKey) != 0);
            }).ToArray();
            var token = cor?.EntryPointTokenOrRelativeVirtualAddress ?? 0;
            var managedEntry = (flags & CorFlags.NativeEntryPoint) == 0 && token != 0 &&
                MetadataTokens.EntityHandle(token).Kind == HandleKind.MethodDefinition &&
                (token & 0x00ffffff) > 0 && (token & 0x00ffffff) <= metadata.MethodDefinitions.Count;
            return result with
            {
                Identity = identity, References = references,
                TargetFramework = TargetFramework(metadata, definition), HasManagedEntryPoint = managedEntry,
                ProtocolVersion = PresentationDependencyProtocolVersionReader.Read(metadata),
                PublisherConfigurationFile = PublisherConfigurationFile(metadata),
            };
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or BadImageFormatException or
            ArgumentException or InvalidOperationException or NotSupportedException)
        { return new(); }
    }

    /// <summary>:param name: 清单名。:param version: 程序集版本。:param culture: 文化。:param key: 元数据token或公钥。:param fullKey: 公钥标志。:returns: 非执行的程序集身份。</summary>
    private static PresentationAssemblyIdentity Identity(string name, Version version, string culture, byte[] key, bool fullKey)
    {
        byte[] token;
        if (fullKey && key.Length > 0)
        {
            var assemblyName = new AssemblyName { Name = name, Version = version, CultureName = culture };
            assemblyName.SetPublicKey(key);
            token = assemblyName.GetPublicKeyToken() ?? [];
        }
        else token = key;
        return new(name, version.ToString(), culture, Convert.ToHexString(token).ToLowerInvariant());
    }

    /// <summary>:param metadata: 只读清单。:param definition: 程序集定义。:returns: 实际TargetFrameworkAttribute声明，不从文件名或程序集版本猜测。</summary>
    private static string TargetFramework(MetadataReader metadata, AssemblyDefinition definition)
    {
        foreach (var handle in definition.GetCustomAttributes())
        {
            var attribute = metadata.GetCustomAttribute(handle);
            if (attribute.Constructor.Kind != HandleKind.MemberReference) continue;
            var constructor = metadata.GetMemberReference((MemberReferenceHandle)attribute.Constructor);
            if (constructor.Parent.Kind != HandleKind.TypeReference) continue;
            var type = metadata.GetTypeReference((TypeReferenceHandle)constructor.Parent);
            if (metadata.GetString(type.Namespace) != "System.Runtime.Versioning" ||
                metadata.GetString(type.Name) != "TargetFrameworkAttribute") continue;
            var blob = metadata.GetBlobReader(attribute.Value);
            if (blob.ReadUInt16() != 1) return string.Empty;
            return blob.ReadSerializedString() ?? string.Empty;
        }
        return string.Empty;
    }

    /// <summary>:param metadata: 只读清单。:returns: 唯一ManifestResource明确关联的外部leaf配置；不猜目录中的任意config，不验证旧hash真实性。</summary>
    private static string? PublisherConfigurationFile(MetadataReader metadata)
    {
        var resources = metadata.ManifestResources.Take(2).ToArray();
        if (resources.Length != 1) return null;
        var resource = metadata.GetManifestResource(resources[0]);
        if (resource.Implementation.Kind != HandleKind.AssemblyFile) return null;
        var file = metadata.GetAssemblyFile((AssemblyFileHandle)resource.Implementation);
        if (file.ContainsMetadata) return null;
        var name = metadata.GetString(file.Name);
        if (string.IsNullOrWhiteSpace(name) || name.IndexOfAny(['/', '\\', ':']) >= 0 ||
            !name.EndsWith(".config", StringComparison.OrdinalIgnoreCase) || name is "." or "..") return null;
        return name;
    }
}
