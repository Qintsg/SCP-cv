// 输出路由仅记录完整写出意图；与实体画面实时反馈分开。
using System.Text.Json.Serialization;

namespace ScpCv.Contracts.Http;

public sealed record DisplayRoutingIntentDto
{
    [JsonPropertyName("input")] public VideoWallInputDto? Input { get; init; }
    [JsonPropertyName("layout_code")] public string? LayoutCode { get; init; }
}

public sealed record DisplayRoutingTargetDto
{
    [JsonPropertyName("target")] public string Target { get; init; } = string.Empty;
    [JsonPropertyName("status")] public string Status { get; init; } = "unknown";
    [JsonPropertyName("last_sent")] public DisplayRoutingIntentDto? LastSent { get; init; }
    [JsonPropertyName("updated_at")] public DateTimeOffset? UpdatedAt { get; init; }
}

public sealed record TelevisionRouteRequest
{
    [JsonPropertyName("input")] public VideoWallInputDto? Input { get; init; }
    [JsonPropertyName("force")] public bool Force { get; init; } = true;
}

public sealed record ScenarioTelevisionDto
{
    [JsonPropertyName("state")] public string State { get; init; } = "keep";
    [JsonPropertyName("input")] public VideoWallInputDto? Input { get; init; }
}

public sealed record ScenarioTelevisionsDto
{
    [JsonPropertyName("tv_left")] public ScenarioTelevisionDto TvLeft { get; init; } = new();
    [JsonPropertyName("tv_right")] public ScenarioTelevisionDto TvRight { get; init; } = new();
}
