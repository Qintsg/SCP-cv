// 屏幕共享内存授权、窗口占用和代次安全回归；不启动实体播放器。SCP-cv 2026-10-10。
using System.Text.Json;
using ScpCv.ControlHost.ScreenSharing;
using ScpCv.Infrastructure.Playback;

namespace ScpCv.ControlHost.Tests;

public sealed class ScreenShareRegistryTests
{
    [Fact]
    public void EachWindowHasOneReservationAndTwoWindowsAreIndependent()
    {
        var registry = new ScreenShareRegistry(new TestClock());
        var first = registry.Create("alice", Target(1), false);
        var second = registry.Create("bob", Target(2), true);
        Assert.NotEqual(first.Id, second.Id);
        Assert.Equal(2, registry.ActiveCount);
        Assert.Equal("share_window_busy", Assert.Throws<ScreenShareException>(
            () => registry.Create("bob", Target(1), false)).Code);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    [InlineData(-1)]
    public void InvalidTargetFailsBeforeReservation(int window)
    {
        var registry = new ScreenShareRegistry(new TestClock());
        Assert.Equal(400, Assert.Throws<ScreenShareException>(() => registry.Create("alice", Target(window), false)).StatusCode);
        Assert.Equal(0, registry.ActiveCount);
    }

    [Fact]
    public void OtherOwnerCannotReadNegotiateRenewOrStop()
    {
        var registry = new ScreenShareRegistry(new TestClock());
        var share = registry.Create("alice", Target(1), false);
        Assert.Equal(404, Assert.Throws<ScreenShareException>(() => registry.GetOwned("bob", share.Id)).StatusCode);
        Assert.Throws<ScreenShareException>(() => registry.SetOffer("bob", share.Id, "offer", "v=0\r\n"));
        Assert.Throws<ScreenShareException>(() => registry.Heartbeat("bob", share.Id));
        Assert.Throws<ScreenShareException>(() => registry.BeginStop("bob", share.Id, "publisher_stop"));
        Assert.Equal(ScreenShareState.Allocated, registry.GetOwned("alice", share.Id).State);
    }

    [Fact]
    public void CaptureReservationHasNoReceiverAuthorityAndDtoNeverContainsToken()
    {
        var registry = new ScreenShareRegistry(new TestClock());
        var share = registry.Create("alice", Target(1), false);
        Assert.Null(share.SourceGeneration);
        Assert.Throws<ScreenShareException>(() => registry.GetReceiverGrant(share.Id, Target(1), 7));
        var grant = Negotiate(registry, share.Id, 7);
        Assert.Equal(64, grant.Token.Length);
        Assert.DoesNotContain(grant.Token, JsonSerializer.Serialize(registry.GetOwned("alice", share.Id)), StringComparison.Ordinal);
        Assert.DoesNotContain(grant.Token, grant.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void ReceiverChecksTokenWindowAndGeneration()
    {
        var registry = new ScreenShareRegistry(new TestClock());
        var share = registry.Create("alice", Target(1), false);
        var grant = Negotiate(registry, share.Id, 7);
        Assert.Equal("v=0\r\n", registry.ReadOffer(share.Id, 1, 7, grant.Token).Sdp);
        Assert.Throws<ScreenShareException>(() => registry.ReadOffer(share.Id, 1, 7, new string('0', 64)));
        Assert.Throws<ScreenShareException>(() => registry.ReadOffer(share.Id, 2, 7, grant.Token));
        Assert.Throws<ScreenShareException>(() => registry.ReadOffer(share.Id, 1, 8, grant.Token));
        Assert.Throws<ScreenShareException>(() => registry.ReadOffer(Guid.NewGuid(), 1, 7, grant.Token));
        Assert.Throws<ScreenShareException>(() => registry.BindReceiver("alice", share.Id, 8));
    }

    [Theory]
    [InlineData("answer", "v=0\r\n", "share_sdp_invalid")]
    [InlineData("offer", "", "share_sdp_invalid")]
    [InlineData("offer", "nonsense", "share_sdp_invalid")]
    public void InvalidOfferIsRejectedBeforeChangingState(string type, string sdp, string code)
    {
        var registry = new ScreenShareRegistry(new TestClock());
        var share = registry.Create("alice", Target(1), false);
        Assert.Equal(code, Assert.Throws<ScreenShareException>(() => registry.SetOffer("alice", share.Id, type, sdp)).Code);
        Assert.Equal(ScreenShareState.Allocated, registry.GetOwned("alice", share.Id).State);
    }

    [Fact]
    public void Utf8SdpLimitAndSingleNegotiationAreEnforced()
    {
        var registry = new ScreenShareRegistry(new TestClock());
        var share = registry.Create("alice", Target(1), false);
        Assert.Equal(413, Assert.Throws<ScreenShareException>(() => registry.SetOffer("alice", share.Id, "offer", "v=0\r\n" + new string('中', 22000))).StatusCode);
        var grant = Negotiate(registry, share.Id, 7);
        Assert.Throws<ScreenShareException>(() => registry.SetOffer("alice", share.Id, "offer", "v=0\r\n"));
        Assert.Equal(413, Assert.Throws<ScreenShareException>(() => registry.SetAnswer(share.Id, 1, 7, grant.Token, "answer", "v=0\r\n" + new string('a', 65536))).StatusCode);
        registry.SetAnswer(share.Id, 1, 7, grant.Token, "answer", "v=0\r\n");
        Assert.Equal("v=0\r\n", registry.GetOwned("alice", share.Id).Answer?.Sdp);
        Assert.Throws<ScreenShareException>(() => registry.SetAnswer(share.Id, 1, 7, grant.Token, "answer", "v=0\r\n"));
    }

    [Fact]
    public void ConnectionAndPublisherHeartbeatAreNotFrameEvidence()
    {
        var registry = new ScreenShareRegistry(new TestClock());
        var share = registry.Create("alice", Target(1), false);
        var grant = Negotiate(registry, share.Id, 7);
        registry.SetAnswer(share.Id, 1, 7, grant.Token, "answer", "v=0\r\n");
        registry.Heartbeat("alice", share.Id);
        Assert.Equal(ScreenShareState.Negotiating, registry.GetOwned("alice", share.Id).State);
        Assert.Throws<ScreenShareException>(() => registry.ReportFrame(share.Id, 1, 7, grant.Token, 0, false));
        registry.ReportFrame(share.Id, 1, 7, grant.Token, 1, false);
        var receiving = registry.GetOwned("alice", share.Id);
        Assert.Equal(ScreenShareState.Receiving, receiving.State);
        Assert.Equal(1, receiving.ActualFrames);
        Assert.False(receiving.ReceiverHasAudio);
        Assert.NotNull(receiving.LastFrameAt);
    }

    [Fact]
    public void ReceiverQualityRequiresMatchingGrantAndIsClearedOnStop()
    {
        var registry = new ScreenShareRegistry(new TestClock());
        var share = registry.Create("alice", Target(1), false);
        var grant = Negotiate(registry, share.Id, 7);
        registry.SetAnswer(share.Id, 1, 7, grant.Token, "answer", "v=0\r\n");
        var quality = new ReceiverQuality(1920, 1080, 30, 7.8);
        Assert.Throws<ScreenShareException>(() => registry.ReportFrame(share.Id, 1, 8, grant.Token, 1, false, quality));
        Assert.Null(registry.GetOwned("alice", share.Id).ReceiverQuality);
        registry.ReportFrame(share.Id, 1, 7, grant.Token, 1, false, quality);
        Assert.Equal(quality, registry.GetOwned("alice", share.Id).ReceiverQuality);
        registry.BeginStop("alice", share.Id, "publisher_stop");
        Assert.Null(registry.GetOwned("alice", share.Id).ReceiverQuality);
        Assert.Throws<ScreenShareException>(() => registry.ReportFrame(share.Id, 1, 7, grant.Token, 2, false, quality));
    }

    [Fact]
    public void AllocatedDeadlineCannotBeExtendedByHeartbeatAndExpiredGrantIsRevoked()
    {
        var clock = new TestClock();
        var registry = new ScreenShareRegistry(clock);
        var share = registry.Create("alice", Target(1), false);
        var grant = Negotiate(registry, share.Id, 7);
        clock.Advance(TimeSpan.FromSeconds(19));
        registry.Heartbeat("alice", share.Id);
        clock.Advance(TimeSpan.FromSeconds(1));
        Assert.Throws<ScreenShareException>(() => registry.ReadOffer(share.Id, 1, 7, grant.Token));
        var pending = Assert.Single(registry.GetPendingStops());
        Assert.Equal(share.Id, pending.Id);
        Assert.Equal(ScreenShareState.Stopping, pending.State);
        Assert.Throws<ScreenShareException>(() => registry.Create("bob", Target(1), false));
    }

    [Fact]
    public void ActiveLeaseExpiresWithoutPublisherEvenIfReceiverKeepsReportingFrames()
    {
        var clock = new TestClock();
        var registry = new ScreenShareRegistry(clock);
        var share = registry.Create("alice", Target(1), false);
        var grant = Negotiate(registry, share.Id, 7);
        registry.SetAnswer(share.Id, 1, 7, grant.Token, "answer", "v=0\r\n");
        registry.ReportFrame(share.Id, 1, 7, grant.Token, 1, false);
        clock.Advance(TimeSpan.FromSeconds(29));
        registry.ReportFrame(share.Id, 1, 7, grant.Token, 2, false);
        clock.Advance(TimeSpan.FromSeconds(1));
        Assert.Throws<ScreenShareException>(() => registry.ReportFrame(share.Id, 1, 7, grant.Token, 3, false));
        Assert.Equal(ScreenShareState.Stopping, Assert.Single(registry.GetPendingStops()).State);
    }

    [Fact]
    public void StopRevokesImmediatelyButKeepsSlotUntilActualConfirmation()
    {
        var registry = new ScreenShareRegistry(new TestClock());
        var share = registry.Create("alice", Target(1), false);
        var grant = Negotiate(registry, share.Id, 7);
        Assert.Equal(ScreenShareState.Stopping, registry.BeginStop("alice", share.Id, "publisher_stop").State);
        Assert.Throws<ScreenShareException>(() => registry.ReadOffer(share.Id, 1, 7, grant.Token));
        Assert.Equal(ScreenShareState.Stopping, registry.BeginStop("alice", share.Id, "publisher_stop").State);
        Assert.False(registry.ConfirmClosed(share.Id, 1, 8));
        Assert.Throws<ScreenShareException>(() => registry.Create("bob", Target(1), false));
        Assert.True(registry.ConfirmClosed(share.Id, 1, 7));
        var next = registry.Create("bob", Target(1), false);
        Assert.True(registry.ConfirmClosed(share.Id, 1, 7));
        Assert.Equal(ScreenShareState.Allocated, registry.GetOwned("bob", next.Id).State);
    }

    [Fact]
    public void UnopenedStopAndExpiredReservationNeedNoPlayerClose()
    {
        var clock = new TestClock();
        var registry = new ScreenShareRegistry(clock);
        var first = registry.Create("alice", Target(1), false);
        Assert.Equal(ScreenShareState.Closed, registry.BeginStop("alice", first.Id, "cancelled").State);
        registry.Create("bob", Target(1), false);
        clock.Advance(TimeSpan.FromSeconds(20));
        Assert.Empty(registry.GetPendingStops());
        Assert.Equal(0, registry.ActiveCount);
    }

    [Fact]
    public void TerminalHistoryIsBounded()
    {
        var registry = new ScreenShareRegistry(new TestClock());
        for (var index = 0; index < 100; index++)
        {
            var share = registry.Create("alice", Target(1), false);
            registry.BeginStop("alice", share.Id, "cancelled");
        }
        Assert.InRange(registry.RetainedCount, 1, 32);
        Assert.Equal(0, registry.ActiveCount);
    }

    private static ScreenShareReceiverGrant Negotiate(ScreenShareRegistry registry, Guid id, long generation)
    {
        registry.SetOffer("alice", id, "offer", "v=0\r\n");
        registry.BindReceiver("alice", id, generation);
        return registry.GetReceiverGrant(id, Target(1), generation);
    }

    /// <summary>固定原Worker身份，使重启围栏可以与窗口/源代次分别验证。</summary>
    private static ScreenShareTargetIdentity Target(int window) => new(window, new Guid("01000000-0000-0000-0000-000000000001"), 7, 19);

    [Fact]
    public void NewCurrentWorkerCannotRetrieveOriginalWorkersCapability()
    {
        var registry = new ScreenShareRegistry(new TestClock());
        var share = registry.Create("alice", Target(1), false);
        var grant = Negotiate(registry, share.Id, 7);
        Assert.Throws<ScreenShareException>(() => registry.GetReceiverGrant(share.Id, Target(1) with { WorkerInstanceId = Guid.NewGuid() }, 7));
        Assert.Throws<ScreenShareException>(() => registry.GetReceiverGrant(share.Id, Target(1) with { OwnerEpoch = 8 }, 7));
        Assert.Throws<ScreenShareException>(() => registry.GetReceiverGrant(share.Id, Target(1) with { GroupEpoch = 20 }, 7));
        Assert.Equal(grant.Token, registry.GetReceiverGrant(share.Id, Target(1), 7).Token);
    }

    private sealed class TestClock : TimeProvider
    {
        private DateTimeOffset _now = new(2026, 10, 10, 0, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan delta) => _now += delta;
    }
}
