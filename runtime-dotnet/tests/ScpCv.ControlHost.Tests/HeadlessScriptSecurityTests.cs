namespace ScpCv.ControlHost.Tests;

public sealed class HeadlessScriptSecurityTests
{
    private static readonly string ScriptPath = Path.Combine(
        AppContext.BaseDirectory,
        "scripts",
        "run-headless.ps1");

    [Fact]
    public void ControlHostPasswordIsInheritedThroughEnvironmentInsteadOfCommandLine()
    {
        var script = File.ReadAllText(ScriptPath);

        Assert.Contains("Authentication__DevelopmentAccount__Password", script, StringComparison.Ordinal);
        Assert.DoesNotContain("--Authentication:DevelopmentAccount:Password", script, StringComparison.Ordinal);
    }

    [Fact]
    public void DetachedLauncherReferencesProtectedPasswordFileInsteadOfEmbeddingPassword()
    {
        var script = File.ReadAllText(ScriptPath);

        Assert.Contains("DevelopmentPasswordFile = '", script, StringComparison.Ordinal);
        Assert.DoesNotContain("(\"    DevelopmentPassword = '\"", script, StringComparison.Ordinal);
        Assert.Contains("icacls.exe", script, StringComparison.Ordinal);
    }

    [Fact]
    public void AllowedOriginsAcceptsAListAndConfiguresEachEntry()
    {
        var script = File.ReadAllText(ScriptPath);

        Assert.Contains("-split '[,;]'", script, StringComparison.Ordinal);
        Assert.Contains("--Authentication:AllowedOrigins:{0}={1}", script, StringComparison.Ordinal);
    }
}
