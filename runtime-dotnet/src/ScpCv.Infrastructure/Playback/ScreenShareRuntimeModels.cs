// 分享运行意图与实际完成分离；不包含SDP、接收URI或凭据。SCP-cv 2026-10-10。
namespace ScpCv.Infrastructure.Playback;

public sealed record ScreenShareTargetIdentity(int WindowId, Guid WorkerInstanceId, long OwnerEpoch, long GroupEpoch);

public sealed record ScreenShareOpenReceipt(Guid CommandId, int WindowId, long SourceGeneration);

public enum ScreenShareCloseDisposition
{
    Queued,
    NoLongerDesired,
}

/// <summary>NoLongerDesired只代表旧分享不再是当前意图，不证明原生资源已退出。</summary>
public sealed record ScreenShareCloseReceipt(
    ScreenShareCloseDisposition Disposition,
    Guid? CommandId,
    long? CloseGeneration);
