// 现场音频与摄像机合同；未绑定与仿真不能冒充已执行。
using System.Text.Json.Serialization;

namespace ScpCv.Contracts.Http;

public sealed record VenueAudioSettings
{
    [JsonPropertyName("screen_volume")] public int? ScreenVolume { get; init; }
    [JsonPropertyName("computer_volume")] public int? ComputerVolume { get; init; }
    [JsonPropertyName("handheld_volume")] public int? HandheldVolume { get; init; }
    // Join1的唯一保存/结果字段；HangingVolume是已确认同组的旧输入别名。
    [JsonPropertyName("headset_volume")] public int? HeadsetVolume { get; init; }
    [JsonPropertyName("hanging_volume")] public int? HangingVolume { get; init; }
    [JsonPropertyName("source")] public string? Source { get; init; }
}

public sealed record VenueAudioResult
{
    [JsonPropertyName("source")] public string? Source { get; init; }
    [JsonPropertyName("status")] public string Status { get; init; } = "not_sent";
    [JsonPropertyName("detail")] public string Detail { get; init; } = string.Empty;
    [JsonPropertyName("screen_volume")] public int? ScreenVolume { get; init; }
    [JsonPropertyName("sent_fields")] public IReadOnlyList<string> SentFields { get; init; } = [];
    [JsonPropertyName("failed_field")] public string? FailedField { get; init; }
}

public sealed record CameraControlRequest
{
    [JsonPropertyName("direction")] public string Direction { get; init; } = string.Empty;
    [JsonPropertyName("position")] public int? Position { get; init; }
    [JsonPropertyName("motion_id")] public Guid MotionId { get; init; }
    [JsonPropertyName("phase")] public string Phase { get; init; } = string.Empty;
    [JsonPropertyName("issued_at_ms")] public long IssuedAtMilliseconds { get; init; }
}

public sealed record VenueCameraResult
{
    [JsonPropertyName("status")] public string Status { get; init; } = "not_sent";
    [JsonPropertyName("detail")] public string Detail { get; init; } = string.Empty;
    [JsonPropertyName("motion_id")] public Guid? MotionId { get; init; }
}
