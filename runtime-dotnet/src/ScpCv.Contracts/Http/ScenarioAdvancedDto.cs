// 高级预设中null或未提交表示保持，不表示调用默认位置或重置音量。
using System.Text.Json.Serialization;

namespace ScpCv.Contracts.Http;

public sealed record ScenarioAdvancedDto
{
    [JsonPropertyName("camera_positions")] public IReadOnlyDictionary<string, int?> CameraPositions { get; init; } = new Dictionary<string, int?>();
    [JsonPropertyName("audio")] public VenueAudioSettings? Audio { get; init; }
}
