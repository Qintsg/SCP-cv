// 公开Player切源的双清理失败回归；只替代外部文稿引擎，不显示窗口/启动Office或操作设备。
using System.Text.Json;
using System.Windows.Controls;
using ScpCv.Contracts.Presentations;
using ScpCv.PlayerWorker;
using ScpCv.PlayerWorker.Playback;
using ScpCv.PlayerWorker.Presentations;

namespace ScpCv.Windows.Tests;

public sealed partial class PlayerRuntimeHostLifecycleTests
{
    /// <summary>:returns: 旧退役和候选清理双失败也须隔离后续普通控制，不能只保护Activate失败路径。</summary>
    [Fact]
    public async Task FailedRetirementCandidateCleanupAlsoQuarantinesPlayback()
    {
        await OnHiddenDispatcherAsync(async () =>
        {
            var previous = new ControlledPresentation(() => ValueTask.FromException(
                new PresentationTransportException("旧关闭未确认", new EndOfStreamException())));
            var candidate = new ControlledPresentation(() => ValueTask.FromException(new IOException("候选清理未确认")));
            var adapters = new Queue<IPresentationSurface>([previous, candidate]);
            var window = new PlayerWindow();
            var host = new PlayerRuntimeHost(window, 1)
            { PresentationFactory = (_, _, _) => Task.FromResult(adapters.Dequeue()) };
            try
            {
                await host.ExecuteAsync(OpenLease(CreatePackage()), CancellationToken.None);
                var replacement = OpenLease(CreatePackage()) with { SourceGeneration = 2 };
                await Assert.ThrowsAsync<PresentationTransportException>(() => host.ExecuteAsync(replacement, CancellationToken.None));
                await Assert.ThrowsAsync<PresentationTransportException>(() => host.ExecuteAsync(replacement with { Command = "NEXT_EFFECT" }, CancellationToken.None));
                Assert.Null(await host.SampleProgressAsync(CancellationToken.None));
                Assert.Equal(1, previous.CloseCalls);
                Assert.Equal(1, candidate.CloseCalls);
            }
            finally
            {
                try { await host.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(3)); }
                catch (IOException) { }
                catch (AggregateException) { }
                window.Close();
            }
        });
    }

    /// <summary>:returns: 候选清理第二个异常不得把旧引擎不确定降为普通失败，也不能发布候选源/布局。</summary>
    [Fact]
    public async Task ReplacementCleanupFailureDoesNotOverwriteRetirementUncertainty()
    {
        await OnHiddenDispatcherAsync(async () =>
        {
            var retirement = new PresentationTransportException("旧文稿关闭回执缺失", new EndOfStreamException());
            var cleanup = new IOException("候选文稿清理未确认");
            var previous = new ControlledPresentation(() => ValueTask.FromException(retirement));
            var candidate = new ControlledPresentation(() => ValueTask.FromException(cleanup));
            var adapters = new Queue<IPresentationSurface>([previous, candidate]);
            var window = new PlayerWindow();
            var host = new PlayerRuntimeHost(window, 1)
            { PresentationFactory = (_, _, _) => Task.FromResult(adapters.Dequeue()) };
            try
            {
                var first = OpenLease(CreatePackage());
                first.Args["fit_text"] = JsonSerializer.SerializeToElement(true);
                await host.ExecuteAsync(first, CancellationToken.None);
                var surfaces = Assert.IsType<Grid>(window.FindName("SurfaceHost"));
                var original = Assert.Single(surfaces.Children.Cast<object>());
                var replacement = OpenLease(CreatePackage()) with { SourceGeneration = 2 };
                replacement.Args["source_id"] = JsonSerializer.SerializeToElement(202);
                replacement.Args["fit_text"] = JsonSerializer.SerializeToElement(false);

                var error = await Assert.ThrowsAsync<PresentationTransportException>(() => host.ExecuteAsync(replacement, CancellationToken.None));
                var reasons = Assert.IsType<AggregateException>(error.InnerException);
                Assert.Collection(reasons.InnerExceptions, item => Assert.Same(retirement, item), item => Assert.Same(cleanup, item));
                Assert.Equal(1, previous.CloseCalls);
                Assert.Equal(1, candidate.CloseCalls);
                Assert.Same(original, Assert.Single(surfaces.Children.Cast<object>()));
                var observed = await host.SampleProgressAsync(CancellationToken.None);
                // 两资源关闭均未确认，只保留历史观测；不能借受控adapter伪造新的实时playing采样。
                Assert.Null(observed);
                Assert.False(window.IsVisible);
                Assert.Equal(nint.Zero, window.NativeHandle);
            }
            finally
            {
                try { await host.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(3)); }
                catch (PresentationTransportException) { }
                catch (AggregateException) { }
                window.Close();
            }
        });
    }

    /// <summary>:param retirementUncertain: 旧异常是否已有不确定类型。:param cleanupUncertain: 候选异常是否已有不确定类型。:returns: 双外部清理失败均保留原因，不借最后的普通IO异常报告安全失败。</summary>
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task BothReplacementCloseFailuresRemainUncertain(bool retirementUncertain, bool cleanupUncertain)
    {
        await OnHiddenDispatcherAsync(async () =>
        {
            var retirement = ReplacementFailure(retirementUncertain, "旧资源");
            var cleanup = ReplacementFailure(cleanupUncertain, "候选资源");
            var previous = new ControlledPresentation(() => ValueTask.FromException(retirement));
            var candidate = new ControlledPresentation(() => ValueTask.FromException(cleanup));
            var adapters = new Queue<IPresentationSurface>([previous, candidate]);
            var window = new PlayerWindow();
            var host = new PlayerRuntimeHost(window, 1)
            { PresentationFactory = (_, _, _) => Task.FromResult(adapters.Dequeue()) };
            try
            {
                await host.ExecuteAsync(OpenLease(CreatePackage()), CancellationToken.None);
                var error = await Assert.ThrowsAsync<PresentationTransportException>(() => host.ExecuteAsync(
                    OpenLease(CreatePackage()) with { SourceGeneration = 2 }, CancellationToken.None));
                var reasons = Assert.IsType<AggregateException>(error.InnerException);
                Assert.Collection(reasons.InnerExceptions, item => Assert.Same(retirement, item), item => Assert.Same(cleanup, item));
                Assert.Equal(1, previous.CloseCalls);
                Assert.Equal(1, candidate.CloseCalls);
            }
            finally
            {
                try { await host.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(3)); }
                catch (IOException) { }
                catch (AggregateException) { }
                window.Close();
            }
        });
    }

    /// <summary>:param uncertain: 原异常类型。:returns: 候选成功清理时仍抛原实例/类型，不能把一般已知失败无条件升级。</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SuccessfulCandidateCleanupPreservesOriginalRetirementFailure(bool uncertain)
    {
        await OnHiddenDispatcherAsync(async () =>
        {
            var retirement = ReplacementFailure(uncertain, "旧资源");
            var previous = new ControlledPresentation(() => ValueTask.FromException(retirement));
            var candidate = new ControlledPresentation(() => ValueTask.CompletedTask);
            var adapters = new Queue<IPresentationSurface>([previous, candidate]);
            var window = new PlayerWindow();
            var host = new PlayerRuntimeHost(window, 1)
            { PresentationFactory = (_, _, _) => Task.FromResult(adapters.Dequeue()) };
            try
            {
                await host.ExecuteAsync(OpenLease(CreatePackage()), CancellationToken.None);
                var raised = await Assert.ThrowsAnyAsync<IOException>(() => host.ExecuteAsync(
                    OpenLease(CreatePackage()) with { SourceGeneration = 2 }, CancellationToken.None));
                Assert.Same(retirement, raised);
                Assert.Equal(1, previous.CloseCalls);
                Assert.Equal(1, candidate.CloseCalls);
                var state = await host.SampleProgressAsync(CancellationToken.None);
                Assert.NotNull(state);
                Assert.Equal(1, state.SourceGeneration);
                Assert.Equal(101, state.State.GetProperty("source_id").GetInt64());
            }
            finally
            {
                try { await host.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(3)); }
                catch (IOException) { }
                window.Close();
            }
        });
    }

    /// <summary>:returns: 正常关闭旧资源后只发布候选，真实源/布局随成功资源切换，退出不重关旧候选。</summary>
    [Fact]
    public async Task SuccessfulReplacementPublishesOnlyCandidateState()
    {
        await OnHiddenDispatcherAsync(async () =>
        {
            var previous = new ControlledPresentation(() => ValueTask.CompletedTask);
            var candidate = new ControlledPresentation(() => ValueTask.CompletedTask);
            var adapters = new Queue<IPresentationSurface>([previous, candidate]);
            var window = new PlayerWindow();
            var host = new PlayerRuntimeHost(window, 1)
            { PresentationFactory = (_, _, _) => Task.FromResult(adapters.Dequeue()) };
            try
            {
                await host.ExecuteAsync(OpenLease(CreatePackage()), CancellationToken.None);
                var surfaces = Assert.IsType<Grid>(window.FindName("SurfaceHost"));
                var original = Assert.Single(surfaces.Children.Cast<object>());
                var replacement = OpenLease(CreatePackage()) with { SourceGeneration = 2 };
                replacement.Args["source_id"] = JsonSerializer.SerializeToElement(202);
                replacement.Args["fit_text"] = JsonSerializer.SerializeToElement(true);
                var result = await host.ExecuteAsync(replacement, CancellationToken.None);
                Assert.Equal("completed", result.Status);
                Assert.Equal(202, result.ActualState.GetProperty("source_id").GetInt64());
                Assert.Equal(2, result.ActualState.GetProperty("source_generation").GetInt64());
                Assert.True(result.ActualState.GetProperty("actual_fit_text").GetBoolean());
                Assert.NotSame(original, Assert.Single(surfaces.Children.Cast<object>()));
                Assert.Equal(1, previous.CloseCalls);
                Assert.Equal(0, candidate.CloseCalls);
                await host.DisposeAsync();
                Assert.Equal(1, previous.CloseCalls);
                Assert.Equal(1, candidate.CloseCalls);
            }
            finally { await host.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(3)); window.Close(); }
        });
    }

    /// <summary>:param uncertain: 是否已有原生不确定证据。:param role: 异常出处。:returns: 外部adapter注入的明确异常，不模拟宿主内部逻辑。</summary>
    private static IOException ReplacementFailure(bool uncertain, string role) => uncertain
        ? new PresentationTransportException(role + "关闭回执缺失", new EndOfStreamException())
        : new IOException(role + "关闭失败");
}
