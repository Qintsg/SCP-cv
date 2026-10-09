// 布局意图和兼容预设状态投影；历史空记录不伪装成实体确认。
using System.Text.Json;
using ScpCv.Domain.Model;
using ScpCv.Domain.Rules;

namespace ScpCv.Infrastructure.VideoWall;

public static class VideoWallStateProjection
{
    public static void Record(RuntimeState state, WallLayout layout, long revision, bool hardware)
    {
        state.WallLayoutAppliedJson = JsonSerializer.Serialize(layout);
        state.WallLayoutAppliedRevision = revision;
        state.BigScreenMode = WallLayoutPolicy.CapturedPreset(layout) ?? BigScreenMode.Custom;
        state.WallLayoutDispatchStatus = hardware ? "sent_unconfirmed" : "simulated";
        state.WallLayoutDispatchDetail = hardware ? "控制帧已写出；尚无与本次操作关联的实体画面确认。" : "模拟应用；未向实体墙面发送命令。";
    }

    public static WallLayout? ReadApplied(RuntimeState state) =>
        string.IsNullOrWhiteSpace(state.WallLayoutAppliedJson) || state.WallLayoutAppliedJson == "{}"
            ? null : JsonSerializer.Deserialize<WallLayout>(state.WallLayoutAppliedJson);

    public static IReadOnlyList<int> MutedWindows(RuntimeState state)
    {
        var applied = ReadApplied(state);
        return applied is null ? state.BigScreenMode == BigScreenMode.Single ? [2] : [] : MutedWindows(applied);
    }

    public static IReadOnlyList<int> MutedWindows(WallLayout layout)
    {
        var used = layout.Mappings.Select(mapping => mapping.Input.Kind).ToHashSet();
        var muted = new List<int>();
        if (!used.Contains(WallInputKind.Window1)) muted.Add(1);
        if (!used.Contains(WallInputKind.Window2)) muted.Add(2);
        return muted;
    }
}
