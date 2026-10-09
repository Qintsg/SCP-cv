// goto严格输入及first/last既有派生语义保护；不扩大wire页数上限或其它媒体seek规则。
using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ScpCv.Infrastructure.Persistence;

namespace ScpCv.ControlHost.Tests;

public sealed partial class PresentationNavigationInputEndpointTests
{
    [Theory]
    [InlineData("null")]
    [InlineData("\"2\"")]
    [InlineData("1.5")]
    [InlineData("2147483648")]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("true")]
    [InlineData("[]")]
    [InlineData("{}")]
    public async Task InvalidGotoTargetShapeIsRejectedWithoutChangingIntentOrSessions(string targetJson)
    {
        using var factory = new ControlHostApplicationFactory();
        using var client = factory.CreateHttpsClient();
        var csrf = await UploadAndOpenAsync(client);
        var beforeSessions = await ReadSessionsAsync(client);
        var beforeCommands = await ReadCommandsAsync(factory);
        using var input = JsonDocument.Parse($$"""{"action":"goto","target_index":{{targetJson}}} """);
        using var request = JsonRequest(HttpMethod.Post, "/api/playback/1/navigate/", csrf, input.RootElement);

        using var response = await client.SendAsync(request);
        using var error = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("invalid_navigation", error.RootElement.GetProperty("code").GetString());
        Assert.Equal(beforeSessions, await ReadSessionsAsync(client));
        Assert.Equal(beforeCommands, await ReadCommandsAsync(factory));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(7)]
    [InlineData(500)]
    public async Task PositiveGotoTargetKeepsItsExplicitRequestedPage(int target)
    {
        using var factory = new ControlHostApplicationFactory();
        using var client = factory.CreateHttpsClient();
        var csrf = await UploadAndOpenAsync(client);
        using var request = JsonRequest(HttpMethod.Post, "/api/playback/1/navigate/", csrf,
            new { action = "goto", target_index = target });

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var contexts = factory.Services.GetRequiredService<IDbContextFactory<ControlDbContext>>();
        await using var database = await contexts.CreateDbContextAsync();
        var command = Assert.Single(await database.CommandRecords.AsNoTracking().Where(command => command.Command == "GOTO").ToArrayAsync());
        using var args = JsonDocument.Parse(command.ArgsJson);
        Assert.Equal("goto", args.RootElement.GetProperty("action").GetString());
        Assert.Equal(target, args.RootElement.GetProperty("target_index").GetInt32());
    }

    [Theory]
    [InlineData("first", 1)]
    [InlineData("last", 7)]
    public async Task FirstAndLastWithoutTargetKeepTheirKnownSlideCountDerivation(string action, int expectedTarget)
    {
        using var factory = new ControlHostApplicationFactory();
        using var client = factory.CreateHttpsClient();
        var csrf = await UploadAndOpenAsync(client);
        // 仅设置自有fixture的既有实际总页数，不把HTTP受理冒称原生观测。
        await factory.Services.GetRequiredService<WriteCoordinator>().ExecuteAsync(async (database, token) =>
        {
            var session = await database.PlaybackSessions.SingleAsync(session => session.WindowId == 1, token);
            session.TotalSlides = 7;
        });
        using var request = JsonRequest(HttpMethod.Post, "/api/playback/1/navigate/", csrf, new { action });

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var contexts = factory.Services.GetRequiredService<IDbContextFactory<ControlDbContext>>();
        await using var check = await contexts.CreateDbContextAsync();
        var command = Assert.Single(await check.CommandRecords.AsNoTracking().Where(command => command.Command == "GOTO").ToArrayAsync());
        using var args = JsonDocument.Parse(command.ArgsJson);
        Assert.Equal("goto", args.RootElement.GetProperty("action").GetString());
        Assert.Equal(expectedTarget, args.RootElement.GetProperty("target_index").GetInt32());
    }
}
