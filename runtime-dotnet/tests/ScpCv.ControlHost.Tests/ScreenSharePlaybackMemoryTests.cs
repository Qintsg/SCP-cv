// 非秘密共享投影的重启、乱序、回滚和两窗口隔离；纯值测试不代表真实出画。SCP-cv 2026-10-11。
using System.Text.Json;
using ScpCv.Contracts.Http;
using ScpCv.Infrastructure.Playback;

namespace ScpCv.ControlHost.Tests;

public sealed class ScreenSharePlaybackMemoryTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 11, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ConfirmedReleaseCannotBeRevivedByLateOriginalReportOrClearLaterShare()
    {
        var (projection, target, id) = Bind();
        Observe(projection, target, id, 30, 1);
        projection.Release(target, id, 3);
        Observe(projection, target, id, 900, 2);
        Assert.Null(projection.Project(Session()).ActualShareId);
        Assert.Equal(0, projection.Project(Session()).ActualFrames);
        var replacement = Guid.NewGuid();
        projection.Bind(target, replacement, 3);
        Observe(projection, target, replacement, 60, 3);
        projection.Release(target, id, 3);
        Assert.Equal(replacement, projection.Project(Session()).ActualShareId);
        Assert.Equal(60, projection.Project(Session()).ActualFrames);
    }

    [Theory]
    [InlineData("window")]
    [InlineData("instance")]
    [InlineData("owner")]
    [InlineData("group")]
    [InlineData("share")]
    [InlineData("generation")]
    public void MismatchedReleaseDoesNotClearCurrentActualIdentity(string mismatch)
    {
        var (projection, target, id) = Bind();
        Observe(projection, target, id, 30, 1);
        var wrong = mismatch switch
        {
            "window" => target with { WindowId = 2 },
            "instance" => target with { WorkerInstanceId = Guid.NewGuid() },
            "owner" => target with { OwnerEpoch = 8 },
            "group" => target with { GroupEpoch = 12 },
            _ => target,
        };
        projection.Release(wrong, mismatch == "share" ? Guid.NewGuid() : id, mismatch == "generation" ? 4 : 3);
        Assert.Equal(id, projection.Project(Session()).ActualShareId);
        Assert.Equal(30, projection.Project(Session()).ActualFrames);
    }

    [Fact]
    public void ConfirmedReleaseIsIdempotentAndDoesNotChangeOtherTarget()
    {
        var (projection, left, leftId) = Bind();
        var right = left with { WindowId = 2, WorkerInstanceId = Guid.NewGuid() };
        var rightId = Guid.NewGuid();
        projection.Bind(right, rightId, 3);
        Observe(projection, left, leftId, 30, 1);
        Observe(projection, right, rightId, 50, 1);
        var rightActual = projection.Project(Session() with { WindowId = 2 });
        projection.Release(left, leftId, 3);
        projection.Release(left, leftId, 3);
        Assert.Null(projection.Project(Session()).ActualShareId);
        Assert.Equal(rightActual, projection.Project(Session() with { WindowId = 2 }));
    }

    [Fact]
    public void CommittedNewerCloseCannotBeReversedByOlderPostCommitCallback()
    {
        var (projection, target, id) = Bind();
        Observe(projection, target, id, 30, 1);
        Assert.Equal(id, projection.Project(Session()).ActualShareId);
        projection.Observe(1, target.WorkerInstanceId, target.OwnerEpoch, 4,
            JsonSerializer.SerializeToElement(new { source_id = (long?)null, adapter_kind = "" }), Now.AddSeconds(3));
        Observe(projection, target, id, 900, 2);
        Assert.Null(projection.Project(Session()).ActualShareId);
        Assert.Equal(0, projection.Project(Session()).ActualFrames);
    }

    [Fact]
    public void FramesDoNotRegressAndAudioComesFromLatestValidObservation()
    {
        var (projection, target, id) = Bind();
        Observe(projection, target, id, 30, 1);
        Observe(projection, target, id, 20, 2, true);
        var actual = projection.Project(Session());
        Assert.Equal(30, actual.ActualFrames);
        Assert.True(actual.ActualAudio);
        Observe(projection, target, id, 900, 1, false);
        Assert.Equal(actual, projection.Project(Session()));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ReplacementIdentityCannotReusePreviousActualObservation(bool replaceInstance)
    {
        var (projection, target, id) = Bind();
        Observe(projection, target, id, 30, 1);
        var replacement = replaceInstance ? target with { WorkerInstanceId = Guid.NewGuid() }
            : target with { OwnerEpoch = target.OwnerEpoch + 1 };
        Observe(projection, replacement, id, 900, 2);
        var actual = projection.Project(Session());
        Assert.Null(actual.ActualShareId);
        Assert.Equal(0, actual.ActualFrames);
        Assert.Null(actual.ActualAudio);
    }

    [Fact]
    public void RolledBackDesiredGenerationCannotPublishUncommittedSharingIdentity()
    {
        var (projection, _, _) = Bind();
        var uncommitted = projection.Project(Session() with { DesiredGeneration = 2, ObservedGeneration = 2 });
        Assert.Null(uncommitted.ShareId);
        Assert.Equal("", uncommitted.SourceType);
        Assert.Null(uncommitted.ActualShareId);
    }

    [Fact]
    public void ControlHostRestartDoesNotRecreateTransientIdentityFromPersistedNullSource()
    {
        var actual = new ScreenSharePlaybackProjection().Project(Session());
        Assert.Null(actual.ShareId);
        Assert.Null(actual.ActualShareId);
        Assert.Equal(0, actual.ActualFrames);
        Assert.Null(actual.ActualAudio);
        Assert.Equal("", actual.SourceType);
    }

    [Fact]
    public void TwoTargetsKeepIndependentIdentityAndClearingOneDoesNotChangeOther()
    {
        var (projection, left, leftId) = Bind();
        var right = left with { WindowId = 2, WorkerInstanceId = Guid.NewGuid() };
        var rightId = Guid.NewGuid();
        projection.Bind(right, rightId, 3);
        Observe(projection, left, leftId, 30, 1);
        Observe(projection, right, rightId, 50, 1);
        var rightActual = projection.Project(Session() with { WindowId = 2 });
        Assert.Equal(rightId, rightActual.ActualShareId);
        Assert.Equal(50, rightActual.ActualFrames);
        projection.Observe(1, left.WorkerInstanceId, left.OwnerEpoch, 4,
            JsonSerializer.SerializeToElement(new { source_id = 42L, adapter_kind = "image" }), Now.AddSeconds(2));
        Assert.Null(projection.Project(Session()).ActualShareId);
        Assert.Equal(rightActual, projection.Project(Session() with { WindowId = 2 }));
    }

    /// <summary>:returns: 独立内存投影及明确原Worker身份，不依赖全局运行组。</summary>
    private static (ScreenSharePlaybackProjection Projection, ScreenShareTargetIdentity Target, Guid Id) Bind()
    {
        var target = new ScreenShareTargetIdentity(1, Guid.NewGuid(), 7, 11);
        var id = Guid.NewGuid();
        var projection = new ScreenSharePlaybackProjection();
        projection.Bind(target, id, 3);
        return (projection, target, id);
    }

    /// <summary>:returns: 数据库同形的无永久媒体库ID基线。</summary>
    private static PlaybackSessionDto Session() => new()
    {
        WindowId = 1, SourceId = null, DesiredGeneration = 3, ObservedGeneration = 3,
        ActualSourceId = null, ActualAdapterKind = "screen_share", PlaybackState = "playing",
    };

    /// <summary>:param frames: 已通过Worker报告门禁的纯值帧计数。:returns: 对原目标观察一次。</summary>
    private static void Observe(ScreenSharePlaybackProjection projection, ScreenShareTargetIdentity target,
        Guid id, long frames, int version, bool audio = false) => projection.Observe(target.WindowId,
        target.WorkerInstanceId, target.OwnerEpoch, 3, JsonSerializer.SerializeToElement(new
        { source_id = (long?)null, adapter_kind = "screen_share", share_id = id, actual_frames = frames, actual_audio = audio }), Now.AddSeconds(version));
}
