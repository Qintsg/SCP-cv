// 固定必需事实、平台/Framework及PE元数据保护矩阵；测试外部快照，不操作实际安装或Registry。
using ScpCv.Contracts.Presentations.Dependencies;

namespace ScpCv.Contracts.Tests;

public sealed partial class PresentationDependencyPolicyTests
{
    [Theory]
    [InlineData("host")]
    [InlineData("protocol")]
    [InlineData("soffice_exe")]
    [InlineData("soffice_bin")]
    [InlineData("uno_ini")]
    [InlineData("cli_uno")]
    [InlineData("types_rdb")]
    [InlineData("services_rdb")]
    [InlineData("offapi_rdb")]
    public void EveryRequiredFileFactMustBePresentExactlyOnce(string id)
    {
        var healthy = HealthySnapshot();
        foreach (var duplicate in new[] { false, true })
        {
            var files = duplicate
                ? healthy.Files.Append(healthy.Files.Single(file => file.Id == id) with { Label = "fixture-secret-label" }).ToArray()
                : healthy.Files.Where(file => file.Id != id).Append(new("unknown-file", "fixture-secret-label", PresentationDependencyFileState.Readable)).ToArray();
            var report = Inspect(healthy with { Files = files });
            Assert.False(report.Available);
            var component = Assert.Single(report.Components, component => component.Id == id);
            Assert.Equal(duplicate ? "dependency_fact_ambiguous" : "dependency_fact_missing", component.ReasonCode);
            Assert.DoesNotContain("fixture-secret-label", report.Detail, StringComparison.Ordinal);
        }
    }

    [Theory]
    [InlineData(528039, false)]
    [InlineData(528040, true)]
    [InlineData(533320, true)]
    public void FrameworkFullReleaseUsesTheOfficialMinimumAndAcceptsNewerRuntime(int release, bool available)
    {
        var report = Inspect(HealthySnapshot() with { Framework = new(release, true) });
        Assert.Equal(available, report.Available);
        Assert.Equal(available ? "framework_compatible" : "framework_too_old",
            Assert.Single(report.Components, component => component.Id == "framework").ReasonCode);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("clr-unreadable")]
    [InlineData("registry-unreadable")]
    public void FrameworkMustHaveReadableRuntimeFacts(string fault)
    {
        var framework = fault switch
        {
            "missing" => new PresentationFrameworkRuntime(null, true),
            "clr-unreadable" => new PresentationFrameworkRuntime(528040, false),
            _ => new PresentationFrameworkRuntime(528040, true, "framework_registry_unreadable"),
        };
        Assert.False(Inspect(HealthySnapshot() with { Framework = framework }).Available);
    }

    [Theory]
    [InlineData(false, true, true)]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    public void WindowsOperatingSystemAndHostProcessMustBothBeX64(bool windows, bool osX64, bool processX64)
    {
        var report = Inspect(HealthySnapshot() with { Platform = new(windows, osX64, processX64) });
        Assert.False(report.Available);
        Assert.Equal("platform_unsupported", Assert.Single(report.Components, component => component.Id == "platform").ReasonCode);
    }

    [Theory]
    [InlineData("host-x86", "host", "architecture_incompatible")]
    [InlineData("host-net10", "host", "framework_metadata_incompatible")]
    [InlineData("host-no-entry", "host", "host_entrypoint_invalid")]
    [InlineData("host-dll", "host", "host_entrypoint_invalid")]
    [InlineData("host-no-mscorlib", "host", "framework_metadata_incompatible")]
    [InlineData("protocol-32-required", "protocol", "architecture_incompatible")]
    [InlineData("protocol-net10", "protocol", "framework_metadata_incompatible")]
    [InlineData("protocol-exe", "protocol", "protocol_kind_invalid")]
    [InlineData("protocol-wrong-version", "protocol", "protocol_binding_incompatible")]
    [InlineData("protocol-wrong-token", "protocol", "protocol_binding_incompatible")]
    public void ActualHostAndProtocolMetadataMustMatchTheirRuntimeRole(string fault, string id, string expectedCode)
    {
        var healthy = HealthySnapshot();
        var images = healthy.Images.ToDictionary(pair => pair.Key, pair => pair.Value);
        var image = images[id];
        images[id] = fault switch
        {
            "host-x86" => image with { Architecture = PresentationImageArchitecture.X86 },
            "host-net10" or "protocol-net10" => image with { TargetFramework = ".NETCoreApp,Version=v10.0" },
            "host-no-entry" => image with { HasManagedEntryPoint = false },
            "host-dll" => image with { IsDll = true },
            "host-no-mscorlib" => image with { References = image.References.Where(reference => reference.Name != "mscorlib").ToArray() },
            "protocol-32-required" => image with { Requires32Bit = true },
            "protocol-exe" => image with { IsDll = false },
            "protocol-wrong-version" => image with { Identity = image.Identity! with { Version = "2.0.0.0" } },
            _ => image with { Identity = image.Identity! with { PublicKeyToken = "0102030405060708" } },
        };
        var report = Inspect(healthy with { Images = images });
        Assert.False(report.Available);
        Assert.Equal(expectedCode, Assert.Single(report.Components, component => component.Id == id).ReasonCode);
    }

    [Fact]
    public void ManagedAnyCpuProtocolIsX64CompatibleAndBuildToolFactsAreNotRuntimeRequirements()
    {
        var healthy = HealthySnapshot();
        // 外部事实明确提供SDK/targeting-pack缺失，运行policy只检查既定运行事实，不把构建条件加进来。
        var files = healthy.Files.Concat(new[]
        {
            new PresentationDependencyFile("sdk", "build-sdk", PresentationDependencyFileState.Missing),
            new PresentationDependencyFile("targeting-pack", "build-targeting-pack", PresentationDependencyFileState.Missing),
        }).ToArray();
        Assert.Equal(PresentationImageArchitecture.X86, healthy.Images["protocol"].Architecture);
        Assert.True(healthy.Images["protocol"].IlOnly);
        Assert.True(Inspect(healthy with { Files = files }).Available);
    }
}
