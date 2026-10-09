// 正式诊断buffer纯值容量回归；Compile Link同源，不引用UNO或扩大生产协议。
using ScpCv.LibreOfficeHost.Runtime;

namespace ScpCv.Contracts.Tests;

public sealed class PresentationEventBufferTests
{
    [Fact]
    public void FullCapacityPreservesFirstEntriesAndReportsTheDroppedEvent()
    {
        var buffer = new PresentationEventBuffer(capacity: 2);
        buffer.TryRecord("animation_begin", observedWindow: 7);
        buffer.TryRecord("animation_end", observedWindow: 7);
        buffer.TryRecord("transition", observedWindow: 8);

        var batch = buffer.Drain();

        Assert.Equal(2, batch.Entries.Count);
        Assert.Equal(1, batch.Dropped);
        Assert.Equal(["animation_begin", "animation_end"], batch.Entries.Select(entry => entry.Kind));
    }

    [Fact]
    public void DrainingReusesCapacityButDoesNotMutateSnapshotsOrResetSequenceAndWindowLabels()
    {
        var buffer = new PresentationEventBuffer(capacity: 1);
        Assert.True(buffer.TryRecord("animation_begin", observedWindow: 7));
        Assert.False(buffer.TryRecord("dropped", observedWindow: 99));
        var first = buffer.Drain();
        var firstEntry = Assert.Single(first.Entries);
        Assert.Equal(1, firstEntry.Sequence);
        Assert.Equal(7, firstEntry.ObservedWindow);
        Assert.Equal(1, first.Dropped);
        Assert.True(buffer.TryRecord("animation_end", observedWindow: 8));

        var second = buffer.Drain();
        var secondEntry = Assert.Single(second.Entries);
        Assert.Equal(2, secondEntry.Sequence);
        Assert.Equal(8, secondEntry.ObservedWindow);
        Assert.True(secondEntry.Timestamp >= firstEntry.Timestamp);
        Assert.Equal(0, second.Dropped);
        Assert.Equal("animation_begin", Assert.Single(first.Entries).Kind);
        Assert.Equal(1, first.Dropped);
        var empty = buffer.Drain();
        Assert.Empty(empty.Entries);
        Assert.Equal(0, empty.Dropped);
    }

    [Fact]
    public async Task ConcurrentCallbacksAccountForEveryAcceptedOrDroppedAttempt()
    {
        const int workers = 8;
        const int attemptsPerWorker = 500;
        var buffer = new PresentationEventBuffer(capacity: 128);
        var accepted = 0;
        var tasks = Enumerable.Range(1, workers).Select(worker => Task.Run(() =>
        {
            for (var attempt = 0; attempt < attemptsPerWorker; attempt++)
                if (buffer.TryRecord("callback", worker)) Interlocked.Increment(ref accepted);
        })).ToArray();
        await Task.WhenAll(tasks).WaitAsync(TimeSpan.FromSeconds(10));

        var batch = buffer.Drain();

        Assert.InRange(batch.Entries.Count, 1, 128);
        Assert.Equal(accepted, batch.Entries.Count);
        Assert.Equal(workers * attemptsPerWorker, accepted + batch.Dropped);
        Assert.Equal(Enumerable.Range(1, accepted).Select(value => (long)value), batch.Entries.Select(entry => entry.Sequence));
        Assert.All(batch.Entries, entry => Assert.InRange(entry.ObservedWindow, 1, workers));
    }

    [Fact]
    public void CapacityIsRestrictedToTheDeclaredBoundAndDefaultsTo128()
    {
        foreach (var capacity in new[] { -1, 0, 129, int.MaxValue })
            Assert.Throws<ArgumentOutOfRangeException>(() => new PresentationEventBuffer(capacity));
        var buffer = new PresentationEventBuffer();
        for (var index = 0; index < 129; index++) buffer.TryRecord("callback", index);
        var batch = buffer.Drain();
        Assert.Equal(128, batch.Entries.Count);
        Assert.Equal(1, batch.Dropped);
    }
}
