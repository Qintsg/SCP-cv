// 通过正常HTTP和公开队列领取验证窗口音频配置随OPEN传递；独立夹具，不启动实体播放器。
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ScpCv.Domain.Model;
using ScpCv.Infrastructure.Commands;
using ScpCv.Infrastructure.Persistence;

namespace ScpCv.ControlHost.Tests;

public sealed class PlaybackOpenAudioIntentTests
{
    /// <summary>:returns: 已保存的静音/零音量跟随实际OPEN租约，不能只留在HTTP窗口配置。</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OpenCarriesPersistedWindowAudioIntoClaimedLease(bool fromScenario)
    {
        using var factory = new ControlHostApplicationFactory();
        using var client = factory.CreateHttpsClient();
        using var login = await client.PostAsJsonAsync("/api/auth/login/",
            new { username = "operator", password = "Old-password-123" });
        login.EnsureSuccessStatusCode();
        using var csrfResponse = await client.GetAsync("/api/auth/csrf/");
        using var csrf = await JsonDocument.ParseAsync(await csrfResponse.Content.ReadAsStreamAsync());
        var contexts = factory.Services.GetRequiredService<IDbContextFactory<ControlDbContext>>();
        long sourceId;
        await using (var database = await contexts.CreateDbContextAsync())
        {
            var source = new MediaSource { Name = "窗口音频租约夹具", Uri = "audio-intent.mp4", SourceType = MediaSourceType.Video };
            database.MediaSources.Add(source);
            var session = await database.PlaybackSessions.SingleAsync(value => value.WindowId == 2);
            session.Volume = 0;
            session.IsMuted = true;
            var group = await database.RuntimeGroupControls.SingleAsync();
            group.State = RuntimeGroupState.Armed;
            group.GroupEpoch = 1;
            await database.SaveChangesAsync();
            sourceId = source.Id;
        }

        var path = "/api/playback/2/open/";
        object body = new { source_id = sourceId, autoplay = true };
        if (fromScenario)
        {
            using var create = new HttpRequestMessage(HttpMethod.Post, "/api/scenarios/")
            {
                Content = JsonContent.Create(new
                {
                    name = "声音配置预设", big_screen_mode_state = "unset", volume_state = "unset",
                    targets = new[] { new { window_id = 2, source_state = "set", source_id = sourceId, autoplay = true, resume = false } },
                }),
            };
            create.Headers.Add("X-CSRFToken", csrf.RootElement.GetProperty("csrfToken").GetString());
            using var created = await client.SendAsync(create);
            created.EnsureSuccessStatusCode();
            using var scenario = await JsonDocument.ParseAsync(await created.Content.ReadAsStreamAsync());
            path = $"/api/scenarios/{scenario.RootElement.GetProperty("scenario").GetProperty("id").GetInt64()}/activate/";
            body = new { };
        }
        using var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body) };
        request.Headers.Add("X-CSRFToken", csrf.RootElement.GetProperty("csrfToken").GetString());
        using var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        var command = await factory.Services.GetRequiredService<CommandRepository>().ClaimAsync(
            new ClaimCommand(CommandTargetKind.Display, 2, Guid.NewGuid(), 1, 1, TimeSpan.FromSeconds(30)));
        Assert.NotNull(command);
        Assert.Equal("OPEN", command.Command);
        using var args = JsonDocument.Parse(command.ArgsJson);
        Assert.Equal(0, args.RootElement.GetProperty("volume").GetInt32());
        Assert.True(args.RootElement.GetProperty("muted").GetBoolean());
    }
}
