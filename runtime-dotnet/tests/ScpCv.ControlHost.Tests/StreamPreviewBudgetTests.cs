// 预览预算与质量降档回归：不改系统QoS，不访问网卡或设备。
using System.Diagnostics;
using ScpCv.ControlHost.StreamPreview;

namespace ScpCv.ControlHost.Tests;

public sealed class StreamPreviewBudgetTests
{
    [Fact]
    public void FormalPlaybackAndHostPressureReduceOnlyPreviewAndRecoverWithHysteresis()
    {
        var clock = new BudgetClock(); using var budget = new PreviewBandwidth(new(), clock);
        Assert.Equal(1200, budget.SelectEncoding(true).KilobitsPerSecond);
        budget.UpdateDemand(1, true, false); Assert.Equal(2000, budget.CurrentKilobitsPerSecond);
        Assert.Equal(300, budget.SelectEncoding(true).KilobitsPerSecond);
        budget.UpdateDemand(1, false, false); Assert.Equal(2000, budget.CurrentKilobitsPerSecond);
        clock.Advance(TimeSpan.FromSeconds(31)); Assert.Equal(8000, budget.CurrentKilobitsPerSecond);
        budget.UpdateDemand(1, false, true); Assert.Equal(2000, budget.CurrentKilobitsPerSecond);
    }

    [Fact]
    public void DuplicateViewersShareEncodingButTheirOutgoingTrafficStillCounts()
    {
        using var budget = new PreviewBandwidth(new(), new BudgetClock());
        budget.UpdateDemand(20, false, false); Assert.Equal(300, budget.SelectEncoding(true).KilobitsPerSecond);
        budget.UpdateDemand(128, false, false); Assert.Equal(96, budget.SelectEncoding(true).KilobitsPerSecond);
        budget.ObserveWrite(TimeSpan.FromSeconds(1)); Assert.Equal(2000, budget.CurrentKilobitsPerSecond);
    }

    [Fact]
    public async Task GlobalBucketEnforcesBoundAndCancelledWaitDoesNotReserveFutureCapacityAsync()
    {
        using var budget = new PreviewBandwidth(new() { EgressKilobitsPerSecond = 8000 }, TimeProvider.System);
        await budget.WaitAsync(PreviewBandwidth.MaximumChunkBytes, CancellationToken.None);
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => budget.WaitAsync(PreviewBandwidth.MaximumChunkBytes, cancelled.Token));
        var watch = Stopwatch.StartNew();
        for (var index = 0; index < 8; index++) await budget.WaitAsync(PreviewBandwidth.MaximumChunkBytes, CancellationToken.None);
        Assert.True(watch.Elapsed >= TimeSpan.FromMilliseconds(90), "除16KiB突发外不能一次写出全部预览数据");
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => budget.WaitAsync(PreviewBandwidth.MaximumChunkBytes + 1, CancellationToken.None));
    }

    private sealed class BudgetClock : TimeProvider
    {
        private DateTimeOffset _now = DateTimeOffset.Parse("2026-10-20T00:00:00Z", global::System.Globalization.CultureInfo.InvariantCulture);
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan elapsed) => _now += elapsed;
    }
}
