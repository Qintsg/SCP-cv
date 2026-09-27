// 两窗性能脚本不得访问退役窗口，也不得把口令写在进程命令行。
namespace ScpCv.ControlHost.Tests;

public sealed class BenchmarkScriptTests
{
    private static readonly string ScriptPath = Path.Combine(
        AppContext.BaseDirectory, "scripts", "benchmark-commands.ps1");

    [Fact]
    public void DefaultsToTwoWindowsAndRejectsLegacyTargets()
    {
        var script = File.ReadAllText(ScriptPath);

        Assert.Contains("[int[]]$Targets = @(1, 2)", script, StringComparison.Ordinal);
        Assert.Contains("窗口号必须在 1..2", script, StringComparison.Ordinal);
        Assert.DoesNotContain("@(1, 2, 3, 4)", script, StringComparison.Ordinal);
    }

    [Fact]
    public void ReadsCredentialFromProtectedFileAndAllowsExplicitSqliteTool()
    {
        var script = File.ReadAllText(ScriptPath);

        Assert.Contains("[string]$PasswordFile", script, StringComparison.Ordinal);
        Assert.Contains("[string]$SqliteExecutable", script, StringComparison.Ordinal);
        Assert.DoesNotContain("[Parameter(Mandatory = $true)][string]$Password", script, StringComparison.Ordinal);
    }
}
