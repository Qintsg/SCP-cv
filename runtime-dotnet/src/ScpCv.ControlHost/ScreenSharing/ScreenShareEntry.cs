// 内存分享条目只能在Registry锁内访问，不能交给HTTP序列化。SCP-cv 2026-10-10。
namespace ScpCv.ControlHost.ScreenSharing;

internal sealed class ScreenShareEntry
{
    internal required Guid Id { get; init; }
    internal required string OwnerId { get; init; }
    internal required ScpCv.Infrastructure.Playback.ScreenShareTargetIdentity Target { get; init; }
    internal int WindowId => Target.WindowId;
    internal required bool HasAudio { get; init; }
    internal ScreenShareVideoPolicy VideoPolicy { get; init; } = new();
    internal required DateTimeOffset CreatedAt { get; init; }
    internal required DateTimeOffset LastPublisherSeen { get; set; }
    internal required DateTimeOffset ExpiresAt { get; set; }
    internal ScreenShareState State { get; set; } = ScreenShareState.Allocated;
    internal long? SourceGeneration { get; set; }
    internal string? ReceiverToken { get; set; }
    internal ScreenShareSdp? Offer { get; set; }
    internal ScreenShareSdp? Answer { get; set; }
    internal long ActualFrames { get; set; }
    internal DateTimeOffset? LastFrameAt { get; set; }
    internal bool ReceiverHasAudio { get; set; }
    internal ReceiverQuality? Quality { get; set; }
    internal bool NativeReleased { get; set; }
    internal string StopReason { get; set; } = "";
    internal DateTimeOffset? TerminalAt { get; set; }
    internal bool IsTerminal => State is ScreenShareState.Closed or ScreenShareState.Failed;

    internal ScreenShareSnapshot Snapshot() => new(
        Id, WindowId, State, HasAudio, ReceiverHasAudio, SourceGeneration,
        ExpiresAt, ActualFrames, LastFrameAt, Answer, StopReason) { ReceiverQuality = Quality, VideoPolicy = VideoPolicy };
}
