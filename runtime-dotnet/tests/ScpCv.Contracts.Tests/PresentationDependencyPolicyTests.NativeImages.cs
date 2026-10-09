// 公共SnapshotReader读取自有Net48 PE副本的入口/标志回归；不执行EXE，不要求LibreOffice。
using System.Buffers.Binary;
using System.Reflection.PortableExecutable;
using System.Runtime.InteropServices;
using ScpCv.Contracts.Presentations.Dependencies;

namespace ScpCv.Contracts.Tests;

[Collection(PresentationDependencyReaderEnvironmentGroup.Name)]
public sealed partial class PresentationDependencyPolicyTests
{
    private static readonly string[] ExpectedCliBindingNames = ["cli_basetypes", "cli_ure", "cli_uretypes", "cli_oootypes", "cli_cppuhelper"];

    [Theory]
    [InlineData("<configuration><runtime><assemblyBinding xmlns='wrong' /></runtime></configuration>")]
    [InlineData("<!DOCTYPE configuration [<!ENTITY owned 'x'>]><configuration />")]
    [InlineData("<configuration><runtime><assemblyBinding xmlns='urn:schemas-microsoft-com:asm.v1'><dependentAssembly><assemblyIdentity name='cli_basetypes' publicKeyToken='ce2cb7e279207b9e' /><codeBase version='1.0.20.0' href='owned.dll' /></dependentAssembly></assemblyBinding></runtime></configuration>")]
    [InlineData("<configuration><runtime><assemblyBinding xmlns='urn:schemas-microsoft-com:asm.v1'><probing privatePath='owned' /></assemblyBinding></runtime></configuration>")]
    [InlineData("<configuration><runtime><assemblyBinding xmlns='urn:schemas-microsoft-com:asm.v1'><publisherPolicy apply='unknown' /></assemblyBinding></runtime></configuration>")]
    [InlineData("<configuration><runtime><assemblyBinding xmlns='urn:schemas-microsoft-com:asm.v1' appliesTo='v2.0.50727'><dependentAssembly><assemblyIdentity name='cli_basetypes' publicKeyToken='ce2cb7e279207b9e' culture='neutral' /><bindingRedirect oldVersion='1.0.0.0-1.0.20.0' newVersion='1.0.20.0' /></dependentAssembly></assemblyBinding></runtime></configuration>")]
    [InlineData("<configuration><runtime><assemblyBinding xmlns='urn:schemas-microsoft-com:asm.v1'><dependentAssembly><assemblyIdentity name='cli_basetypes' publicKeyToken='ce2cb7e279207b9e' culture='neutral' processorArchitecture='x86' /><bindingRedirect oldVersion='1.0.0.0-1.0.20.0' newVersion='1.0.20.0' /></dependentAssembly></assemblyBinding></runtime></configuration>")]
    public void PublicReaderDoesNotConfirmUnsupportedOrMalformedHostBindingConfiguration(string configuration)
    {
        using var fixture = new OwnedDependencyImage(ReadOwnedNet48Host());
        fixture.WriteHostConfiguration(configuration);
        Assert.True(fixture.ReadSnapshot().BindingConfigurationUnconfirmed);
    }

    [Theory]
    [InlineData("<configuration />")]
    [InlineData("<configuration><runtime><assemblyBinding xmlns='urn:schemas-microsoft-com:asm.v1'><dependentAssembly><assemblyIdentity name='System.Xml' publicKeyToken='b77a5c561934e089' /><bindingRedirect oldVersion='4.0.0.0' newVersion='4.0.0.0' /></dependentAssembly></assemblyBinding></runtime></configuration>")]
    public void PublicReaderKeepsMachineConfigurationWithoutCliOverridesConfirmed(string configuration)
    {
        using var fixture = new OwnedDependencyImage(ReadOwnedNet48Host());
        var machinePath = fixture.WriteMachineConfiguration(configuration);
        Assert.False(fixture.ReadSnapshot(machinePath).BindingConfigurationUnconfirmed);
    }


    [Fact]
    public void PublicReaderDoesNotConfirmUnsupportedCliOverrideInMachineConfiguration()
    {
        using var fixture = new OwnedDependencyImage(ReadOwnedNet48Host());
        var machinePath = fixture.WriteMachineConfiguration("""
            <configuration><runtime><assemblyBinding xmlns="urn:schemas-microsoft-com:asm.v1">
              <dependentAssembly><assemblyIdentity name="cli_basetypes" publicKeyToken="ce2cb7e279207b9e" culture="neutral" />
                <bindingRedirect oldVersion="1.0.0.0-1.0.20.0" newVersion="1.0.999.0" />
              </dependentAssembly>
            </assemblyBinding></runtime></configuration>
            """);

        var snapshot = fixture.ReadSnapshot(machinePath);

        Assert.True(snapshot.BindingConfigurationUnconfirmed);
    }
    [Fact]
    public void PublicReaderHonorsGlobalPublisherPolicyDisabledForAllRequiredCliBindings()
    {
        using var fixture = new OwnedDependencyImage(ReadOwnedNet48Host());
        fixture.WriteHostConfiguration("""
            <configuration><runtime><assemblyBinding xmlns="urn:schemas-microsoft-com:asm.v1">
            <publisherPolicy apply="no" />
            </assemblyBinding></runtime></configuration>
            """);

        var snapshot = fixture.ReadSnapshot();

        Assert.False(snapshot.BindingConfigurationUnconfirmed);
        Assert.Equal(ExpectedCliBindingNames.Order(),
            snapshot.PublisherPolicyDisabled.Distinct(StringComparer.Ordinal).Order());
    }

    [Fact]
    public void PublicReaderDoesNotConfirmAnUnknownHostConfigurationRoot()
    {
        using var fixture = new OwnedDependencyImage(ReadOwnedNet48Host());
        Assert.False(fixture.ReadSnapshot().BindingConfigurationUnconfirmed,
            "无自有config时已有GAC配置尚未确认，需先诊断夹具前置，不能冒称错root业务红。");
        fixture.WriteHostConfiguration("<not-configuration />");

        var snapshot = fixture.ReadSnapshot();

        Assert.True(snapshot.BindingConfigurationUnconfirmed);
    }

    [Fact]
    public void PublicReaderX64FactsMatchActualOperatingSystemAndProcessArchitectures()
    {
        var snapshot = new WindowsPresentationDependencySnapshotReader().Read();
        Assert.NotNull(snapshot.Platform);
        Assert.Equal(OperatingSystem.IsWindows(), snapshot.Platform.IsWindows);
        Assert.Equal(RuntimeInformation.OSArchitecture == Architecture.X64, snapshot.Platform.IsX64OperatingSystem);
        Assert.Equal(RuntimeInformation.ProcessArchitecture == Architecture.X64, snapshot.Platform.IsX64Process);
    }

    [Fact]
    public void PublicReaderRejectsNilMethodDefinitionAsAValidManagedEntryPoint()
    {
        var bytes = ReadOwnedNet48Host();
        using (var pe = new PEReader(new MemoryStream(bytes, writable: false)))
        {
            Assert.NotNull(pe.PEHeaders.CorHeader);
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(pe.PEHeaders.CorHeaderStartOffset + 20, 4), 0x06000000);
        }
        using var fixture = new OwnedDependencyImage(bytes);

        var image = fixture.ReadHostImage();

        Assert.True(image.IsValidImage);
        Assert.True(image.HasManagedMetadata);
        Assert.False(image.HasManagedEntryPoint);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NativeOfficeRequiresExecutableImageFlagAndNonzeroEntryPoint(bool removeExecutableFlag)
    {
        var bytes = ReadOwnedNet48Host();
        using (var pe = new PEReader(new MemoryStream(bytes, writable: false)))
        {
            var optionalHeader = pe.PEHeaders.PEHeaderStartOffset;
            var directoriesOffset = pe.PEHeaders.PEHeader!.Magic == PEMagic.PE32Plus ? 112 : 96;
            // 删CLR数据目录，仅测试仍可读取的原生PE；从不执行此自有副本。
            bytes.AsSpan(optionalHeader + directoriesOffset + 14 * 8, 8).Clear();
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(optionalHeader + 16, 4), removeExecutableFlag ? 1 : 0);
            if (removeExecutableFlag)
            {
                var characteristicsOffset = pe.PEHeaders.CoffHeaderStartOffset + 18;
                var flags = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(characteristicsOffset, 2));
                BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(characteristicsOffset, 2), (ushort)(flags & ~0x0002));
            }
        }
        using var fixture = new OwnedDependencyImage(bytes);
        var image = fixture.ReadHostImage();
        Assert.True(image.IsValidImage);
        Assert.False(image.HasManagedMetadata);
        var healthy = HealthySnapshot();
        var images = healthy.Images.ToDictionary(pair => pair.Key, pair => pair.Value);
        images["soffice_exe"] = image;

        var report = Inspect(healthy with { Images = images });

        Assert.False(report.Available);
        Assert.Equal("native_entrypoint_invalid", Assert.Single(report.Components, component => component.Id == "soffice_exe").ReasonCode);
    }

    /// <summary>:returns: 项目构建复制的正式自有Net48测试宿主字节，不修改/执行原文件。</summary>
    private static byte[] ReadOwnedNet48Host()
    {
        var source = Path.Combine(AppContext.BaseDirectory, "ParentGuardTestHost", "ScpCv.ParentGuard.TestHost.exe");
        Assert.True(File.Exists(source), "缺少构建应复制的正式自有Net48测试宿主，不能跳过PE门禁。");
        return File.ReadAllBytes(source);
    }

    private sealed class OwnedDependencyImage : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), "scp-cv-dependency-image-" + Guid.NewGuid().ToString("N"));
        private readonly string _host;

        /// <summary>:param bytes: 自有修改副本字节，不是用户程序。</summary>
        internal OwnedDependencyImage(byte[] bytes)
        {
            Directory.CreateDirectory(_root);
            _host = Path.Combine(_root, "ScpCv.LibreOfficeHost.exe");
            File.WriteAllBytes(_host, bytes);
        }

        /// <summary>:returns: 正式公共Reader读取的host映像，不调用private解析器或执行进程。</summary>
        internal PresentationDependencyImage ReadHostImage() => ReadSnapshot().Images["host"];

        /// <summary>:param content: 自有host配置文本。:returns: 写入故障夹具，不读取或更改真实配置。</summary>
        internal void WriteHostConfiguration(string content) => File.WriteAllText(_host + ".config", content);

        /// <summary>:param content: 自有machine配置文本。:returns: 自有测试路径，不更改系统machine.config。</summary>
        internal string WriteMachineConfiguration(string content)
        {
            var path = Path.Combine(_root, "machine.config");
            File.WriteAllText(path, content);
            return path;
        }

        /// <summary>:returns: 正式公共Reader全部外部事实，不调用private解析器或运行Office。</summary>
        internal PresentationDependencySnapshot ReadSnapshot(string? machineConfigurationPath = null)
        {
            var previousHost = Environment.GetEnvironmentVariable("SCP_CV_LIBREOFFICE_HOST");
            var previousProgram = Environment.GetEnvironmentVariable("SCP_CV_LIBREOFFICE_PROGRAM");
            try
            {
                Environment.SetEnvironmentVariable("SCP_CV_LIBREOFFICE_HOST", _host);
                Environment.SetEnvironmentVariable("SCP_CV_LIBREOFFICE_PROGRAM", _root);
                return new WindowsPresentationDependencySnapshotReader(machineConfigurationPath).Read();
            }
            finally
            {
                Environment.SetEnvironmentVariable("SCP_CV_LIBREOFFICE_HOST", previousHost);
                Environment.SetEnvironmentVariable("SCP_CV_LIBREOFFICE_PROGRAM", previousProgram);
            }
        }

        /// <summary>:returns: 仅清理已核对的自有临时根，原Net48测试宿主不变。</summary>
        public void Dispose()
        {
            var root = Path.GetFullPath(_root);
            if (Path.GetDirectoryName(root) != Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar))
                throw new InvalidOperationException("拒绝清理依赖PE测试根之外的路径。");
            Directory.Delete(root, recursive: true);
        }
    }
}

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class PresentationDependencyReaderEnvironmentGroup
{
    public const string Name = "Presentation dependency image reader environment";
}
