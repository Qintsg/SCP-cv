// 外部只读快照策略回归；通过正式Probe接口，不替换内部判断、不修改本机Registry或安装。
using ScpCv.Contracts.Presentations.Dependencies;

namespace ScpCv.Contracts.Tests;

public sealed partial class PresentationDependencyPolicyTests
{
    [Fact]
    public void MissingRequiredHostFactCannotMakeAnOtherwiseHealthySnapshotAvailable()
    {
        var healthy = HealthySnapshot();
        var missingHost = healthy with { Files = healthy.Files.Where(file => file.Id != "host").ToArray() };

        var report = Inspect(missingHost);

        Assert.False(report.Available);
        var host = Assert.Single(report.Components, component => component.Id == "host");
        Assert.Equal("unavailable", host.Status);
        Assert.Equal("dependency_fact_missing", host.ReasonCode);
    }

    /// <summary>:param snapshot: 外部文件/Registry/PE事实。:returns: 正式Probe对外结果，不直接测试private policy。</summary>
    private static PresentationDependencyReport Inspect(PresentationDependencySnapshot snapshot) =>
        new PresentationDependencyProbe(new FixtureSnapshotReader(snapshot)).Inspect();

    /// <summary>:returns: 仅当前stage2所需的完整纯值事实；不表示真实UNO或本机无SDK。</summary>
    private static PresentationDependencySnapshot HealthySnapshot()
    {
        var core = new PresentationAssemblyIdentity("mscorlib", "4.0.0.0", "", "b77a5c561934e089");
        var protocol = new PresentationAssemblyIdentity("ScpCv.Presentation.Protocol", "1.0.0.0", "", "");
        var protocolImage = new PresentationDependencyImage
        {
            IsValidImage = true, Architecture = PresentationImageArchitecture.X86, HasManagedMetadata = true,
            IlOnly = true, IsDll = true, IsExecutableImage = true, Identity = protocol, TargetFramework = ".NETFramework,Version=v4.8",
            ProtocolVersion = ScpCv.Presentation.Protocol.PresentationRequestPolicy.ProtocolVersion,
            References = [core],
        };
        var hostImage = protocolImage with
        {
            Architecture = PresentationImageArchitecture.X64, IsDll = false, HasManagedEntryPoint = true,
            Identity = new("ScpCv.LibreOfficeHost", "1.0.0.0", "", ""),
            References = [core, protocol,
                new("cli_basetypes", "1.0.20.0", "", "ce2cb7e279207b9e"),
                new("cli_oootypes", "1.0.9.0", "", "ce2cb7e279207b9e"),
                new("cli_uretypes", "1.0.9.0", "", "ce2cb7e279207b9e"),
                new("cli_cppuhelper", "1.0.23.0", "", "ce2cb7e279207b9e")],
        };
        var native = new PresentationDependencyImage { IsValidImage = true, Architecture = PresentationImageArchitecture.X64,
            IsExecutableImage = true, HasNativeEntryPoint = true };
        var basetypes = new PresentationAssemblyIdentity("cli_basetypes", "1.0.20.0", "", "ce2cb7e279207b9e");
        var ure = new PresentationAssemblyIdentity("cli_ure", "1.0.23.0", "", "ce2cb7e279207b9e");
        var uretypes = new PresentationAssemblyIdentity("cli_uretypes", "1.0.9.0", "", "ce2cb7e279207b9e");
        var oootypes = new PresentationAssemblyIdentity("cli_oootypes", "1.0.9.0", "", "ce2cb7e279207b9e");
        var cppuhelper = new PresentationAssemblyIdentity("cli_cppuhelper", "1.0.23.0", "", "ce2cb7e279207b9e");
        var candidates = new[] { basetypes, ure, uretypes, oootypes, cppuhelper }.Select(identity =>
            new PresentationCliCandidate(identity.Name, PresentationDependencyFileState.Readable, protocolImage with
            {
                Identity = identity, TargetFramework = string.Empty, ProtocolVersion = null,
                Architecture = identity.Name == "cli_cppuhelper" ? PresentationImageArchitecture.X64 : PresentationImageArchitecture.X86,
                IlOnly = identity.Name != "cli_cppuhelper",
                References = identity.Name switch
                {
                    "cli_cppuhelper" => [core, basetypes, ure],
                    "cli_oootypes" => [core, basetypes, uretypes],
                    "cli_basetypes" => [core],
                    _ => [core, basetypes],
                },
            })).ToArray();
        var ids = new[] { "host", "protocol", "soffice_exe", "soffice_bin", "uno_ini", "cli_uno", "types_rdb", "services_rdb", "offapi_rdb" };
        return new PresentationDependencySnapshot(ids.Select(id => new PresentationDependencyFile(id, id, PresentationDependencyFileState.Readable)).ToArray())
        {
            Platform = new(true, true, true), Framework = new(528040, true),
            CliCandidates = candidates,
            Images = new Dictionary<string, PresentationDependencyImage>
            {
                ["host"] = hostImage, ["protocol"] = protocolImage,
                ["soffice_exe"] = native, ["soffice_bin"] = native,
                ["cli_uno"] = native with { HasManagedMetadata = true, IsDll = true, IlOnly = false,
                    Identity = new("cli_uno", "0.0.0.0", "", ""), References = [basetypes, ure, uretypes] },
            },
        };
    }

    private sealed class FixtureSnapshotReader(PresentationDependencySnapshot snapshot) : IPresentationDependencySnapshotReader
    {
        /// <summary>:returns: 仅测试外部事实，不创建文件、Registry键或进程。</summary>
        public PresentationDependencySnapshot Read() => snapshot;
    }
}
