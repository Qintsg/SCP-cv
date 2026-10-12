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
        player.SetOutputDevice(deviceId: selected!, module: "mmdevice");
    }

    /// <summary>:param player: 已建立输出模块的播放器。:param configured: 期望端点。:returns: 核验端点但不重置正在播放/暂停的输出模块。</summary>
    public static string? Validate(MediaPlayer player, string? configured) =>
        string.IsNullOrWhiteSpace(configured) ? null :
            AudioOutputBinding.Resolve(configured, player.AudioOutputDeviceEnum.Select(device => device.DeviceIdentifier));

    /// <summary>:param player: 自有播放器。:param configured: 期望端点。:returns: 有实际音轨后检查原生输出，不把设置意图当已出声。</summary>
    public static bool HasMismatch(MediaPlayer player, string? configured) =>
        !string.IsNullOrWhiteSpace(configured) && player.State == VLCState.Playing && player.AudioTrackCount > 0 &&
        !AudioOutputBinding.Matches(configured.Trim(), player.OutputDevice);
}
