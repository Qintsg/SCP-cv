// 通过公开客户端和真实自有stdio侧车验证排队、期限与通信故障，不启动Office。
using ScpCv.Contracts.Presentations;
using ScpCv.Presentation.Protocol;
using System.Diagnostics;
using System.Globalization;

namespace ScpCv.Contracts.Tests;

public sealed class PresentationProcessClientFaultTests
{
    /// <summary>:returns: 排队请求按自己deadline停止，无dispatch且不破坏仍在途的有效请求。</summary>
    [Fact]
    public async Task ExpiredQueuedRequestStopsWaitingWithoutPoisoningActiveRequest()
    {
        if (!OperatingSystem.IsWindows()) return;
        var scope = Identity();
        await using var fixture = new PresentationTransportFixture("queued", scope);
        var client = fixture.Client;
        var root = fixture.Root;
        var first = client.SendAsync(Request(scope, 8), scope);
        Task<PresentationResponse>? queued = null;
        try
        {
            await WaitForReceiptAsync(root);
            queued = client.SendAsync(Request(scope, 0.3), scope);
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => queued.WaitAsync(TimeSpan.FromSeconds(2)));
            Assert.False(first.IsCompleted, "排队超时不能终结仍有效的在途请求。");
            Assert.Single(File.ReadAllLines(Path.Combine(root, "requests.txt")));
            await File.WriteAllTextAsync(Path.Combine(root, "release"), string.Empty);
            Assert.Equal("succeeded", (await first).Status);
            Assert.Equal("succeeded", (await client.SendAsync(Request(scope, 3), scope)).Status);
            Assert.Equal(2, File.ReadAllLines(Path.Combine(root, "requests.txt")).Length);
        }
        finally
        {
            await File.WriteAllTextAsync(Path.Combine(root, "release"), string.Empty);
            await fixture.ObserveForCleanupAsync(first);
            if (queued is not null) await fixture.ObserveForCleanupAsync(queued);
        }
    }

    /// <summary>:returns: 实际半帧读取取消后，迟到帧不能被另一条请求采纳或引起重放。</summary>
    [Fact]
    public async Task CancelledHalfFramePoisonsTransportAndRejectsLateReplyReuse()
    {
        if (!OperatingSystem.IsWindows()) return;
        var scope = Identity();
        await using var fixture = new PresentationTransportFixture("half-late", scope);
        var client = fixture.Client;
        var root = fixture.Root;
        using var cancellation = new CancellationTokenSource();
        var first = client.SendAsync(Request(scope, 8), scope, cancellation.Token);
        try
        {
            await WaitForFileAsync(Path.Combine(root, "half-sent"));
            cancellation.Cancel();
            var failure = await Assert.ThrowsAsync<PresentationTransportException>(() => first.WaitAsync(TimeSpan.FromSeconds(3)));
            Assert.IsAssignableFrom<OperationCanceledException>(failure.InnerException);
            await File.WriteAllTextAsync(Path.Combine(root, "release"), string.Empty);
            await WaitForFileAsync(Path.Combine(root, "late-sent"));
            await Assert.ThrowsAsync<IOException>(() => client.SendAsync(Request(scope, 3), scope));
            Assert.Single(File.ReadAllLines(Path.Combine(root, "requests.txt")));
        }
        finally
        {
            await File.WriteAllTextAsync(Path.Combine(root, "release"), string.Empty);
            cancellation.Cancel();
            await fixture.ObserveForCleanupAsync(first);
        }
    }

    /// <summary>:param mode: 固定外部故障。:returns: framing或身份失败后，下一条不能被发送。</summary>
    [Theory]
    [InlineData("bad-length")]
    [InlineData("wrong-scope")]
    [InlineData("truncated")]
    public async Task InvalidWireReplyIsUncertainAndCannotBeReused(string mode)
    {
        if (!OperatingSystem.IsWindows()) return;
        var scope = Identity();
        await using var fixture = new PresentationTransportFixture(mode, scope);
        var client = fixture.Client;
        await Assert.ThrowsAsync<PresentationTransportException>(() => client.SendAsync(Request(scope, 3), scope));
        await Assert.ThrowsAsync<IOException>(() => client.SendAsync(Request(scope, 3), scope));
        Assert.Single(File.ReadAllLines(Path.Combine(fixture.Root, "requests.txt")));
    }

    /// <summary>:returns: 关闭取消实际在途读取、并发调用共享结束，精确自有子进程已退出。</summary>
    [Fact]
    public async Task ConcurrentDisposalCancelsInflightReadAndWaitsForOwnedProcessExit()
    {
        if (!OperatingSystem.IsWindows()) return;
        var scope = Identity();
        await using var fixture = new PresentationTransportFixture("silent", scope);
        var client = fixture.Client;
        var first = client.SendAsync(Request(scope, 8), scope);
        try
        {
            await WaitForReceiptAsync(fixture.Root);
            var queued = client.SendAsync(Request(scope, 8), scope);
            var closing = client.DisposeAsync().AsTask();
            var concurrent = client.DisposeAsync().AsTask();
            await Task.WhenAll(closing, concurrent).WaitAsync(TimeSpan.FromSeconds(4));
            await Assert.ThrowsAsync<PresentationTransportException>(() => first);
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => queued);
            Assert.Single(File.ReadAllLines(Path.Combine(fixture.Root, "requests.txt")));
            Assert.True(fixture.HasExited);
            Assert.Equal(0, fixture.ExitCode);
            await client.DisposeAsync();
        }
        finally
        {
            await fixture.ObserveForCleanupAsync(first);
        }
    }

    /// <summary>:returns: 主侧车退出后，正常短寿命自有子成员仍须在既有预算内确认退出。</summary>
    [Fact]
    public async Task DisposalWaitsForOwnedChildWithinExistingShutdownBudget()
    {
        if (!OperatingSystem.IsWindows()) return;
        var scope = Identity();
        await using var fixture = new PresentationTransportFixture("late-child", scope);
        using var release = new EventWaitHandle(false, EventResetMode.ManualReset, "Local\\SCP-cv-ShortChild-" + Path.GetFileName(fixture.Root));
        Assert.Equal("succeeded", (await fixture.Client.SendAsync(Request(scope, 3), scope)).Status);
        using var child = Process.GetProcessById(int.Parse(await File.ReadAllTextAsync(Path.Combine(fixture.Root, "child-pid")), CultureInfo.InvariantCulture));
        _ = child.Handle;
        Assert.False(child.HasExited, "测试须先观察到仍活着的自有短寿命子进程。");
        var closing = fixture.Client.DisposeAsync().AsTask();
        try
        {
            using var observationBudget = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            while (!fixture.HasExited) await Task.Delay(10, observationBudget.Token);
            Assert.Equal(0, fixture.ExitCode);
            Assert.False(child.HasExited, "宿主已退出时，自有子成员仍应由测试门禁保持存活。");
            Assert.False(closing.IsCompleted, "不能只凭宿主退出提前完成关闭。");
        }
        finally { release.Set(); }
        await closing.WaitAsync(TimeSpan.FromSeconds(4));
        Assert.True(fixture.HasExited);
        Assert.Equal(0, fixture.ExitCode);
        Assert.True(child.HasExited);
        Assert.Equal(0, child.ExitCode);
    }

    /// <summary>:returns: 不携带真实文稿的自有converter身份。</summary>
    [Fact]
    public async Task ChildOutlivingShutdownBudgetRemainsUncertainAndIsOwnedJobCleaned()
    {
        if (!OperatingSystem.IsWindows()) return;
        var scope = Identity();
        var fixture = new PresentationTransportFixture("stuck-child", scope);
        try
        {
            Assert.Equal("succeeded", (await fixture.Client.SendAsync(Request(scope, 3), scope)).Status);
            using var child = Process.GetProcessById(int.Parse(await File.ReadAllTextAsync(Path.Combine(fixture.Root, "child-pid")), CultureInfo.InvariantCulture));
            _ = child.Handle;
            Assert.False(child.HasExited);
            var elapsed = Stopwatch.StartNew();
            var failed = await Assert.ThrowsAsync<PresentationTransportException>(() =>
                fixture.Client.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(20)));
            Assert.IsType<TimeoutException>(failed.InnerException);
            Assert.InRange(elapsed.Elapsed.TotalSeconds, 14, 20);
            Assert.True(fixture.HasExited);
            Assert.Equal(0, fixture.ExitCode);
            await child.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(3));
            Assert.True(child.HasExited);
            var repeated = await Assert.ThrowsAsync<PresentationTransportException>(() => fixture.Client.DisposeAsync().AsTask());
            Assert.Same(failed, repeated);
        }
        finally
        {
            try { await fixture.DisposeAsync(); }
            catch (PresentationTransportException) { }
        }
    }

    /// <summary>:returns: 不携带真实文稿的自有converter身份。</summary>
    private static PresentationScope Identity() => new()
    { Role = "converter", EngineInstanceId = Guid.NewGuid(), GroupEpoch = 1, OwnerEpoch = 1, EngineEpoch = 1 };

    /// <summary>:param scope: 身份。:param seconds: deadline预算。:returns: 合法关闭消息，测试侧车仅回纯值。</summary>
    private static PresentationRequest Request(PresentationScope scope, double seconds) => new()
    {
        Scope = scope, Operation = "shutdown", OperationId = Guid.NewGuid(), ParentShutdownId = Guid.NewGuid(),
        Deadline = DateTimeOffset.UtcNow.AddSeconds(seconds).ToString("O"),
    };

    /// <summary>:param root: 测试侧车证据目录。:returns: 等真实接收证据，不用固定延时推测已dispatch。</summary>
    private static Task WaitForReceiptAsync(string root) => WaitForFileAsync(Path.Combine(root, "received"));

    /// <summary>:param path: 自有侧车的公开证据文件。:returns: 有界等待真实外部事件。</summary>
    private static async Task WaitForFileAsync(string path)
    {
        using var budget = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (!File.Exists(path)) await Task.Delay(10, budget.Token);
    }
}
