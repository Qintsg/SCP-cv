// 正常认证REST→持久化→OPEN租约凭据完整性；独立仿真主机，不启动播放器或现场连接。
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ScpCv.Domain.Model;
using ScpCv.Infrastructure.Commands;
using ScpCv.Infrastructure.Persistence;

namespace ScpCv.ControlHost.Tests;

public sealed class StreamCredentialEndpointTests
{
    [Fact]
    public async Task CredentialedSourceCanBeEditedAndOpenedWithoutLeakingItsAutomaticName()
    {
        using var factory = new ControlHostApplicationFactory();
        using var client = factory.CreateHttpsClient();
        using var login = await client.PostAsJsonAsync("/api/auth/login/", new { username = "operator", password = "Old-password-123" });
        login.EnsureSuccessStatusCode();
        using var csrfResponse = await client.GetAsync("/api/auth/csrf/");
        using var csrfBody = await JsonDocument.ParseAsync(await csrfResponse.Content.ReadAsStreamAsync());
        client.DefaultRequestHeaders.Add("X-CSRFToken", csrfBody.RootElement.GetProperty("csrfToken").GetString());
        const string original = "rtsp://fixture-name:fixture-pass@192.0.2.188:554/streaming/channels/501";
        using var create = await client.PostAsJsonAsync("/api/sources/streams/", new { source_type = "rtsp_stream", url = original });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        using var created = await JsonDocument.ParseAsync(await create.Content.ReadAsStreamAsync());
        var source = created.RootElement.GetProperty("source");
        var sourceId = source.GetProperty("id").GetInt64();
        Assert.DoesNotContain("fixture-name", source.GetProperty("name").GetString()!, StringComparison.Ordinal);
        Assert.DoesNotContain("fixture-pass", source.GetProperty("path").GetString()!, StringComparison.Ordinal);
        const string replacement = "rtsp://fixture-name:changed%40pass@192.0.2.188:554/streaming/channels/501";
        using var update = await client.PatchAsJsonAsync($"/api/sources/{sourceId}/", new { uri = replacement });
        update.EnsureSuccessStatusCode();
        using var list = await client.GetAsync("/api/sources/?source_type=rtsp_stream");
        using var listed = await JsonDocument.ParseAsync(await list.Content.ReadAsStreamAsync());
        Assert.Equal(new Uri(replacement).AbsoluteUri, listed.RootElement.GetProperty("sources").EnumerateArray()
            .Single(item => item.GetProperty("id").GetInt64() == sourceId).GetProperty("uri").GetString());

        var contexts = factory.Services.GetRequiredService<IDbContextFactory<ControlDbContext>>();
        await using (var db = await contexts.CreateDbContextAsync())
        {
            var group = await db.RuntimeGroupControls.SingleAsync();
            group.State = RuntimeGroupState.Armed; group.GroupEpoch = 1;
            await db.SaveChangesAsync();
        }
        using var open = await client.PostAsJsonAsync("/api/playback/1/open/", new { source_id = sourceId, autoplay = true });
        open.EnsureSuccessStatusCode();
        var command = await factory.Services.GetRequiredService<CommandRepository>().ClaimAsync(
            new ClaimCommand(CommandTargetKind.Display, 1, Guid.NewGuid(), 1, 1, TimeSpan.FromSeconds(30)));
        Assert.NotNull(command);
        using var args = JsonDocument.Parse(command.ArgsJson);
        Assert.Equal("OPEN", command.Command);
        Assert.Equal(new Uri(replacement).AbsoluteUri, args.RootElement.GetProperty("uri").GetString());
    }
}
