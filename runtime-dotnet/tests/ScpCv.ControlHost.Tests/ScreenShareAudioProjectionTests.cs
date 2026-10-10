// 分享音频实际投影的schema、空值、乱序和两目标隔离；不推断声卡出声。SCP-cv 2026-10-11。
using System.Text.Json;
using ScpCv.Contracts.Http;
using ScpCv.Infrastructure.Playback;

namespace ScpCv.ControlHost.Tests;

public sealed class ScreenShareAudioProjectionTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-10-11T00:00:00Z", System.Globalization.CultureInfo.InvariantCulture);

    [Theory]
    [InlineData(true, false)]
    [InlineData(true, true)]
    [InlineData(false, true)]
    public void ActualAudioPropertiesRequireMatchingReceipt(bool audio, bool muted)
    {
        var (projection, target, id) = Bind();
        Observe(projection, target, State(id, audio, 29, muted), 1);
        var actual = Snapshot(projection);
        Assert.Equal(29, actual.GetProperty("actual_share_volume").GetInt32());
        Assert.Equal(muted, actual.GetProperty("actual_share_muted").GetBoolean());
        Assert.Equal(id, actual.GetProperty("actual_share_id").GetGuid());
    }

    [Theory]
    [InlineData("none")]
    [InlineData("missing-volume")]
    [InlineData("missing-mute")]
    [InlineData("string-volume")]
    [InlineData("fraction-volume")]
    [InlineData("negative-volume")]
    [InlineData("large-volume")]
    [InlineData("string-mute")]
    [InlineData("silent-unmuted")]
    public void InvalidOrMissingReceiptClearsOldAudioButDoesNotInventOrLoseVideo(string kind)
    {
        var (projection, target, id) = Bind();
        Observe(projection, target, State(id, true, 80, false), 1);
        var state = State(id, kind != "silent-unmuted", 29, false);
        if (kind is "none" or "missing-volume") state.Remove("actual_share_volume");
        if (kind is "none" or "missing-mute") state.Remove("actual_share_muted");
        if (kind == "string-volume") state["actual_share_volume"] = "29";
        if (kind == "fraction-volume") state["actual_share_volume"] = 29.5;
        if (kind == "negative-volume") state["actual_share_volume"] = -1;
        if (kind == "large-volume") state["actual_share_volume"] = 101;
        if (kind == "string-mute") state["actual_share_muted"] = "false";
        Observe(projection, target, state, 2);
        var actual = Snapshot(projection);
        Assert.Equal(JsonValueKind.Null, actual.GetProperty("actual_share_volume").ValueKind);
        Assert.Equal(JsonValueKind.Null, actual.GetProperty("actual_share_muted").ValueKind);
        Assert.Equal(id, actual.GetProperty("actual_share_id").GetGuid());
        Assert.Equal(10L, actual.GetProperty("actual_frames").GetInt64());
    }

    [Fact]
    public void OldCallbackCannotRestoreAudioAndOtherTargetKeepsItsOwnReceipt()
    {
        var (projection, target, id) = Bind();
        var other = target with { WindowId = 2, WorkerInstanceId = Guid.NewGuid() };
        var otherId = Guid.NewGuid();
        projection.Bind(other, otherId, 3);
        Observe(projection, other, State(otherId, true, 35, true), 1);
        Observe(projection, target, State(id, true, 29, false), 1);
        Observe(projection, target, State(id, true, null, null), 3);
        Observe(projection, target, State(id, true, 80, false), 2);
        Assert.Equal(JsonValueKind.Null, Snapshot(projection).GetProperty("actual_share_volume").ValueKind);
        Assert.Equal(35, Snapshot(projection, 2).GetProperty("actual_share_volume").GetInt32());
        Assert.True(Snapshot(projection, 2).GetProperty("actual_share_muted").GetBoolean());
        projection.Observe(1, target.WorkerInstanceId, target.OwnerEpoch, 4,
            JsonSerializer.SerializeToElement(new { source_id = 42L, adapter_kind = "image" }), Now.AddSeconds(4));
        Assert.Equal(JsonValueKind.Null, Snapshot(projection).GetProperty("actual_share_muted").ValueKind);
    }

    /// <summary>:returns: 独立两目标内存模型和不可变分享身份。</summary>
    private static (ScreenSharePlaybackProjection Projection, ScreenShareTargetIdentity Target, Guid Id) Bind()
    {
        var target = new ScreenShareTargetIdentity(1, Guid.NewGuid(), 7, 19);
        var id = Guid.NewGuid();
        var projection = new ScreenSharePlaybackProjection();
        projection.Bind(target, id, 3);
        return (projection, target, id);
    }

    /// <summary>:returns: 严格实际报告同形字段，null是未知而非默认音量。</summary>
    private static Dictionary<string, object?> State(Guid id, bool audio, int? volume, bool? muted) => new()
    {
        ["source_id"] = null, ["adapter_kind"] = "screen_share", ["share_id"] = id,
        ["actual_frames"] = 10L, ["actual_audio"] = audio,
        ["actual_share_volume"] = volume, ["actual_share_muted"] = muted,
    };

    /// <summary>:param version: 已提交单调版本。:returns: 观察纯值；不会排命令。</summary>
    private static void Observe(ScreenSharePlaybackProjection projection, ScreenShareTargetIdentity target, object state, int version) =>
        projection.Observe(target.WindowId, target.WorkerInstanceId, target.OwnerEpoch, 3, JsonSerializer.SerializeToElement(state), Now.AddSeconds(version));

    /// <summary>:returns: 正式DTO序列化，校验HTTP/SSE字段名称而非私有记录。</summary>
    private static JsonElement Snapshot(ScreenSharePlaybackProjection projection, int window = 1) =>
        JsonSerializer.SerializeToElement(projection.Project(new PlaybackSessionDto
        {
            WindowId = window, SourceId = null, ActualSourceId = null, ActualAdapterKind = "screen_share",
            DesiredGeneration = 3, ObservedGeneration = 3,
        }));
}
