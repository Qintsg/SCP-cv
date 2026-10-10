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
}
