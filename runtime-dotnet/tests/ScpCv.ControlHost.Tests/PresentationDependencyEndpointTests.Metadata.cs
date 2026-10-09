// 文件齐全仍必须通过宿主PE/Framework元数据门禁；只有自有文件，不执行占位EXE。
using System.Net;
using System.Text.Json;

namespace ScpCv.ControlHost.Tests;

public sealed partial class PresentationDependencyEndpointTests
{
    [Fact]
    public async Task ReadableFilesWithANonPeHostCannotAdvertiseRuntimeAvailability()
    {
        using var factory = new ControlHostApplicationFactory();
        using var client = factory.CreateHttpsClient();
        var fixtureRoot = Path.Combine(factory.TemporaryRoot, "metadata-dependency-fixture");
        var program = Path.Combine(fixtureRoot, "program");
        var host = Path.Combine(fixtureRoot, "ScpCv.LibreOfficeHost.exe");
        Directory.CreateDirectory(Path.Combine(program, "types"));
        await File.WriteAllTextAsync(host, "owned-non-pe-host-not-executable");
        foreach (var relative in new[] { "soffice.exe", "soffice.bin", "uno.ini", "cli_uno.dll", "types.rdb", "services.rdb", Path.Combine("types", "offapi.rdb") })
            await File.WriteAllTextAsync(Path.Combine(program, relative), "owned-readable-dependency-placeholder");
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
            var hostComponent = Assert.Single(settings.GetProperty("components").EnumerateArray(),
                component => component.GetProperty("id").GetString() == "host");
            Assert.Equal("unavailable", hostComponent.GetProperty("status").GetString());
            Assert.Equal("invalid_image", hostComponent.GetProperty("reason_code").GetString());
            Assert.DoesNotContain(fixtureRoot, body.RootElement.GetRawText(), StringComparison.OrdinalIgnoreCase);
            Assert.Equal(8, Directory.GetFiles(fixtureRoot, "*", SearchOption.AllDirectories).Length);
        }
        finally
        {
            Environment.SetEnvironmentVariable("SCP_CV_LIBREOFFICE_PROGRAM", previousProgram);
            Environment.SetEnvironmentVariable("SCP_CV_LIBREOFFICE_HOST", previousHost);
        }
    }
}
