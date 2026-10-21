// 输入发现的缓存/单飞/取消与受限并发；所有探测均为假实现。
using ScpCv.Infrastructure.Devices;

namespace ScpCv.Infrastructure.Tests;

public sealed class InputDiscoveryTests
{
    [Fact]
    public async Task SimulationDoesNotPretendReachability()
    {
        await using var discovery = new InputDiscovery(new SimulationInputReachabilityProbe(), new(), TimeProvider.System);
        var result = await discovery.RefreshAsync(false);
        Assert.Equal(31, result.Count);
        Assert.All(result.Values, item => { Assert.Equal("unknown", item.Status); Assert.False(item.Available); });
    }

    [Fact]
    public async Task ConcurrentAndCachedReadersShareOneBoundedProbePass()
    {
        var probe = new ControlledProbe();
        await using var discovery = new InputDiscovery(probe, new() { MaximumConcurrentInputs = 3 }, TimeProvider.System);
        var first = discovery.RefreshAsync(false);
        var second = discovery.RefreshAsync(true);
        probe.Release.TrySetResult();
        await Task.WhenAll(first, second);
        var cached = await discovery.RefreshAsync(false);
        Assert.Equal(31, probe.Calls);
        Assert.InRange(probe.Maximum, 1, 3);
        Assert.Equal(31, cached.Count);
        Assert.DoesNotContain("192.168.5.75", probe.Addresses);
    }

    [Fact]
    public async Task CancellingOneWaiterDoesNotCancelTheSharedPass()
    {
        var probe = new ControlledProbe();
        await using var discovery = new InputDiscovery(probe, new(), TimeProvider.System);
        using var cancel = new CancellationTokenSource();
        var abandoned = discovery.RefreshAsync(false, cancel.Token);
        var retained = discovery.RefreshAsync(false);
        await cancel.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => abandoned);
        probe.Release.TrySetResult();
        Assert.Equal(31, (await retained).Count);
    }

    private sealed class ControlledProbe : IInputReachabilityProbe
    {
        internal readonly TaskCompletionSource Release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal readonly System.Collections.Concurrent.ConcurrentBag<string> Addresses = [];
        internal int Calls, Maximum;
        private int _active;

        public async Task<InputReachability> ProbeAsync(SiteInputDefinition input, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref Calls); Addresses.Add(input.Address);
            var active = Interlocked.Increment(ref _active);
            int observed;
            do { observed = Maximum; if (observed >= active) break; } while (Interlocked.CompareExchange(ref Maximum, active, observed) != observed);
            try { await Release.Task.WaitAsync(cancellationToken); return new(true, false, "reachable", DateTimeOffset.UtcNow); }
            finally { Interlocked.Decrement(ref _active); }
        }
    }
}
