// 分享会话快照与本机私有授权；快照不得包含接收token。SCP-cv 2026-10-10。
namespace ScpCv.ControlHost.ScreenSharing;

public enum ScreenShareState
{
    Allocated,
    Negotiating,
    Receiving,
    Stopping,
    Closed,
    Failed,
}

public sealed record ScreenShareSdp(string Type, string Sdp);

public sealed record ScreenShareSnapshot(
    Guid Id,
    int WindowId,
    ScreenShareState State,
    bool HasAudio,
    bool ReceiverHasAudio,
    long? SourceGeneration,
    DateTimeOffset ExpiresAt,
    long ActualFrames,
    DateTimeOffset? LastFrameAt,
    ScreenShareSdp? Answer,
    string StopReason)
{
    public ReceiverQuality? ReceiverQuality { get; init; }
    public int? ActualVolume { get; init; }
    public bool? ActualMuted { get; init; }
}

/// <summary>仅供已认证Worker领取的内存能力，不允许自动格式化输出token。</summary>
public sealed class ScreenShareReceiverGrant(Guid id, int windowId, long sourceGeneration, string token)
{
    public Guid Id { get; } = id;
    public int WindowId { get; } = windowId;
    public long SourceGeneration { get; } = sourceGeneration;
    public string Token { get; } = token;
    public override string ToString() => $"ScreenShareReceiverGrant({Id}, window={WindowId}, generation={SourceGeneration})";
}

/// <summary>公开失败仅包含稳定错误码和说明，不包含信令或接收凭据。</summary>
public sealed class ScreenShareException : Exception
{
    public ScreenShareException() : this("分享请求无效。") { }
    public ScreenShareException(string message) : base(message) { }
    public ScreenShareException(string message, Exception innerException) : base(message, innerException) { }
    public ScreenShareException(string message, string code, int statusCode = 400) : base(message)
    {
        Code = code;
        StatusCode = statusCode;
    }

    public string Code { get; } = "share_invalid";
    public int StatusCode { get; } = 400;
}
