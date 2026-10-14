// VLC窗口音频意图严格读取；无字段保留旧内部合同，畸形/越界在原生副作用前拒绝。
using System.IO;
using System.Text.Json;
using LibVLCSharp.Shared;
using ScpCv.Contracts.Ipc;

namespace ScpCv.PlayerWorker.Playback;

public sealed partial class PlayerRuntimeHost
{
    /// <summary>:param lease: 原认证OPEN租约。:returns: 明确字段或已记忆默认；不把无效值变为100/false。</summary>
    private (int? Volume, bool? Muted) ReadVlcAudioIntent(CommandLeaseDto lease)
    {
        var volume = _vlcVolume;
        var muted = _vlcMuted;
        if (lease.Args.TryGetValue("volume", out var level))
        {
            if (level.ValueKind != JsonValueKind.Number || !level.TryGetInt32(out var number) || number is < 0 or > 100)
                throw new InvalidDataException("VLC音量必须是0–100的整数，未创建播放资源。");
            volume = number;
        }
        if (lease.Args.TryGetValue("muted", out var mute))
        {
            if (mute.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
                throw new InvalidDataException("VLC静音必须是布尔值，未创建播放资源。");
            muted = mute.GetBoolean();
        }
        return (volume, muted);
    }

    /// <summary>:param player: 自有VLC。:param volume: 已校验音量。:param muted: 已校验静音。:param token: 命令寿命。:returns: 先静音调整、再核原生属性，超时保持静音并失败。</summary>
    private static async Task ApplyVlcAudioConfirmedAsync(MediaPlayer player, int volume, bool muted, CancellationToken token)
    {
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(token);
        budget.CancelAfter(TimeSpan.FromSeconds(2));
        try
        {
            player.Mute = true;
            if (player.AudioTrackCount > 0)
                await WaitForVlcAudioAsync(player, null, true, budget.Token);
            player.Volume = volume;
            if (player.AudioTrackCount > 0)
                await WaitForVlcAudioAsync(player, volume, true, budget.Token);
            player.Mute = muted;
            if (player.AudioTrackCount > 0)
                await WaitForVlcAudioAsync(player, volume, muted, budget.Token);
        }
        catch (OperationCanceledException) when (!token.IsCancellationRequested)
        {
            player.Mute = true;
            throw new TimeoutException("两秒内未确认VLC音量/静音，当前资源保持静音。");
        }
        catch { player.Mute = true; throw; }
    }

    /// <summary>:param player: 当前自有实例。:param volume: 可选原生音量。:param muted: 原生静音。:param token: 有界寿命。:returns: 属性真实达到目标后返回。</summary>
    private static async Task WaitForVlcAudioAsync(MediaPlayer player, int? volume, bool muted, CancellationToken token)
    {
        while (true)
        {
            token.ThrowIfCancellationRequested();
            if ((volume is null || player.Volume == volume.Value) && player.Mute == muted) return;
            await Task.Delay(20, token);
        }
    }
}
