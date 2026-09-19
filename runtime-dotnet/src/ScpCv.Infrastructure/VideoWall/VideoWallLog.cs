using Microsoft.Extensions.Logging;

namespace ScpCv.Infrastructure.VideoWall;

/// <summary>
/// 视频墙下发的日志条目（FR-018）。字段只有运行态模式、阶段、节点 <c>IP:端口</c>、包数与失败计数：
/// 这条链路不携带凭据，日志里也不得出现凭据——拼接屏节点只接受写入，没有任何认证参数可记。
/// </summary>
internal static partial class VideoWallLog
{
    [LoggerMessage(
        EventId = 4100,
        Level = LogLevel.Information,
        Message = "视频墙下发完成：模式 {Mode}，控制包 {PacketCount} 个")]
    public static partial void DispatchSucceeded(ILogger logger, string mode, int packetCount);

    [LoggerMessage(
        EventId = 4101,
        Level = LogLevel.Error,
        Message = "视频墙下发失败：模式 {Mode}，中止于阶段 {Phase}，失败节点 {FailedNodeCount} 个，首个原因：{Reason}")]
    public static partial void DispatchFailed(
        ILogger logger,
        string mode,
        string phase,
        int failedNodeCount,
        string reason);

    [LoggerMessage(
        EventId = 4102,
        Level = LogLevel.Information,
        Message = "视频墙节点重试后成功：{Ip}:{Port} 阶段 {Phase} 第 {Attempt}/{MaxAttempts} 次尝试")]
    public static partial void RetrySucceeded(
        ILogger logger,
        string ip,
        int port,
        string phase,
        int attempt,
        int maxAttempts);

    [LoggerMessage(
        EventId = 4103,
        Level = LogLevel.Information,
        Message = "视频墙下发已跳过（Simulation）：模式 {Mode}，本应下发 {PacketCount} 个控制包，未建立任何连接")]
    public static partial void DispatchSkipped(ILogger logger, string mode, int packetCount);
}
