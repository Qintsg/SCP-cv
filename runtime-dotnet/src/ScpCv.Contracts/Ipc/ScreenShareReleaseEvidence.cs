// 原Worker的非秘密共享释放凭据；只在原生Dispose成功后产生，不能由网页closed消息替代。
using System.Text.Json.Serialization;

namespace ScpCv.Contracts.Ipc;

public sealed record ScreenShareReleaseEvidenceDto
{
    [JsonPropertyName("share_id")]
    public Guid ShareId { get; init; }

    [JsonPropertyName("source_generation")]
    public long SourceGeneration { get; init; }
}
