// 实际安装映像种类约束：Office启动器是EXE，cli_uno是AMD64混合托管DLL而不是纯原生文件。
using ScpCv.Contracts.Presentations.Dependencies;

namespace ScpCv.Contracts.Tests;

public sealed partial class PresentationDependencyPolicyTests
{
    [Fact]
    public void OfficeDllCannotReplaceTheRequiredExecutable()
    {
        var healthy = HealthySnapshot();
        var images = healthy.Images.ToDictionary(pair => pair.Key, pair => pair.Value);
        images["soffice_exe"] = images["soffice_exe"] with { IsDll = true };

        var report = Inspect(healthy with { Images = images });

        Assert.False(report.Available);
        var office = Assert.Single(report.Components, component => component.Id == "soffice_exe");
        Assert.Equal("executable_kind_invalid", office.ReasonCode);
    }

    [Fact]
    public void PureNativeFileCannotReplaceTheRequiredCliUnoMixedAssembly()
    {
        var healthy = HealthySnapshot();
        var images = healthy.Images.ToDictionary(pair => pair.Key, pair => pair.Value);
        images["cli_uno"] = images["cli_uno"] with { HasManagedMetadata = false, IsDll = false, Identity = null };

        var report = Inspect(healthy with { Images = images });

        Assert.False(report.Available);
        var bridge = Assert.Single(report.Components, component => component.Id == "cli_uno");
        Assert.Equal("cli_uno_kind_invalid", bridge.ReasonCode);
    }
}
