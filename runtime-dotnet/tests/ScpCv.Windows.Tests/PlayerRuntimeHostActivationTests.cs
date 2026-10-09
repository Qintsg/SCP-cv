// 候选原生窗口切入失败的公开Player回归；不显示窗口，不启动Office或操作真实显示器。
using System.ComponentModel;
using System.Text.Json;
using ScpCv.Contracts.Runtime;
using ScpCv.Contracts.Presentations;
using ScpCv.Contracts.Ipc;
using ScpCv.PlayerWorker;
using ScpCv.PlayerWorker.Playback;
using ScpCv.PlayerWorker.Presentations;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ScpCv.Windows.Tests;

public sealed partial class PlayerRuntimeHostLifecycleTests
{
    /// <summary>:returns: 候选切入失败后不能被后台采样成playing；已确认旧关闭/候选释放时须给出空输出错误。</summary>
    [Fact]
    public async Task FailedActivationNeverBecomesPlayingInLaterObservation()
    {
        await OnHiddenDispatcherAsync(async () =>
        {
            var activation = new Win32Exception("本轮候选窗口切入失败");
            var previous = new ControlledPresentation(() => ValueTask.CompletedTask);
            var candidate = new ControlledPresentation(() => ValueTask.CompletedTask, () => throw activation);
            var adapters = new Queue<IPresentationSurface>([previous, candidate]);
            var window = new PlayerWindow();
            var host = new PlayerRuntimeHost(window, 1)
            { PresentationFactory = (_, _, _) => Task.FromResult(adapters.Dequeue()) };
            try
            {
                await host.ExecuteAsync(OpenLease(CreatePackage()), CancellationToken.None);
                var replacement = OpenLease(CreatePackage()) with { SourceGeneration = 2 };
                replacement.Args["source_id"] = JsonSerializer.SerializeToElement(202);
                WorkerExecutionResult? result = null;
                try { result = await host.ExecuteAsync(replacement, CancellationToken.None); }
                catch (Win32Exception) { }
                // 先证实迟到公开采样没有把已失败的候选宣布playing，不只测试异常确实抛出。
                var sample = await host.SampleProgressAsync(CancellationToken.None);
                Assert.Null(sample);
                Assert.NotNull(result);
                Assert.Equal("failed", result.Status);
                Assert.Equal("presentation_activation_failed", result.ResultCode);
                Assert.Equal(JsonValueKind.Null, result.ActualState.GetProperty("source_id").ValueKind);
                Assert.Equal("error", result.ActualState.GetProperty("playback_state").GetString());
                Assert.Equal(1, previous.CloseCalls);
                Assert.Equal(1, candidate.CloseCalls);
                Assert.False(window.IsVisible);
                Assert.Equal(nint.Zero, window.NativeHandle);
            }
            finally { await host.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(3)); window.Close(); }
        });
    }

    /// <summary>:returns: 切入和清理双失败保持不确定，拒绝采样/新控制，退出仍持有候选归属而不静默遗失。</summary>
    [Fact]
    public async Task FailedActivationCleanupRetainsOwnershipAndQuarantinesControls()
    {
        await OnHiddenDispatcherAsync(async () =>
        {
            var activation = new Win32Exception("本轮候选切入未确认");
            var cleanup = new IOException("本轮候选关闭未确认");
            var previous = new ControlledPresentation(() => ValueTask.CompletedTask);
            var candidate = new ControlledPresentation(() => ValueTask.FromException(cleanup), () => throw activation);
            var adapters = new Queue<IPresentationSurface>([previous, candidate]);
            var window = new PlayerWindow();
            var host = new PlayerRuntimeHost(window, 1)
            { PresentationFactory = (_, _, _) => Task.FromResult(adapters.Dequeue()) };
            try
            {
                await host.ExecuteAsync(OpenLease(CreatePackage()), CancellationToken.None);
                var replacement = OpenLease(CreatePackage()) with { SourceGeneration = 2 };
                replacement.Args["source_id"] = JsonSerializer.SerializeToElement(202);
                var failure = await Assert.ThrowsAsync<PresentationTransportException>(() => host.ExecuteAsync(replacement, CancellationToken.None));
                var reasons = Assert.IsType<AggregateException>(failure.InnerException);
                Assert.Collection(reasons.InnerExceptions, item => Assert.Same(activation, item), item => Assert.Same(cleanup, item));
                Assert.Null(await host.SampleProgressAsync(CancellationToken.None));
                await Assert.ThrowsAsync<PresentationTransportException>(() => host.ExecuteAsync(replacement with { Command = "NEXT_EFFECT" }, CancellationToken.None));
                await Assert.ThrowsAsync<PresentationTransportException>(() => host.ExecuteAsync(replacement, CancellationToken.None));
                Assert.Equal(1, previous.CloseCalls);
                Assert.Equal(1, candidate.CloseCalls);
                // 外部adapter这里只计关闭尝试；实际LibreOfficeSurface自身缓存首次关闭终态，不重放UNO。
                var exit = await Assert.ThrowsAnyAsync<IOException>(() => host.DisposeAsync().AsTask());
                Assert.Same(cleanup, exit);
                Assert.Equal(1, previous.CloseCalls);
                Assert.Equal(2, candidate.CloseCalls);
                Assert.Same(exit, await Assert.ThrowsAnyAsync<IOException>(() => host.DisposeAsync().AsTask()));
                Assert.Equal(2, candidate.CloseCalls);
            }
            finally
            {
                try { await host.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(3)); }
                catch (IOException) { }
                window.Close();
            }
        });
    }

    /// <summary>:param hadPrevious: 冷开或已确认退役旧文稿。:returns: 失败空输出身份完整清空，显式新OPEN仍可正常恢复。</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ConfirmedEmptyActivationFailureClearsPresentationIdentityAndAllowsRecovery(bool hadPrevious)
    {
        await OnHiddenDispatcherAsync(async () =>
        {
            var previous = new ControlledPresentation(() => ValueTask.CompletedTask);
            var failed = new ControlledPresentation(() => ValueTask.CompletedTask, () => throw new Win32Exception("候选切入失败"));
            var recovered = new ControlledPresentation(() => ValueTask.CompletedTask);
            var adapters = new Queue<IPresentationSurface>();
            if (hadPrevious) adapters.Enqueue(previous);
            adapters.Enqueue(failed); adapters.Enqueue(recovered);
            var window = new PlayerWindow();
            var host = new PlayerRuntimeHost(window, 1)
            { PresentationFactory = (_, _, _) => Task.FromResult(adapters.Dequeue()) };
            try
            {
                if (hadPrevious) await host.ExecuteAsync(OpenLease(CreatePackage()), CancellationToken.None);
                var replacement = OpenLease(CreatePackage()) with { SourceGeneration = 2 };
                var failure = await host.ExecuteAsync(replacement, CancellationToken.None);
                Assert.Equal("failed", failure.Status);
                Assert.Equal("presentation_activation_failed", failure.ResultCode);
                var empty = failure.ActualState;
                Assert.Equal(JsonValueKind.Null, empty.GetProperty("source_id").ValueKind);
                Assert.Equal(JsonValueKind.Null, empty.GetProperty("actual_fit_text").ValueKind);
                Assert.Equal("none", empty.GetProperty("playback_mode").GetString());
                Assert.Equal(string.Empty, empty.GetProperty("adapter_kind").GetString());
                Assert.Equal(0, empty.GetProperty("current_slide").GetInt32());
                Assert.Equal(0, empty.GetProperty("total_slides").GetInt32());
                Assert.Equal(2, empty.GetProperty("source_generation").GetInt64());
                Assert.Null(await host.SampleProgressAsync(CancellationToken.None));
                Assert.Equal(hadPrevious ? 1 : 0, previous.CloseCalls);
                Assert.Equal(1, failed.CloseCalls);
                var recovery = OpenLease(CreatePackage()) with { SourceGeneration = 3 };
                recovery.Args["source_id"] = JsonSerializer.SerializeToElement(303);
                recovery.Args["fit_text"] = JsonSerializer.SerializeToElement(true);
                var result = await host.ExecuteAsync(recovery, CancellationToken.None);
                Assert.Equal("completed", result.Status);
                Assert.Equal(303, result.ActualState.GetProperty("source_id").GetInt64());
                Assert.Equal("playing", result.ActualState.GetProperty("playback_state").GetString());
                Assert.True(result.ActualState.GetProperty("actual_fit_text").GetBoolean());
                Assert.Equal(1, failed.CloseCalls);
            }
            finally { await host.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(3)); window.Close(); }
        });
    }

    /// <summary>:returns: 未退役的真实页图在候选切入失败时继续保留源/布局/表面，不能按原生旧关闭路径清空。</summary>
    [Fact]
    public async Task FailedNativeActivationKeepsLiveSlideImageSource()
    {
        await OnHiddenDispatcherAsync(async () =>
        {
            var package = CreatePackage();
            var root = Path.GetDirectoryName(package)!;
            var bitmap = BitmapSource.Create(1, 1, 96, 96, PixelFormats.Bgra32, null, new byte[] { 0, 80, 255, 255 }, 4);
            var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(bitmap));
            using (var file = File.Open(Path.Combine(root, "page-0001.png"), FileMode.CreateNew, FileAccess.Write)) png.Save(file);
            var activation = new Win32Exception("候选切入失败，但旧页图仍可用");
            var candidate = new ControlledPresentation(() => ValueTask.CompletedTask, () => throw activation);
            var window = new PlayerWindow();
            var host = new PlayerRuntimeHost(window, 1)
            { PresentationFactory = (_, _, _) => Task.FromResult<IPresentationSurface>(candidate) };
            try
            {
                var original = OpenLease(package);
                original.Args["presentation_mode"] = JsonSerializer.SerializeToElement("slide_images");
                original.Args["slide_images_directory"] = JsonSerializer.SerializeToElement(root);
                original.Args["fit_text"] = JsonSerializer.SerializeToElement(true);
                await host.ExecuteAsync(original, CancellationToken.None);
                var surfaces = Assert.IsType<System.Windows.Controls.Grid>(window.FindName("SurfaceHost"));
                var image = Assert.Single(surfaces.Children.Cast<object>());
                Assert.Same(activation, await Assert.ThrowsAsync<Win32Exception>(() => host.ExecuteAsync(
                    OpenLease(package) with { SourceGeneration = 2 }, CancellationToken.None)));
                var observed = await host.ExecuteAsync(original with { Command = "GOTO" }, CancellationToken.None);
                Assert.Equal(101, observed.ActualState.GetProperty("source_id").GetInt64());
                Assert.Equal("slide_images", observed.ActualState.GetProperty("playback_mode").GetString());
                Assert.True(observed.ActualState.GetProperty("actual_fit_text").GetBoolean());
                Assert.Same(image, Assert.Single(surfaces.Children.Cast<object>()));
                Assert.Equal(1, candidate.CloseCalls);
            }
            finally { await host.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(3)); window.Close(); }
        });
    }

    /// <summary>:returns: 隔离后的CLOSE必须继续报告未确认；同一外部关闭终态不会因CLOSE/退出重放。</summary>
    [Fact]
    public async Task QuarantinedCloseCannotSucceedOrRepeatExternalClosure()
    {
        await OnHiddenDispatcherAsync(async () =>
        {
            var effects = 0;
            Task? externalOutcome = null;
            var cleanup = new IOException("本轮外部关闭终态未确认");
            // 仅外部adapter自行缓存一次关闭结果，不访问生产关闭模块的私有实现。
            Task CloseExternalOnce() { effects++; return Task.FromException(cleanup); }
            var candidate = new ControlledPresentation(() => new ValueTask(externalOutcome ??= CloseExternalOnce()),
                () => throw new Win32Exception("候选切入失败"));
            var window = new PlayerWindow();
            var host = new PlayerRuntimeHost(window, 1)
            { PresentationFactory = (_, _, _) => Task.FromResult<IPresentationSurface>(candidate) };
            try
            {
                var open = OpenLease(CreatePackage());
                await Assert.ThrowsAsync<PresentationTransportException>(() => host.ExecuteAsync(open, CancellationToken.None));
                var close = open with { Command = "CLOSE" };
                await Assert.ThrowsAsync<PresentationTransportException>(() => host.ExecuteAsync(close, CancellationToken.None));
                Assert.Equal(1, effects);
                Assert.Null(await host.SampleProgressAsync(CancellationToken.None));
                Assert.Same(cleanup, await Assert.ThrowsAsync<IOException>(() => host.DisposeAsync().AsTask()));
                Assert.Equal(1, effects);
                Assert.Same(cleanup, await Assert.ThrowsAsync<IOException>(() => host.DisposeAsync().AsTask()));
                Assert.Equal(1, effects);
            }
            finally
            {
                try { await host.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(3)); }
                catch (IOException) { }
                window.Close();
            }
        });
    }

    /// <summary>:returns: 已确认无实际输出后的公开翻页不得返回completed，不只靠前端禁用隐藏空操作。</summary>
    [Fact]
    public async Task NavigationAfterConfirmedEmptyActivationFailureIsRejected()
    {
        await OnHiddenDispatcherAsync(async () =>
        {
            var candidate = new ControlledPresentation(() => ValueTask.CompletedTask, () => throw new Win32Exception("切入失败"));
            var window = new PlayerWindow();
            var host = new PlayerRuntimeHost(window, 1)
            { PresentationFactory = (_, _, _) => Task.FromResult<IPresentationSurface>(candidate) };
            try
            {
                var open = OpenLease(CreatePackage());
                Assert.Equal("failed", (await host.ExecuteAsync(open, CancellationToken.None)).Status);
                await Assert.ThrowsAsync<InvalidOperationException>(() => host.ExecuteAsync(open with { Command = "NEXT" }, CancellationToken.None));
                Assert.Null(await host.SampleProgressAsync(CancellationToken.None));
            }
            finally { await host.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(3)); window.Close(); }
        });
    }
}
