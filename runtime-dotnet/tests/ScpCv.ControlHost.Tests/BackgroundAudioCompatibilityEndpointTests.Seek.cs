// 背景音频既有seek合同通过真实HTTP验证；受理不伪造实际媒体位置。
using System.Net;

namespace ScpCv.ControlHost.Tests;

public sealed partial class BackgroundAudioCompatibilityEndpointTests
{
    /// <summary>:returns: seek按文档受理到命令队列，当前位置仍保持原观察值。</summary>
    [Fact]
    public async Task SeekIsAcceptedWithoutReportingTargetAsObservedPosition()
    {
        using var factory = new ControlHostApplicationFactory();
        using var client = factory.CreateHttpsClient();
        var sourceIds = await SeedAudioSourcesAsync(factory);
        await SeedAudioSnapshotAsync(factory, sourceIds);
        var csrf = await AuthenticateAsync(client);
        using var request = Request(HttpMethod.Post, "/api/background-audio/control/", csrf,
            new { action = "seek", position_ms = 12500L });
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = await Json(response);
        var state = body.RootElement.GetProperty("background_audio").GetProperty("state");
        Assert.Equal("SEEK", state.GetProperty("pending_command").GetString());
        Assert.Equal(sourceIds[0], state.GetProperty("source_id").GetInt64());
        Assert.NotEqual(12500, state.GetProperty("position_ms").GetInt64());
    }

    /// <summary>:param positionJson: 非法或缺失目标。:returns: 参数不被静默转换成0，既有状态不变。</summary>
    [Theory]
    [InlineData("")]
    [InlineData(",\"position_ms\":-1")]
    [InlineData(",\"position_ms\":1.5")]
    [InlineData(",\"position_ms\":\"1200\"")]
    [InlineData(",\"position_ms\":null")]
    [InlineData(",\"position_ms\":9223372036854775808")]
    public async Task InvalidSeekPositionDoesNotChangeAudioState(string positionJson)
    {
        using var factory = new ControlHostApplicationFactory();
        using var client = factory.CreateHttpsClient();
        var ids = await SeedAudioSourcesAsync(factory);
        await SeedAudioSnapshotAsync(factory, ids);
        var csrf = await AuthenticateAsync(client);
        using var beforeResponse = await client.GetAsync("/api/background-audio/");
        using var before = await Json(beforeResponse);
        using var request = Request(HttpMethod.Post, "/api/background-audio/control/", csrf, new { });
        request.Content = new StringContent("{\"action\":\"seek\"" + positionJson + "}", System.Text.Encoding.UTF8, "application/json");
        using var response = await client.SendAsync(request);
        using var error = await Json(response);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("invalid_position", error.RootElement.GetProperty("code").GetString());
        using var afterResponse = await client.GetAsync("/api/background-audio/");
        using var after = await Json(afterResponse);
        Assert.Equal(before.RootElement.GetProperty("background_audio").GetRawText(), after.RootElement.GetProperty("background_audio").GetRawText());
    }

    /// <summary>:returns: 空源的合法目标也不入队，身份与观察不被伪造。</summary>
    [Fact]
    public async Task SeekWithoutSourceIsRejected()
    {
        using var factory = new ControlHostApplicationFactory();
        using var client = factory.CreateHttpsClient();
        var csrf = await AuthenticateAsync(client);
        using var request = Request(HttpMethod.Post, "/api/background-audio/control/", csrf, new { action = "seek", position_ms = 0L });
        using var response = await client.SendAsync(request);
        using var body = await Json(response);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("no_source", body.RootElement.GetProperty("code").GetString());
    }
}
