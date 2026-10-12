// 现场音频与摄像机合同；未绑定与仿真不能冒充已执行。
using System.Text.Json.Serialization;

namespace ScpCv.Contracts.Http;

public sealed record VenueAudioSettings
{
    [JsonPropertyName("screen_volume")] public int? ScreenVolume { get; init; }
    [JsonPropertyName("handheld_volume")] public int? HandheldVolume { get; init; }
    [JsonPropertyName("hanging_volume")] public int? HangingVolume { get; init; }
    [JsonPropertyName("source")] public string? Source { get; init; }
}

public sealed record VenueAudioResult
{
    [JsonPropertyName("status")] public string Status { get; init; } = "not_sent";
    [JsonPropertyName("detail")] public string Detail { get; init; } = string.Empty;
    [JsonPropertyName("screen_volume")] public int? ScreenVolume { get; init; }
}

public sealed record CameraControlRequest
{
    [JsonPropertyName("direction")] public string Direction { get; init; } = string.Empty;
    [JsonPropertyName("position")] public int? Position { get; init; }
}
