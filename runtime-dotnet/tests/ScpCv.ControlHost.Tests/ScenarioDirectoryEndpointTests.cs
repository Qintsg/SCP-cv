// 预设文件管理拒绝路径冲突和非法移动；高级未配置项不产生实体调用。
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using ScpCv.Infrastructure.Devices;

namespace ScpCv.ControlHost.Tests;

public sealed class ScenarioDirectoryEndpointTests
{
    [Fact]
    public async Task ScreenOnlyAdvancedConfigurationWritesOnceWithoutChangingSystemVolume()
    {
        using var factory = new ControlHostApplicationFactory();
        using var client = factory.CreateHttpsClient();
        client.DefaultRequestHeaders.Add("X-CSRFToken", await ScreenShareEndpointTests.LoginAsync(client));
        var transport = Assert.IsType<SimulationVenueAudioTransport>(factory.Services.GetRequiredService<IVenueAudioTransport>());
        using var initial = JsonDocument.Parse(await client.GetStringAsync("/api/volume/"));
        var systemVolume = initial.RootElement.GetProperty("volume").GetProperty("level").GetInt32();
        using var create = await client.PostAsJsonAsync("/api/scenarios/", new { name = "仅现场音量",
            advanced = new { audio = new { screen_volume = 35 } } });
        create.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(await create.Content.ReadAsStringAsync());
        var id = json.RootElement.GetProperty("scenario").GetProperty("id").GetInt64();
        using var activate = await client.PostAsJsonAsync($"/api/scenarios/{id}/activate/", new { });
        activate.EnsureSuccessStatusCode();
        Assert.Equal(1, transport.WriteCount);
        Assert.Equal(35, await transport.ReadAsync(CancellationToken.None));
        using var final = JsonDocument.Parse(await client.GetStringAsync("/api/volume/"));
        Assert.Equal(systemVolume, final.RootElement.GetProperty("volume").GetProperty("level").GetInt32());
    }

    [Fact]
    public async Task DuplicatePathsAndInvalidFolderMovesAreRejectedWithoutMutation()
    {
        using var factory = new ControlHostApplicationFactory();
        using var client = factory.CreateHttpsClient();
        client.DefaultRequestHeaders.Add("X-CSRFToken", await ScreenShareEndpointTests.LoginAsync(client));
        using var folder = await client.PostAsJsonAsync("/api/scenario-folders/", new { name = "演讲" });
        folder.EnsureSuccessStatusCode();
        using var folderJson = JsonDocument.Parse(await folder.Content.ReadAsStringAsync());
        var folderId = folderJson.RootElement.GetProperty("folder").GetProperty("id").GetInt64();
        using var first = await client.PostAsJsonAsync("/api/scenarios/", new { name = "开场", folder_id = folderId });
        first.EnsureSuccessStatusCode();
        using var firstJson = JsonDocument.Parse(await first.Content.ReadAsStringAsync());
        var id = firstJson.RootElement.GetProperty("scenario").GetProperty("id").GetInt64();
        using var duplicate = await client.PostAsJsonAsync("/api/scenarios/", new { name = "开场", folder_id = folderId });
        Assert.Equal(HttpStatusCode.BadRequest, duplicate.StatusCode);
        using var invalid = await client.PatchAsJsonAsync($"/api/scenarios/{id}/", new { folder_id = "not-a-folder" });
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        using var current = JsonDocument.Parse(await client.GetStringAsync($"/api/scenarios/{id}/"));
        Assert.Equal("/演讲/开场", current.RootElement.GetProperty("scenario").GetProperty("path").GetString());
        using var root = await client.PostAsJsonAsync("/api/scenarios/", new { name = "开场" });
        root.EnsureSuccessStatusCode();
        using var collision = await client.PatchAsJsonAsync($"/api/scenarios/{id}/", new { folder_id = (long?)null });
        Assert.Equal(HttpStatusCode.BadRequest, collision.StatusCode);
    }

    [Fact]
    public async Task EmptyAdvancedConfigurationPreservesAudioWithZeroWrites()
    {
        using var factory = new ControlHostApplicationFactory();
        using var client = factory.CreateHttpsClient();
        client.DefaultRequestHeaders.Add("X-CSRFToken", await ScreenShareEndpointTests.LoginAsync(client));
        var transport = Assert.IsType<SimulationVenueAudioTransport>(factory.Services.GetRequiredService<IVenueAudioTransport>());
        using var create = await client.PostAsJsonAsync("/api/scenarios/", new { name = "保持现场", advanced = new { } });
        create.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(await create.Content.ReadAsStringAsync());
        var id = json.RootElement.GetProperty("scenario").GetProperty("id").GetInt64();
        using var activate = await client.PostAsJsonAsync($"/api/scenarios/{id}/activate/", new { });
        activate.EnsureSuccessStatusCode();
        Assert.Equal(0, transport.WriteCount);
    }
}
