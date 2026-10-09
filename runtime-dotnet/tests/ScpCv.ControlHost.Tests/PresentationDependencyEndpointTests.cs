// 文稿依赖只读诊断的真实HTTP回归；临时进程环境隔离且精确恢复，不启动Office或打印配置值。
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace ScpCv.ControlHost.Tests;

[Collection(PresentationDependencyEnvironmentGroup.Name)]
public sealed partial class PresentationDependencyEndpointTests
{
    [Fact]
    public async Task ExistingHostAndUnoIniWithoutSofficeCannotAdvertiseRuntimeAvailability()
    {
        using var factory = new ControlHostApplicationFactory();
        using var client = factory.CreateHttpsClient();
        var fixtureRoot = Path.Combine(factory.TemporaryRoot, "dependency-fixture");
        var program = Path.Combine(fixtureRoot, "program");
        var host = Path.Combine(fixtureRoot, "ScpCv.LibreOfficeHost.exe");
        Directory.CreateDirectory(program);
        await File.WriteAllTextAsync(host, "owned-host-placeholder-not-executable");
        await File.WriteAllTextAsync(Path.Combine(program, "uno.ini"), "owned-uno-config-placeholder");
        var previousProgram = Environment.GetEnvironmentVariable("SCP_CV_LIBREOFFICE_PROGRAM");
        var previousHost = Environment.GetEnvironmentVariable("SCP_CV_LIBREOFFICE_HOST");
        try
        {
            Environment.SetEnvironmentVariable("SCP_CV_LIBREOFFICE_PROGRAM", program);
            Environment.SetEnvironmentVariable("SCP_CV_LIBREOFFICE_HOST", host);
            await AuthenticateAsync(client);

            using var response = await client.GetAsync("/api/settings/presentation/");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using var body = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
            var settings = body.RootElement.GetProperty("settings");
            Assert.False(settings.GetProperty("available").GetBoolean());
            Assert.Equal("unavailable", settings.GetProperty("dependency_status").GetString());
            var detail = settings.GetProperty("detail").GetString()!;
            Assert.Contains("soffice.exe", detail, StringComparison.Ordinal);
            Assert.DoesNotContain(fixtureRoot, detail, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(host, body.RootElement.GetRawText(), StringComparison.OrdinalIgnoreCase);
            Assert.Equal(2, Directory.GetFiles(fixtureRoot, "*", SearchOption.AllDirectories).Length);
            Assert.DoesNotContain(AppDomain.CurrentDomain.GetAssemblies(),
                assembly => assembly.GetName().Name?.StartsWith("cli_", StringComparison.Ordinal) == true);
        }
        finally
        {
            Environment.SetEnvironmentVariable("SCP_CV_LIBREOFFICE_PROGRAM", previousProgram);
            Environment.SetEnvironmentVariable("SCP_CV_LIBREOFFICE_HOST", previousHost);
        }
    }

    /// <summary>:param client: 隔离Simulation客户端。:returns: 完成真实认证，不触碰现场或Office。</summary>
    private static async Task AuthenticateAsync(HttpClient client)
    {
        using var response = await client.PostAsJsonAsync("/api/auth/login/",
            new { username = "operator", password = "Old-password-123" });
        response.EnsureSuccessStatusCode();
    }
}

// xUnit仅将当前全局进程环境用例排他运行，避免其它HTTP测试观察临时路径。
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class PresentationDependencyEnvironmentGroup
{
    public const string Name = "Presentation dependency environment";
}
