// 仅替换外部WebView边界的测试接缝；播放身份、后帧和宿主生命周期仍由正式路径校验。
using System.Windows;

namespace ScpCv.PlayerWorker.ScreenSharing;

internal interface IScreenShareSurface : IAsyncDisposable
{
    FrameworkElement Surface { get; }
    ScreenSharePlaybackState State { get; }

    /// <summary>:param volume: 窗口音量。:param muted: 窗口静音。:param token: 本次OPEN的有限寿命。:returns: 实际接收页激活完成；宿主仍须独立核后帧。</summary>
    Task ActivateAsync(int volume, bool muted, CancellationToken token);

    /// <summary>:param volume: 专用音量。:param muted: 专用静音。:param token: 控制寿命。:returns: 真实接收页属性确认后更新本资源状态，不调用VLC。</summary>
    Task ApplyAudioAsync(int volume, bool muted, CancellationToken token);
}
