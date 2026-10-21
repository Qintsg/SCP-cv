// 布局意图和兼容预设状态投影；历史空记录不伪装成实体确认。
using System.Text.Json;
using ScpCv.Domain.Model;
using ScpCv.Infrastructure.DisplayRouting;
using ScpCv.Infrastructure.Devices;
using ScpCv.Domain.Rules;

namespace ScpCv.Infrastructure.VideoWall;

public static class VideoWallStateProjection
{
    public static void Record(RuntimeState state, WallLayout layout, long revision, bool hardware, VideoWallProtocolOptions? protocol = null)
    {
        state.WallLayoutAppliedJson = JsonSerializer.Serialize(layout);
        state.WallLayoutAppliedRevision = revision;
        state.BigScreenMode = WallLayoutPolicy.CapturedPreset(layout) ?? BigScreenMode.Custom;
        state.WallLayoutDispatchStatus = hardware ? "sent_unconfirmed" : "simulated";
        state.WallLayoutDispatchDetail = hardware ? "控制帧已写出；尚无与本次操作关联的实体画面确认。" : "模拟应用；未向实体墙面发送命令。";
        DisplayRoutingState.RecordWall(state, layout, protocol ?? new(), hardware, state.UpdatedAt);
    }

    public static WallLayout? ReadApplied(RuntimeState state) =>
        string.IsNullOrWhiteSpace(state.WallLayoutAppliedJson) || state.WallLayoutAppliedJson == "{}"
            ? null : JsonSerializer.Deserialize<WallLayout>(state.WallLayoutAppliedJson);

    public static IReadOnlyList<int> MutedWindows(RuntimeState state, VideoWallProtocolOptions? protocol = null)
    {
        var applied = ReadApplied(state);
        return applied is null ? state.BigScreenMode == BigScreenMode.Single ? [2] : [] : MutedWindows(applied, protocol);
    }

    public static IReadOnlyList<int> MutedWindows(WallLayout layout, VideoWallProtocolOptions? protocol = null)
    {
        var options = protocol ?? new();
        var used = layout.Mappings.Select(mapping => SiteInputCatalog.ResolveDescriptor(mapping.Input, options)?.UnicastAddress)
            .Where(address => address is not null).ToHashSet(StringComparer.Ordinal);
        var muted = new List<int>();
        if (!used.Contains(SiteInputCatalog.ResolveDescriptor("window_1", null, options)?.UnicastAddress)) muted.Add(1);
        if (!used.Contains(SiteInputCatalog.ResolveDescriptor("window_2", null, options)?.UnicastAddress)) muted.Add(2);
        return muted;
    }
}
