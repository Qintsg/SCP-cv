// 运行时打包合同：TFM隔离、冲突不覆盖与PowerShell5.1编码；不运行dotnet或产品进程。
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ScpCv.ControlHost.Tests;

public sealed class RuntimePackagingScriptTests
{
    private static readonly string ScriptRoot = Path.Combine(AppContext.BaseDirectory, "scripts");

    /// <summary>:returns: 缺少真实生成入口时不得报告包准备完成，已有数据保持。</summary>
    [Fact]
    public async Task PluginCachePreparationRejectsMissingGeneratorWithoutChangingPackage()
    {
        if (!OperatingSystem.IsWindows()) return;
        var root = Path.Combine(Path.GetTempPath(), "scp-cv-cache-package-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root); await File.WriteAllTextAsync(Path.Combine(root, "keep.txt"), "keep");
        try
        {
            var result = await RunFunctionsAsync($$"""
                $root = [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String('{{EncodePath(root)}}'))
                New-RuntimeVlcPluginCache -PackageRoot $root
                """);
            Assert.NotEqual(0, result.ExitCode);
            Assert.Contains("缺少插件索引生成入口", result.Error, StringComparison.Ordinal);
            Assert.Equal("keep", await File.ReadAllTextAsync(Path.Combine(root, "keep.txt")));
            Assert.False(File.Exists(Path.Combine(root, "libvlc", "win-x64", "plugins", "plugins.dat")));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public async Task PublishPlanSeparatesFrameworkHostAndExcludesRetiredPowerPoint()
    {
        if (!OperatingSystem.IsWindows()) return;
        var result = await RunFunctionsAsync("""
            @(Get-RuntimePublishPlan -RuntimeSourceRoot 'C:\contract-source' -PackageRoot 'C:\contract-package') |
                ConvertTo-Json -Compress
            """);
        Assert.True(result.ExitCode == 0, result.Error);
        using var document = JsonDocument.Parse(result.Output);
        var components = document.RootElement.EnumerateArray().ToArray();
        Assert.Equal(5, components.Length);
        Assert.DoesNotContain(components, component => component.GetProperty("Name").GetString() == "ScpCv.PowerPointHost");
        Assert.Contains(components, component => component.GetProperty("Name").GetString() == "ScpCv.ControlHost");
        var framework = Assert.Single(components, component => component.GetProperty("Framework").GetString() == "net48");
        Assert.Equal("ScpCv.LibreOfficeHost", framework.GetProperty("Name").GetString());
        Assert.Equal("C:\\contract-package\\libreoffice", framework.GetProperty("Destination").GetString());
        Assert.All(components.Where(component => component.GetProperty("Framework").GetString() != "net48"),
            component => Assert.Equal("C:\\contract-package", component.GetProperty("Destination").GetString()));
    }

    [Fact]
    public async Task PublishArgumentsKeepProjectLocalRuntimeTargetsAndLockedRestore()
    {
        if (!OperatingSystem.IsWindows()) return;
        var result = await RunFunctionsAsync("""
            $plan = @(Get-RuntimePublishPlan -RuntimeSourceRoot 'C:\contract-source' -PackageRoot 'C:\contract-package')
            @($plan | ForEach-Object {
                [pscustomobject]@{
                    Framework = $_.Framework
                    Arguments = @(Get-RuntimePublishArguments -Component $_ -Configuration 'Release' -StageDirectory 'C:\contract-stage')
                }
            }) | ConvertTo-Json -Compress -Depth 4
            """);
        Assert.True(result.ExitCode == 0, result.Error);
        using var document = JsonDocument.Parse(result.Output);
        var components = document.RootElement.EnumerateArray().ToArray();
        Assert.Equal(5, components.Length);
        foreach (var component in components)
        {
            var arguments = component.GetProperty("Arguments").EnumerateArray().Select(argument => argument.GetString()).ToArray();
            Assert.Equal("publish", arguments[0]);
            Assert.Contains("-p:RestoreLockedMode=true", arguments);
            Assert.Contains("-p:SelfContained=false", arguments);
            Assert.DoesNotContain("-r", arguments);
            Assert.DoesNotContain("--runtime", arguments);
            Assert.DoesNotContain(arguments, argument => argument?.StartsWith("-p:RuntimeIdentifier=", StringComparison.OrdinalIgnoreCase) == true);
            if (component.GetProperty("Framework").GetString() == "net48")
                Assert.DoesNotContain("-p:DefaultAppHostRuntimeIdentifier=win-x64", arguments);
            else
                Assert.Contains("-p:DefaultAppHostRuntimeIdentifier=win-x64", arguments);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DuplicateDependenciesRequireIdenticalBytesAndNeverOverwrite(bool sameContents)
    {
        if (!OperatingSystem.IsWindows()) return;
        var testRoot = Path.Combine(Path.GetTempPath(), $"scp-cv-publish-contract-{Guid.NewGuid():N}");
        var first = Path.Combine(testRoot, "first");
        var second = Path.Combine(testRoot, "second");
        var package = Path.Combine(testRoot, "package");
        Directory.CreateDirectory(first);
        Directory.CreateDirectory(second);
        await File.WriteAllTextAsync(Path.Combine(first, "dependency.dll"), "original-dependency");
        await File.WriteAllTextAsync(Path.Combine(second, "dependency.dll"), sameContents ? "original-dependency" : "different-framework-dependency");
        Directory.CreateDirectory(Path.Combine(second, "nested"));
        await File.WriteAllTextAsync(Path.Combine(second, "nested", "new.txt"), "nested-resource");
        try
        {
            var result = await RunFunctionsAsync($$"""
                function Get-FileHash { throw 'contract_module_dependency: Get-FileHash' }
                $root = [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String('{{EncodePath(testRoot)}}'))
                Copy-MergedRuntimeFiles -SourceDirectory (Join-Path $root 'first') -DestinationDirectory (Join-Path $root 'package')
                Get-RuntimeFileSha256 -Path (Join-Path $root 'package\dependency.dll')
                Copy-MergedRuntimeFiles -SourceDirectory (Join-Path $root 'second') -DestinationDirectory (Join-Path $root 'package')
                """);
            Assert.Equal("original-dependency", await File.ReadAllTextAsync(Path.Combine(package, "dependency.dll")));
            Assert.Equal(Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("original-dependency"))), result.Output.Trim());
            if (sameContents)
            {
                Assert.True(result.ExitCode == 0, result.Error);
                Assert.Equal("nested-resource", await File.ReadAllTextAsync(Path.Combine(package, "nested", "new.txt")));
            }
            else
            {
                Assert.NotEqual(0, result.ExitCode);
                Assert.Contains("内容冲突", result.Error, StringComparison.Ordinal);
                Assert.Contains("未覆盖", result.Error, StringComparison.Ordinal);
            }
        }
        finally
        {
            Directory.Delete(testRoot, recursive: true);
        }
    }

    [Theory]
    [InlineData("publish-runtime.ps1")]
    [InlineData("runtime-publish.functions.ps1")]
    public void PublishScriptsKeepWindowsPowerShellUtf8BomAndLf(string name)
    {
        var bytes = File.ReadAllBytes(Path.Combine(ScriptRoot, name));
        Assert.True(bytes.Length >= 3);
        Assert.Equal((byte)0xef, bytes[0]);
        Assert.Equal((byte)0xbb, bytes[1]);
        Assert.Equal((byte)0xbf, bytes[2]);
        Assert.DoesNotContain((byte)'\r', bytes);
    }

    [Fact]
    public void PublisherUsesLockedRestoreAndNeverStartsOrDeploysRuntime()
    {
        var script = File.ReadAllText(Path.Combine(ScriptRoot, "publish-runtime.ps1"));
        var functions = File.ReadAllText(Path.Combine(ScriptRoot, "runtime-publish.functions.ps1"));
        Assert.Contains("RestoreLockedMode=true", functions, StringComparison.Ordinal);
        Assert.Contains("Get-RuntimePublishArguments -Component $component", script, StringComparison.Ordinal);
        Assert.Contains("if (Test-Path -LiteralPath $packageRoot)", script, StringComparison.Ordinal);
        Assert.Contains("Copy-MergedRuntimeFiles", script, StringComparison.Ordinal);
        Assert.DoesNotContain("Start-Process", script, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Stop-Process", script, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("scp.exe", script, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ssh.exe", script, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("PowerPointHost", script, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>:param path: 自有测试路径。:returns: 仅用于隔离脚本参数的UTF8编码值。</summary>
    private static string EncodePath(string path) => Convert.ToBase64String(Encoding.UTF8.GetBytes(path));

    /// <summary>:param body: 仅调用纯布局/复制函数的测试语句。:returns: 隔离PowerShell结果；不执行发布入口。</summary>
    private static async Task<(int ExitCode, string Output, string Error)> RunFunctionsAsync(string body)
    {
        var command = $$"""
            $ErrorActionPreference = 'Stop'
            [Console]::OutputEncoding = New-Object Text.UTF8Encoding($false)
            try {
                $module = [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String('{{EncodePath(Path.Combine(ScriptRoot, "runtime-publish.functions.ps1"))}}'))
                . $module
                {{body}}
                exit 0
            }
            catch { [Console]::Error.WriteLine($_.Exception.Message); exit 1 }
            """;
        var startInfo = new ProcessStartInfo("powershell.exe")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        string[] arguments = ["-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-EncodedCommand",
            Convert.ToBase64String(Encoding.Unicode.GetBytes(command))];
        foreach (var argument in arguments) startInfo.ArgumentList.Add(argument);
        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("无法启动隔离PowerShell。");
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        try { await process.WaitForExitAsync(timeout.Token); }
        catch (OperationCanceledException exception)
        {
            process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync();
            throw new TimeoutException($"纯发布函数合同超时；stdout={await output}；stderr={await error}", exception);
        }
        return (process.ExitCode, await output, await error);
    }
}
