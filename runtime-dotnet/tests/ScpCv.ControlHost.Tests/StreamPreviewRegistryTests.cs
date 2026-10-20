// 同流65并发、租约归属与慢消费者隔离；仅假生产者，不启动原生或访问网络。
using System.Collections.Concurrent;
using ScpCv.ControlHost.StreamPreview;

namespace ScpCv.ControlHost.Tests;

public sealed class StreamPreviewRegistryTests
{
    [Fact]
    public async Task SixtyFiveConcurrentReadersShareOneProducerAndLastReleaseStopsItAsync()
    {
        var clock = new RegistryClock(); using var budget = new PreviewBandwidth(new(), clock);
        var producer = new ControlledProducer(); await using var registry = new StreamPreviewRegistry(new(), producer, budget, clock);
        var leases = new ConcurrentBag<PreviewLeaseSnapshot>();
        Parallel.For(0, 65, _ => leases.Add(registry.Create("alice-login1", "https://example.test", new("rtmp://192.0.2.1/live/one", "low"))));
        Assert.Equal(0, producer.Starts); Assert.Equal(0, registry.FeedCount);
        var readers = new ConcurrentBag<PreviewSubscription>();
        Parallel.ForEach(leases, lease => readers.Add(registry.Open("alice-login1", "https://example.test", lease.Id)));
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        foreach (var reader in readers) await reader.Reader.ReadAsync(timeout.Token);
        Assert.Equal(1, producer.Starts); Assert.Equal(1, registry.FeedCount);
        foreach (var reader in readers) reader.Dispose();
        Assert.Equal(0, registry.LeaseCount); clock.Advance(TimeSpan.FromSeconds(4));
        await registry.SweepAsync(); Assert.Equal(0, registry.FeedCount); Assert.Equal(1, producer.Stops);
    }

    [Fact]
    public async Task ForeignOrOldLoginAndDuplicateConsumptionNeverStartAnotherProducerAsync()
    {
        var clock = new RegistryClock(); using var budget = new PreviewBandwidth(new(), clock);
        var producer = new ControlledProducer(); await using var registry = new StreamPreviewRegistry(new(), producer, budget, clock);
        var lease = registry.Create("alice-login1", "https://example.test", new("rtmp://192.0.2.1/live/one", "low"));
        Assert.Throws<PreviewException>(() => registry.Open("alice-login2", "https://example.test", lease.Id));
        Assert.Throws<PreviewException>(() => registry.Open("alice-login1", "https://evil.test", lease.Id));
        Assert.Equal(0, producer.Starts);
        using var subscription = registry.Open("alice-login1", "https://example.test", lease.Id);
        Assert.Throws<PreviewException>(() => registry.Open("alice-login1", "https://example.test", lease.Id));
        clock.Advance(TimeSpan.FromSeconds(31));
        Assert.Throws<PreviewException>(() => registry.Heartbeat("alice-login1", "https://example.test", lease.Id));
        await registry.SweepAsync(); Assert.True(subscription.Lifetime.IsCancellationRequested); Assert.Equal(0, registry.LeaseCount);
    }

    [Fact]
    public async Task SlowReaderOnlyLosesWholeGopsAndDoesNotBlockFastReaderAsync()
    {
        var clock = new RegistryClock(); using var budget = new PreviewBandwidth(new(), clock);
        var producer = new ControlledProducer(); await using var registry = new StreamPreviewRegistry(new(), producer, budget, clock);
        var source = new PreviewSource("rtmp://192.0.2.1/live/one", "high");
        var one = registry.Create("alice", "https://example.test", source); var two = registry.Create("bob", "https://example.test", source);
        using var slow = registry.Open("alice", "https://example.test", one.Id); using var fast = registry.Open("bob", "https://example.test", two.Id);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await slow.Reader.ReadAsync(timeout.Token); await fast.Reader.ReadAsync(timeout.Token);
        for (uint index = 1; index <= 10; index++)
        {
            producer.Publish(source.Key, new([(byte)index], index, 1));
            Assert.Equal(index, (await fast.Reader.ReadAsync(timeout.Token)).Timestamp);
        }
        Assert.Equal(9u, (await slow.Reader.ReadAsync(timeout.Token)).Timestamp);
        Assert.Equal(10u, (await slow.Reader.ReadAsync(timeout.Token)).Timestamp);
        Assert.False(slow.Reader.TryRead(out _)); Assert.Equal(1, producer.Starts);
    }

    [Fact]
    public async Task CapacityAndIdleUnconsumedLeaseDoNotCreateUpstreamAsync()
    {
        var clock = new RegistryClock(); var options = new PreviewOptions { MaximumLeases = 1 };
        using var budget = new PreviewBandwidth(options, clock); var producer = new ControlledProducer();
        await using var registry = new StreamPreviewRegistry(options, producer, budget, clock);
        registry.Create("alice", "https://example.test", new("rtmp://192.0.2.1/live/one", "low"));
        Assert.Throws<PreviewException>(() => registry.Create("bob", "https://example.test", new("rtmp://192.0.2.2/live/one", "low")));
        clock.Advance(TimeSpan.FromSeconds(31)); await registry.SweepAsync();
        Assert.Equal(0, producer.Starts); Assert.Equal(0, registry.LeaseCount); Assert.Equal(0, registry.FeedCount);
    }

    [Fact]
    public async Task DependencyRecoveryCanRestartCompletedProducerWithoutDuplicateLiveInstanceAsync()
    {
        var clock = new RegistryClock(); using var budget = new PreviewBandwidth(new(), clock);
        var producer = new RecoveringProducer(); await using var registry = new StreamPreviewRegistry(new(), producer, budget, clock);
        var source = new PreviewSource("rtmp://192.0.2.1/live/one", "low");
        var first = registry.Create("alice", "https://example.test", source);
        using var old = registry.Open("alice", "https://example.test", first.Id);
        await old.Reader.Completion.WaitAsync(TimeSpan.FromSeconds(5)); old.Dispose(); await Task.Yield();
        var second = registry.Create("alice", "https://example.test", source);
        using var current = registry.Open("alice", "https://example.test", second.Id);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        Assert.Equal(7u, (await current.Reader.ReadAsync(timeout.Token)).Timestamp); Assert.Equal(2, producer.Starts);
    }

    private sealed class RegistryClock : TimeProvider
    {
        private DateTimeOffset _now = new(2026, 10, 20, 0, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan elapsed) => _now += elapsed;
    }

    private sealed class ControlledProducer : IPreviewProducer
    {
        private int _starts, _stops;
        private readonly ConcurrentDictionary<string, Action<PreviewGop>> _writers = new(StringComparer.Ordinal);
        public int Starts => Volatile.Read(ref _starts);
        public int Stops => Volatile.Read(ref _stops);
        public void Publish(string key, PreviewGop gop) => _writers[key](gop);
        public async Task RunAsync(PreviewSource source, Func<PreviewEncoding> encoding, Action<PreviewGop> publish, Action<string> status, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _starts); Assert.True(_writers.TryAdd(source.Key, publish));
            try { publish(new([0], 0, 1)); await Task.Delay(Timeout.Infinite, cancellationToken); }
            finally { _writers.TryRemove(source.Key, out _); Interlocked.Increment(ref _stops); }
        }
    }

    private sealed class RecoveringProducer : IPreviewProducer
    {
        public int Starts { get; private set; }
        public async Task RunAsync(PreviewSource source, Func<PreviewEncoding> encoding, Action<PreviewGop> publish, Action<string> status, CancellationToken cancellationToken)
        {
            if (++Starts == 1) throw new PreviewException("合成依赖缺失", "preview_dependency_missing");
            publish(new([7], 7, 1)); await Task.Delay(Timeout.Infinite, cancellationToken);
        }
    }
}
