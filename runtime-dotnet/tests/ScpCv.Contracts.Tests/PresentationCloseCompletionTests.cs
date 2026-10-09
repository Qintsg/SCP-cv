// 纯关闭回归：不启动Office或D4，重复关闭不得把已失败的退出变成成功。
using ScpCv.Contracts.Presentations;

namespace ScpCv.Contracts.Tests;

public sealed class PresentationCloseCompletionTests
{
    /// <summary>:returns: 重复调用仍报告第一次真实退出失败，不再次执行外部关闭。</summary>
    [Fact]
    public async Task RepeatedClosePreservesFirstFailureWithoutReplayingShutdown()
    {
        var completion = new PresentationCloseCompletion();
        var failure = new PresentationTransportException("退出未确认", new IOException("受控失败"));
        var shutdowns = 0;
        Task Close()
        {
            shutdowns++;
            return Task.FromException(failure);
        }

        var first = await Assert.ThrowsAsync<PresentationTransportException>(() => completion.RunAsync(Close).AsTask());
        var repeated = await Assert.ThrowsAsync<PresentationTransportException>(() => completion.RunAsync(Close).AsTask());

        Assert.Same(failure, first);
        Assert.Same(first, repeated);
        Assert.Equal(1, shutdowns);
    }

    /// <summary>:returns: 第二个关闭调用仍等真实退出；成功后再次等待不重复执行关闭。</summary>
    [Fact]
    public async Task ConcurrentAndRepeatedCloseWaitForSameSuccessfulCompletion()
    {
        var completion = new PresentationCloseCompletion();
        var exited = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var shutdowns = 0;
        Task Close()
        {
            shutdowns++;
            return exited.Task;
        }

        var first = completion.RunAsync(Close).AsTask();
        var concurrent = completion.RunAsync(Close).AsTask();
        try
        {
            Assert.False(first.IsCompleted);
            Assert.False(concurrent.IsCompleted);
            Assert.Same(first, concurrent);
            Assert.Equal(1, shutdowns);
        }
        finally { exited.TrySetResult(); }

        await Task.WhenAll(first, concurrent);
        await completion.RunAsync(Close);
        Assert.Equal(1, shutdowns);
    }

    /// <summary>:returns: 关闭同步前段抛错也只执行一次，随后不会被重放或改成成功。</summary>
    [Fact]
    public async Task SynchronousCloseFailureIsRememberedWithoutReplayingShutdown()
    {
        var completion = new PresentationCloseCompletion();
        var failure = new InvalidOperationException("关闭入口同步失败");
        var shutdowns = 0;
        Task Close()
        {
            shutdowns++;
            throw failure;
        }

        var first = await Assert.ThrowsAsync<InvalidOperationException>(() => completion.RunAsync(Close).AsTask());
        var repeated = await Assert.ThrowsAsync<InvalidOperationException>(() => completion.RunAsync(Close).AsTask());

        Assert.Same(failure, first);
        Assert.Same(first, repeated);
        Assert.Equal(1, shutdowns);
    }

    /// <summary>:returns: 真正多线程竞争不会启动多个外部关闭，全部调用等待同一终态。</summary>
    [Fact]
    public async Task ParallelCallersCannotExecuteMultipleShutdowns()
    {
        var completion = new PresentationCloseCompletion();
        var exited = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var shutdowns = 0;
        var calls = new Task[16];
        Task Close()
        {
            Interlocked.Increment(ref shutdowns);
            return exited.Task;
        }

        Parallel.For(0, calls.Length, index => calls[index] = completion.RunAsync(Close).AsTask());
        try
        {
            Assert.Equal(1, shutdowns);
            Assert.All(calls, call => Assert.False(call.IsCompleted));
            Assert.All(calls, call => Assert.Same(calls[0], call));
        }
        finally { exited.TrySetResult(); }
        await Task.WhenAll(calls);
        Assert.Equal(1, shutdowns);
    }
}
