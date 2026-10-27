// 文件描述：共享视频的主机编码策略。项目：SCP-cv；文件：ScreenShareVideoPolicy.cs；作者：Qintsg；日期：2026-10-27。
namespace ScpCv.ControlHost.ScreenSharing;

public sealed record ScreenShareVideoPolicy
{
    public int FullHdBitrate { get; init; } = 16_000_000;
    public int QhdBitrate { get; init; } = 24_000_000;
    public int UltraHdBitrate { get; init; } = 48_000_000;
    public int MaxFrameRate { get; init; } = 30;

    /// <summary>:returns: 码率递增且参数符合编码范围。:exception InvalidOperationException: 主机配置无效。</summary>
    public void Validate()
    {
        if (FullHdBitrate < 1_000_000 || QhdBitrate < FullHdBitrate || UltraHdBitrate < QhdBitrate ||
            UltraHdBitrate > 200_000_000 || MaxFrameRate is < 1 or > 30)
            throw new InvalidOperationException("屏幕共享视频策略无效：码率应递增并位于1–200 Mbps，帧率为1–30。");
    }
}
