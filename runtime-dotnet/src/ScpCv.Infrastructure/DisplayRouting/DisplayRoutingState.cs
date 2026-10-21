// 每输出逻辑记账与帧级指纹；布局名称/来源别名不造成假切换，未知记录不用于跳过。
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ScpCv.Contracts.Http;
using ScpCv.Domain.Model;
using ScpCv.Domain.Rules;
using ScpCv.Infrastructure.VideoWall;

namespace ScpCv.Infrastructure.DisplayRouting;

public sealed record OutputDispatchRecord(string Fingerprint, string Status, DisplayRoutingIntentDto? LastSent, DateTimeOffset UpdatedAt);

public static class DisplayRoutingState
{
    public static IReadOnlyList<string> Targets { get; } = ["wall", "tv_left", "tv_right"];

    /// <summary>:param state: 当前库快照。:param target: 固定输出。:returns: 无有效结构时返回未知，不修补为成功。</summary>
    public static OutputDispatchRecord? Read(RuntimeState state, string target)
    {
        try { return JsonSerializer.Deserialize<Dictionary<string, OutputDispatchRecord>>(state.DisplayRoutingJson)?.GetValueOrDefault(target); }
        catch (JsonException) { return null; }
    }

    /// <summary>:param state: 已在写事务中的实体。:param target: 固定输出。:param record: 新记录。</summary>
    public static void Write(RuntimeState state, string target, OutputDispatchRecord record)
    {
        Dictionary<string, OutputDispatchRecord> records;
        try { records = JsonSerializer.Deserialize<Dictionary<string, OutputDispatchRecord>>(state.DisplayRoutingJson) ?? []; }
        catch (JsonException) { records = []; }
        records[target] = record;
        state.DisplayRoutingJson = JsonSerializer.Serialize(records);
    }

    /// <summary>:param state: 当前快照。:param target: 输出。:param fingerprint: 实际序列指纹。:param hardware: 硬件或模拟边界。:returns: 只有同边界完整成功记录可复用。</summary>
    public static bool Matches(RuntimeState state, string target, string fingerprint, bool hardware)
    {
        var record = Read(state, target);
        return record is { LastSent: not null } && record.Status == (hardware ? "sent_unconfirmed" : "simulated") &&
            record.Fingerprint == fingerprint && (target != "wall" || state.WallLayoutDispatchStatus == record.Status);
    }

    /// <summary>:param state: 写事务实体。:param target: 固定输出。:param status: 非成功状态。:param now: UTC时间。</summary>
    public static void Invalidate(RuntimeState state, string target, string status, DateTimeOffset now)
    {
        var previous = Read(state, target);
        Write(state, target, new(string.Empty, status, previous?.LastSent, now));
    }

    /// <summary>:param layout: 已预检布局。:param options: 当前源和协议参数。:returns: 按真实目标与包字节计算，包含几何及源尺寸。</summary>
    public static string WallFingerprint(WallLayout layout, VideoWallProtocolOptions options) => Digest(
        VideoWallLayoutPlanner.Build(layout, options).Select(item => $"{item.Ip}:{item.Port}:{Convert.ToHexString(item.Packet)}"));

    /// <summary>:param frames: 实际将写出的帧。:param target: 固定目标。:returns: 不含用户可控名称的指纹。</summary>
    public static string TelevisionFingerprint(string target, IReadOnlyList<byte[]> frames) =>
        Digest(frames.Select(frame => $"{TelevisionTargets.Address(target)}:4830:{Convert.ToHexString(frame)}"));

    /// <summary>:param state: 写事务实体。:param layout: 完整发出的布局。:param options: 相同协议快照。:param hardware: 发送边界。:param now: 时间。</summary>
    public static void RecordWall(RuntimeState state, WallLayout layout, VideoWallProtocolOptions options, bool hardware, DateTimeOffset now) =>
        Write(state, "wall", new(WallFingerprint(layout, options), hardware ? "sent_unconfirmed" : "simulated",
            new() { LayoutCode = WallLayoutCode.Encode(layout) }, now));

    /// <summary>:param state: 启动时加载的实体。:param now: 时间。:returns: 是否清理了上次被中断的发送。</summary>
    public static bool Recover(RuntimeState state, DateTimeOffset now)
    {
        var changed = false;
        foreach (var target in Targets.Where(target => Read(state, target)?.Status == "dispatching"))
        { Invalidate(state, target, "uncertain", now); changed = true; }
        return changed;
    }

    /// <summary>:param values: 已规范的有序帧。:returns: 私有比较摘要，不向API暴露。</summary>
    private static string Digest(IEnumerable<string> values) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('\n', values))));
}
