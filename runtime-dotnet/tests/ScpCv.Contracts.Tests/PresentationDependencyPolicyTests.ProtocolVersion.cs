// 真实当前协议PE的literal读取与独立Net48快照门禁；不加载程序集，不将Net10文件冒称Net48。
using System.Buffers.Binary;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using ScpCv.Contracts.Presentations.Dependencies;

namespace ScpCv.Contracts.Tests;

public sealed partial class PresentationDependencyPolicyTests
{
    [Fact]
    public void PublicReaderDetectsProtocolLiteralVersionEvenWhenAssemblyIdentityDoesNotChange()
    {
        var source = Path.Combine(AppContext.BaseDirectory, "ScpCv.Presentation.Protocol.dll");
        Assert.True(File.Exists(source), "缺少正式项目引用复制的协议DLL，不能跳过真实PE门禁。");
        var original = File.ReadAllBytes(source);
        var oldVersion = (byte[])original.Clone();
        ReplaceProtocolLiteral(oldVersion, 1);
        using var fixture = new OwnedProtocolVersionImage(original);

        var current = fixture.ReadProtocolImage();
        Assert.Equal(".NETCoreApp,Version=v10.0", current.TargetFramework);
        Assert.Equal(ScpCv.Presentation.Protocol.PresentationRequestPolicy.ProtocolVersion, current.ProtocolVersion);
        fixture.WriteProtocol(oldVersion);
        var obsolete = fixture.ReadProtocolImage();
        Assert.True(obsolete.IsValidImage);
        Assert.Equal(current.Identity, obsolete.Identity);
        Assert.Equal(current.TargetFramework, obsolete.TargetFramework);
        Assert.Equal(1, obsolete.ProtocolVersion);

        // 运行门禁使用独立的Net48外部事实；真实Net10 PE仅证明常量提取，不替代TFM检查。
        var healthy = HealthySnapshot();
        var images = healthy.Images.ToDictionary(pair => pair.Key, pair => pair.Value);
        images["protocol"] = images["protocol"] with { ProtocolVersion = obsolete.ProtocolVersion };
        var report = Inspect(healthy with { Images = images });

        Assert.False(report.Available);
        var protocol = Assert.Single(report.Components, component => component.Id == "protocol");
        Assert.Equal("protocol_version_incompatible", protocol.ReasonCode);
        Assert.Equal(original, File.ReadAllBytes(source));
    }

    /// <summary>:param bytes: 正式DLL的自有副本。:param version: 注入旧版literal。:returns: 只改Int32常量blob，不改清单/TFM或原件。</summary>
    private static void ReplaceProtocolLiteral(byte[] bytes, int version)
    {
        using var pe = new PEReader(new MemoryStream(bytes, writable: false));
        var metadata = pe.GetMetadataReader();
        var typeHandle = Assert.Single(metadata.TypeDefinitions, handle =>
        {
            var type = metadata.GetTypeDefinition(handle);
            return metadata.GetString(type.Namespace) == "ScpCv.Presentation.Protocol" &&
                metadata.GetString(type.Name) == "PresentationRequestPolicy";
        });
        var fieldHandle = Assert.Single(metadata.GetTypeDefinition(typeHandle).GetFields(), handle =>
            metadata.GetString(metadata.GetFieldDefinition(handle).Name) == "ProtocolVersion");
        var field = metadata.GetFieldDefinition(fieldHandle);
        Assert.True((field.Attributes & FieldAttributes.Literal) != 0);
        Assert.False(field.GetDefaultValue().IsNil);
        var constant = metadata.GetConstant(field.GetDefaultValue());
        Assert.Equal(ConstantTypeCode.Int32, constant.TypeCode);
        var blob = metadata.GetBlobReader(constant.Value);
        Assert.Equal(4, blob.Length);
        Assert.Equal(ScpCv.Presentation.Protocol.PresentationRequestPolicy.ProtocolVersion, blob.ReadInt32());
        var blobOffset = pe.PEHeaders.MetadataStartOffset + metadata.GetHeapMetadataOffset(HeapIndex.Blob) +
            MetadataTokens.GetHeapOffset(constant.Value);
        Assert.Equal((byte)4, bytes[blobOffset]);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(blobOffset + 1, 4), version);
    }

    private sealed class OwnedProtocolVersionImage : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), "scp-cv-protocol-image-" + Guid.NewGuid().ToString("N"));
        private readonly string _host;
        private readonly string _protocol;

        /// <summary>:param protocol: 项目正式协议副本；占位host仅供路径发现，绝不执行。</summary>
        internal OwnedProtocolVersionImage(byte[] protocol)
        {
            Directory.CreateDirectory(_root);
            _host = Path.Combine(_root, "ScpCv.LibreOfficeHost.exe");
            _protocol = Path.Combine(_root, "ScpCv.Presentation.Protocol.dll");
            File.WriteAllText(_host, "owned-readable-host-placeholder-never-executed");
            WriteProtocol(protocol);
        }

        /// <summary>:param bytes: 自有副本。:returns: 只更新本次测试根中的协议文件。</summary>
        internal void WriteProtocol(byte[] bytes) => File.WriteAllBytes(_protocol, bytes);

        /// <summary>:returns: 公共SnapshotReader读取的协议事实，不调用private解析器或执行程序集。</summary>
        internal PresentationDependencyImage ReadProtocolImage()
        {
            var previousHost = Environment.GetEnvironmentVariable("SCP_CV_LIBREOFFICE_HOST");
            var previousProgram = Environment.GetEnvironmentVariable("SCP_CV_LIBREOFFICE_PROGRAM");
            try
            {
                Environment.SetEnvironmentVariable("SCP_CV_LIBREOFFICE_HOST", _host);
                Environment.SetEnvironmentVariable("SCP_CV_LIBREOFFICE_PROGRAM", _root);
                return new WindowsPresentationDependencySnapshotReader().Read().Images["protocol"];
            }
            finally
            {
                Environment.SetEnvironmentVariable("SCP_CV_LIBREOFFICE_HOST", previousHost);
                Environment.SetEnvironmentVariable("SCP_CV_LIBREOFFICE_PROGRAM", previousProgram);
            }
        }

        /// <summary>:returns: 仅清理已核对的自有临时根；正式输出DLL保持不变。</summary>
        public void Dispose()
        {
            var root = Path.GetFullPath(_root);
            if (Path.GetDirectoryName(root) != Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar))
                throw new InvalidOperationException("拒绝清理协议PE测试根之外的路径。");
            Directory.Delete(root, recursive: true);
        }
    }
}
