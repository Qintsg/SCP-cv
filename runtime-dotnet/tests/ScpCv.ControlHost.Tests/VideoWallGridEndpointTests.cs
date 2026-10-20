// 正式Host装配的网格DTO/修订/预留拒发回归；仅Simulation，不访问实体墙。
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace ScpCv.ControlHost.Tests;

public sealed class VideoWallGridEndpointTests
{
    [Fact]
    public async Task GridRoundTripsAndReservedInputIsSavedButNeverApplied()
    {
        using var factory = new ControlHostApplicationFactory(); using var client = factory.CreateHttpsClient();
        client.DefaultRequestHeaders.Add("X-CSRFToken", await ScreenShareEndpointTests.LoginAsync(client));
        using var save = await client.PutAsJsonAsync("/api/video-wall/layout/", new { name = "三矩形", mappings = new[]
        {
            new { region = "grid", rect = new { column = 0, row = 0, columns = 3, rows = 3 }, input = new { kind = "window_1" } },
            new { region = "grid", rect = new { column = 5, row = 0, columns = 5, rows = 5 }, input = new { kind = "window_2" } },
            new { region = "grid", rect = new { column = 0, row = 3, columns = 3, rows = 2 }, input = new { kind = "window_1" } },
        } });
        save.EnsureSuccessStatusCode(); using var body = JsonDocument.Parse(await save.Content.ReadAsStringAsync());
        var layout = body.RootElement.GetProperty("layout");
        Assert.True(layout.GetProperty("draft").GetProperty("can_apply").GetBoolean());
        Assert.Equal(3, layout.GetProperty("draft").GetProperty("mappings").GetArrayLength());
        var revision = layout.GetProperty("draft_revision").GetInt64();
        using var apply = await client.PostAsJsonAsync("/api/video-wall/layout/apply/", new { expected_revision = revision }); apply.EnsureSuccessStatusCode();
        using var reserved = await client.PutAsJsonAsync("/api/video-wall/layout/", new { name = "预留", mappings = new[]
        { new { region = "grid", rect = new { column = 0, row = 0, columns = 10, rows = 5 }, input = new { kind = "window_3" } } } });
        reserved.EnsureSuccessStatusCode(); using var rejectedBody = JsonDocument.Parse(await reserved.Content.ReadAsStringAsync());
        var state = rejectedBody.RootElement.GetProperty("layout"); Assert.False(state.GetProperty("draft").GetProperty("can_apply").GetBoolean());
        using var rejected = await client.PostAsJsonAsync("/api/video-wall/layout/apply/", new { expected_revision = state.GetProperty("draft_revision").GetInt64() });
        Assert.Equal(HttpStatusCode.Conflict, rejected.StatusCode);
        using var current = JsonDocument.Parse(await client.GetStringAsync("/api/video-wall/layout/"));
        Assert.Equal(revision, current.RootElement.GetProperty("layout").GetProperty("applied_revision").GetInt64());
    }
}
