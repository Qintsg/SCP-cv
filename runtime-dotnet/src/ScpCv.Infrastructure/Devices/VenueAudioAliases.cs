// 用户确认悬挂与原头戴1/2为同一组；集中规范化，防止旧意图漏执行或同组重复发送。
using ScpCv.Contracts.Http;

namespace ScpCv.Infrastructure.Devices;

public static class VenueAudioAliases
{
    /// <summary>:param settings: 新请求、旧持久配置或高级预设。:returns: Join1唯一的headset值；不同值不擅自选取。</summary>
    public static VenueAudioSettings Normalize(VenueAudioSettings settings)
    {
        if (settings.HeadsetVolume is { } headset && settings.HangingVolume is { } hanging && headset != hanging)
            throw new VenueDeviceException($"悬挂（头戴）话筒为同一通道，headset_volume={headset}与hanging_volume={hanging}冲突；未发送任何配置。");
        return settings with { HeadsetVolume = settings.HeadsetVolume ?? settings.HangingVolume, HangingVolume = null };
    }
}
