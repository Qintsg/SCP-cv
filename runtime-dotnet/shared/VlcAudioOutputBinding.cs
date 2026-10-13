// 两个VLC进程共用的显式输出接线；只影响自有MediaPlayer，不修改系统音量和默认设备。
using LibVLCSharp.Shared;
using ScpCv.Contracts.Runtime;

namespace ScpCv.Runtime.Audio;

public static class VlcAudioOutputBinding
{
    /// <summary>:param player: 自有播放器。:param configured: 可信启动端点ID。:returns: 播放前逐次核验并绑定；未配置保持既有行为。</summary>
    public static void Apply(MediaPlayer player, string? configured)
    {
        if (string.IsNullOrWhiteSpace(configured)) return;
        if (!player.SetAudioOutput("mmdevice"))
            throw new InvalidOperationException("VLC无法使用Windows音频输出模块，未按默认设备播放。");
        var selected = Validate(player, configured);
        // 显式module仅写下次输出模块的配置；当前mmdevice已创建，必须切它的真实设备。
        player.SetOutputDevice(deviceId: selected!);
    }

    /// <summary>:param player: 已建立输出模块的播放器。:param configured: 期望端点。:returns: 核验端点但不重置正在播放/暂停的输出模块。</summary>
    public static string? Validate(MediaPlayer player, string? configured) =>
        string.IsNullOrWhiteSpace(configured) ? null :
            AudioOutputBinding.Resolve(configured, player.AudioOutputDeviceEnum.Select(device => device.DeviceIdentifier));

    /// <summary>:param player: 已发起播放且保持实例静音的播放器。:param configured: 完整端点。:param token: 命令寿命。:returns: 在五秒内确认原生输出，初始空ID不冒充错绑或成功。</summary>
    public static async Task ConfirmPlaybackAsync(MediaPlayer player, string? configured, CancellationToken token = default)
    {
        if (string.IsNullOrWhiteSpace(configured)) return;
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
        deadline.CancelAfter(TimeSpan.FromSeconds(5));
        while (true)
        {
            token.ThrowIfCancellationRequested();
            var state = player.State;
            if (state == VLCState.Playing)
            {
                // 无音轨媒体没有音频输出；不能要求它产生不存在的端点读回。
                if (player.AudioTrackCount == 0) return;
                var actual = player.OutputDevice;
                if (!string.IsNullOrEmpty(actual))
                {
                    if (!AudioOutputBinding.Matches(configured.Trim(), actual))
                        throw new InvalidOperationException("软件音频实际输出与绑定不一致，未按默认设备播放。");
                    return;
                }
            }
            else if (state is VLCState.Error or VLCState.Ended or VLCState.Stopped)
                throw new InvalidOperationException("绑定音频输出确认前媒体已结束或失败。");
            try { await Task.Delay(25, deadline.Token).ConfigureAwait(false); }
            catch (OperationCanceledException) when (!token.IsCancellationRequested)
            { throw new TimeoutException("五秒内未确认绑定的软件音频输出，未回退系统默认设备。"); }
        }
    }

    /// <summary>:param player: 自有播放器。:param configured: 期望端点。:returns: 有实际音轨后检查原生输出，不把设置意图当已出声。</summary>
    public static bool HasMismatch(MediaPlayer player, string? configured) =>
        !string.IsNullOrWhiteSpace(configured) && player.State == VLCState.Playing && player.AudioTrackCount > 0 &&
        !AudioOutputBinding.Matches(configured.Trim(), player.OutputDevice);
}
