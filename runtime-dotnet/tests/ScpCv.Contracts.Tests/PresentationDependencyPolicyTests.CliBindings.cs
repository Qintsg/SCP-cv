// 正确宿主引用不代表官方CLI绑定已安装；缺候选时不能把阶段2映像检查冒称完整可用。
using ScpCv.Contracts.Presentations.Dependencies;
namespace ScpCv.Contracts.Tests;

public sealed partial class PresentationDependencyPolicyTests
{
    [Theory]
    [InlineData("cli_basetypes")]
    [InlineData("cli_ure")]
    [InlineData("cli_uretypes")]
    [InlineData("cli_oootypes")]
    [InlineData("cli_cppuhelper")]
    public void EveryRequiredCliCandidateIncludingTransitiveUreMustExist(string name)
    {
        var healthy = HealthySnapshot();
        if (name == "cli_ure") Assert.DoesNotContain(healthy.Images["host"].References, reference => reference.Name == name);
        var report = Inspect(healthy with { CliCandidates = healthy.CliCandidates.Where(candidate => candidate.Name != name).ToArray() });
        Assert.False(report.Available);
        Assert.Equal("cli_binding_missing", Assert.Single(report.Components, component => component.Id == name).ReasonCode);
    }

    [Theory]
    [InlineData("token", "cli_basetypes", "cli_binding_incompatible")]
    [InlineData("culture", "cli_basetypes", "cli_binding_incompatible")]
    [InlineData("version", "cli_basetypes", "cli_binding_incompatible")]
    [InlineData("32bit-required", "cli_basetypes", "cli_image_incompatible")]
    [InlineData("helper-il-only", "cli_cppuhelper", "cli_image_incompatible")]
    public void CandidateIdentityAndMsilOrMixedArchitectureMustMatchActualReferences(string fault, string name, string expectedCode)
    {
        var healthy = HealthySnapshot();
        var candidates = healthy.CliCandidates.Select(candidate => candidate.Name != name ? candidate : candidate with
        {
            Image = fault switch
            {
                "token" => candidate.Image with { Identity = candidate.Image.Identity! with { PublicKeyToken = "0102030405060708" } },
                "culture" => candidate.Image with { Identity = candidate.Image.Identity! with { Culture = "zh-CN" } },
                "version" => candidate.Image with { Identity = candidate.Image.Identity! with { Version = "9.0.0.0" } },
                "32bit-required" => candidate.Image with { Requires32Bit = true },
                _ => candidate.Image with { IlOnly = true },
            },
        }).ToArray();
        var report = Inspect(healthy with { CliCandidates = candidates });
        Assert.False(report.Available);
        Assert.Equal(expectedCode, Assert.Single(report.Components, component => component.Id == name).ReasonCode);
    }

    [Fact]
    public void MultipleMatchingCandidatesCannotBeResolvedByTakingTheFirstDll()
    {
        var healthy = HealthySnapshot();
        var report = Inspect(healthy with { CliCandidates = healthy.CliCandidates.Append(healthy.CliCandidates[0]).ToArray() });
        Assert.False(report.Available);
        Assert.Equal("cli_binding_ambiguous", Assert.Single(report.Components, component => component.Id == "cli_basetypes").ReasonCode);
    }

    [Fact]
    public void UnknownCliReferenceCannotBeIgnoredByTheRequiredFiveClosure()
    {
        var healthy = HealthySnapshot();
        var images = healthy.Images.ToDictionary(pair => pair.Key, pair => pair.Value);
        images["host"] = images["host"] with
        { References = images["host"].References.Append(new("cli_unconfirmed_extension", "1.0.0.0", "", "ce2cb7e279207b9e")).ToArray() };
        var report = Inspect(healthy with { Images = images });
        Assert.False(report.Available);
        Assert.Contains(report.Components, component => component.Id == "cli_bindings" && component.ReasonCode == "cli_binding_unconfirmed");
    }

    [Fact]
    public void ConfirmedAppRedirectCanBindOldHostReferencesToTheInstalledCandidate()
    {
        var old = WithHostBasetypesVersion(HealthySnapshot(), "1.0.19.0");
        var report = Inspect(old with
        { BindingRedirects = [new("cli_basetypes", "", "ce2cb7e279207b9e", "1.0.0.0", "1.0.19.0", "1.0.20.0", false)] });
        Assert.True(report.Available);
    }

    [Fact]
    public void ConfirmedPublisherFamilyCanBindOldReferencesWithoutRequiringExactOldDll()
    {
        var old = WithHostBasetypesVersion(HealthySnapshot(), "1.0.19.0");
        var report = Inspect(old with
        { BindingRedirects = [new("cli_basetypes", "", "ce2cb7e279207b9e", "1.0.0.0", "1.0.19.0", "1.0.20.0", true) { PublisherFamily = "1.0" }] });
        Assert.True(report.Available);
    }

    [Fact]
    public void GlobalPublisherDisableDoesNotApplyTheOtherwiseMatchingPublisherRedirect()
    {
        var old = WithHostBasetypesVersion(HealthySnapshot(), "1.0.19.0");
        var report = Inspect(old with
        {
            PublisherPolicyDisabled = ExpectedCliBindingNames,
            BindingRedirects = [new("cli_basetypes", "", "ce2cb7e279207b9e", "1.0.0.0", "1.0.19.0", "1.0.20.0", true) { PublisherFamily = "1.0" }],
        });
        Assert.False(report.Available);
    }

    [Fact]
    public void DifferentPublisherFamilyCannotRedirectTheCurrentBindingVersion()
    {
        var report = Inspect(HealthySnapshot() with
        { BindingRedirects = [new("cli_basetypes", "", "ce2cb7e279207b9e", "1.0.0.0", "1.0.20.0", "9.0.0.0", true) { PublisherFamily = "2.0" }] });
        Assert.True(report.Available);
    }

    [Fact]
    public void PublisherSelectionUsesTheVersionAfterTheAppRedirect()
    {
        var old = WithHostBasetypesVersion(HealthySnapshot(), "1.0.18.0");
        var report = Inspect(old with
        {
            BindingRedirects =
            [
                new("cli_basetypes", "", "ce2cb7e279207b9e", "1.0.18.0", "1.0.18.0", "1.0.19.0", false),
                new("cli_basetypes", "", "ce2cb7e279207b9e", "1.0.19.0", "1.0.19.0", "1.0.20.0", true) { PublisherFamily = "1.0" },
            ],
        });
        Assert.True(report.Available);
    }

    [Fact]
    public void UnconfirmedBindingConfigurationCannotAdvertiseInstalledCompatibility()
    {
        Assert.False(Inspect(HealthySnapshot() with { BindingConfigurationUnconfirmed = true }).Available);
    }

    [Fact]
    public void PublisherRedirectWithUnknownVersionFamilyCannotAuthorizeStaticBinding()
    {
        var snapshot = HealthySnapshot() with
        {
            BindingRedirects = [new("cli_basetypes", "", "ce2cb7e279207b9e", "1.0.0.0", "1.0.20.0", "1.0.20.0", true)],
        };

        var report = Inspect(snapshot);

        Assert.False(report.Available);
        Assert.Equal("cli_binding_unconfirmed", Assert.Single(report.Components, component => component.Id == "cli_basetypes").ReasonCode);
    }

    [Fact]
    public void CorrectHostAssemblyReferencesWithoutCliCandidatesCannotAdvertiseRuntimeAvailability()
    {
        var snapshot = HealthySnapshot() with { CliCandidates = [] };
        var host = snapshot.Images["host"];
        Assert.Contains(host.References, reference => reference.Name == "cli_basetypes" && reference.Version == "1.0.20.0");
        Assert.Contains(host.References, reference => reference.Name == "cli_cppuhelper" && reference.Version == "1.0.23.0");

        var report = Inspect(snapshot);

        Assert.False(report.Available);
        Assert.Contains(report.Components, component => component.Id == "cli_basetypes" && component.ReasonCode == "cli_binding_missing");
    }

    /// <summary>:param snapshot: 健康外部事实。:param version: 旧宿主实际引用。:returns: 只改宿主base引用，不伪造候选DLL。</summary>
    private static PresentationDependencySnapshot WithHostBasetypesVersion(PresentationDependencySnapshot snapshot, string version)
    {
        var images = snapshot.Images.ToDictionary(pair => pair.Key, pair => pair.Value);
        images["host"] = images["host"] with
        { References = images["host"].References.Select(reference => reference.Name == "cli_basetypes" ? reference with { Version = version } : reference).ToArray() };
        return snapshot with { Images = images };
    }
}
