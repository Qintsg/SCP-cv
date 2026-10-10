// 只在认证租约中注入短期接收URI；fragment凭据不会进入HTTP请求目标或数据库。SCP-cv 2026-10-10。
using System.Globalization;
using System.Text.Json;
using ScpCv.Domain.Model;
using ScpCv.Infrastructure.Playback;

namespace ScpCv.ControlHost.ScreenSharing;

public sealed class ScreenShareLeaseAuthority(ScreenShareRegistry registry, ScreenShareOptions options)
{
    /// <summary>已认证不等于属于原共享；实例、owner和group均必须匹配预约身份。</summary>
    public void Enrich(CommandRecord command, ScreenShareTargetIdentity target, Dictionary<string, JsonElement> args)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(args);
        options.Validate();
        if (command.TargetKind != CommandTargetKind.Display || command.TargetId != target.WindowId ||
            !args.TryGetValue("share_id", out var rawId) || !rawId.TryGetGuid(out var id))
            throw new ScreenShareException("分享接收授权已失效。", "share_receiver_denied", 404);
        var grant = registry.GetReceiverGrant(id, target, command.SourceGeneration);
        var uri = new UriBuilder(new Uri(options.ReceiverBaseUri, $"/api/screen-share/receiver/{id:D}/"))
        {
            Query = string.Create(CultureInfo.InvariantCulture, $"window_id={target.WindowId}&source_generation={command.SourceGeneration}"),
            Fragment = grant.Token,
        };
        args["receiver_uri"] = JsonSerializer.SerializeToElement(uri.Uri.AbsoluteUri);
    }

    public static bool IsScreenShareOpen(string command, Dictionary<string, JsonElement> args) =>
        command == "OPEN" && args.TryGetValue("source_type", out var kind) &&
        kind.ValueKind == JsonValueKind.String && kind.GetString() == "screen_share";

    /// <summary>:param target: 已认证领取者。:returns: 同原共享Worker才允许领取；不向音频命令注入接收URI。</summary>
    public void AuthorizeAudio(CommandRecord command, ScreenShareTargetIdentity target, Dictionary<string, JsonElement> args)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(args);
        if (command.TargetKind != CommandTargetKind.Display || command.TargetId != target.WindowId || args.Count != 4 ||
            !args.TryGetValue("share_id", out var raw) || raw.ValueKind != JsonValueKind.String || !raw.TryGetGuid(out var id) ||
            !args.TryGetValue("window_id", out var window) || window.ValueKind != JsonValueKind.Number ||
            !window.TryGetInt32(out var windowId) || windowId != target.WindowId ||
            !args.TryGetValue("volume", out var level) || level.ValueKind != JsonValueKind.Number ||
            !level.TryGetInt32(out var volume) || volume is < 0 or > 100 ||
            !args.TryGetValue("muted", out var mute) || mute.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
            throw new ScreenShareException("分享音频授权已失效。", "share_receiver_denied", 404);
        registry.GetReceiverGrant(id, target, command.SourceGeneration);
    }
}
