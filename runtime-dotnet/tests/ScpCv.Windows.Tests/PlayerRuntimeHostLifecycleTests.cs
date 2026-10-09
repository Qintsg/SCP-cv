// 隐藏STA经公开OPEN/Dispose验证真实宿主生命周期；外部文稿引擎以受控adapter替代，不启动Office。
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Windows.Controls;
using System.Windows.Threading;
using ScpCv.Contracts.Ipc;
using ScpCv.PlayerWorker;
using ScpCv.PlayerWorker.Playback;
using ScpCv.PlayerWorker.Presentations;

namespace ScpCv.Windows.Tests;

public sealed partial class PlayerRuntimeHostLifecycleTests
{
    /// <summary>:returns: 首次外部资源关闭失败不撤画面；重复Dispose仍报告同一失败，不成为正常退出。</summary>
    [Fact]
    public async Task FailedPresentationDisposalKeepsSurfaceAndRepeatedFailure()
    {
        await OnHiddenDispatcherAsync(async () =>
        {
            var path = CreatePackage();
            var failure = new IOException("本轮受控外部引擎关闭未确认。");
            var adapter = new ControlledPresentation(() => ValueTask.FromException(failure));
            var window = new PlayerWindow();
            var host = Host(window, adapter);
            try
            {
                var opened = await host.ExecuteAsync(OpenLease(path), CancellationToken.None);
                Assert.Equal("completed", opened.Status);
                Assert.Equal(101, opened.ActualState.GetProperty("source_id").GetInt64());
                var surfaces = Assert.IsType<Grid>(window.FindName("SurfaceHost"));
                var original = Assert.Single(surfaces.Children.Cast<object>());
                Assert.False(window.IsVisible);
                Assert.Equal(nint.Zero, window.NativeHandle);

                var first = await Assert.ThrowsAsync<IOException>(() => host.DisposeAsync().AsTask());
                Assert.Same(failure, first);
                Assert.Same(original, Assert.Single(surfaces.Children.Cast<object>()));
                var repeated = await Assert.ThrowsAsync<IOException>(() => host.DisposeAsync().AsTask());
                Assert.Same(first, repeated);
                Assert.Equal(1, adapter.CloseCalls);
            }
            finally
            {
                try { await host.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(3)); }
                catch (IOException) { }
                window.Close();
            }
        });
    }

    /// <summary>:param fail: 外部关闭最终是否失败。:returns: 在途并发必须共同等待；成功幂等/失败保留，确认前不撤画面。</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ConcurrentDisposalWaitsForTheSameOwnedResourceOutcome(bool fail)
    {
        await OnHiddenDispatcherAsync(async () =>
        {
            var closeEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var closeResult = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var adapter = new ControlledPresentation(() =>
            {
                closeEntered.TrySetResult();
                return new ValueTask(closeResult.Task);
            });
            var window = new PlayerWindow();
            var host = Host(window, adapter);
            var failure = new IOException("本轮受控在途关闭失败。");
            Task? first = null;
            try
            {
                await host.ExecuteAsync(OpenLease(CreatePackage()), CancellationToken.None);
                var surfaces = Assert.IsType<Grid>(window.FindName("SurfaceHost"));
                var original = Assert.Single(surfaces.Children.Cast<object>());
                first = host.DisposeAsync().AsTask();
                await closeEntered.Task.WaitAsync(TimeSpan.FromSeconds(2));
                var concurrent = host.DisposeAsync().AsTask();
                Assert.False(first.IsCompleted);
                Assert.False(concurrent.IsCompleted);
                Assert.Same(original, Assert.Single(surfaces.Children.Cast<object>()));
                Assert.Null(await host.SampleProgressAsync(CancellationToken.None));
                if (fail)
                {
                    closeResult.SetException(failure);
                    Assert.Same(failure, await Assert.ThrowsAsync<IOException>(() => first));
                    Assert.Same(failure, await Assert.ThrowsAsync<IOException>(() => concurrent));
                    Assert.Same(failure, await Assert.ThrowsAsync<IOException>(() => host.DisposeAsync().AsTask()));
                    Assert.Same(original, Assert.Single(surfaces.Children.Cast<object>()));
                }
                else
                {
                    closeResult.SetResult();
                    await Task.WhenAll(first, concurrent).WaitAsync(TimeSpan.FromSeconds(3));
                    await host.DisposeAsync();
                    Assert.NotSame(original, Assert.Single(surfaces.Children.Cast<object>()));
                }
                Assert.Equal(1, adapter.CloseCalls);
                await Assert.ThrowsAsync<ObjectDisposedException>(() => host.ExecuteAsync(OpenLease(CreatePackage()), CancellationToken.None));
                Assert.Null(await host.SampleProgressAsync(CancellationToken.None));
                Assert.False(window.IsVisible);
                Assert.Equal(nint.Zero, window.NativeHandle);
            }
            finally
            {
                closeResult.TrySetResult();
                if (first is not null)
                {
                    try { await first.WaitAsync(TimeSpan.FromSeconds(3)); }
                    catch (IOException) { }
                }
                try { await host.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(3)); }
                catch (IOException) { }
                window.Close();
            }
        });
    }

    /// <summary>:returns: 实际App使用的退出入口等待真实Host关闭，失败必须选非零码并保留错误。</summary>
    [Fact]
    public async Task ApplicationExitReportsOwnedResourceFailureAsNonzero()
    {
        await OnHiddenDispatcherAsync(async () =>
        {
            var failure = new IOException("本轮外部资源退出未确认。");
            var window = new PlayerWindow();
            var adapter = new ControlledPresentation(() => ValueTask.FromException(failure));
            var host = Host(window, adapter);
            var exitCodes = new List<int>();
            var reported = new List<Exception>();
            var exit = new PlayerApplicationExit(() => host.DisposeAsync().AsTask(), reported.Add,
                code => { exitCodes.Add(code); return Task.CompletedTask; });
            try
            {
                await host.ExecuteAsync(OpenLease(CreatePackage()), CancellationToken.None);
                await exit.RequestAsync();
                Assert.Equal(1, Assert.Single(exitCodes));
                Assert.Same(failure, Assert.Single(reported));
                Assert.Same(failure, exit.Failure);
                await exit.RequestAsync();
                Assert.Single(exitCodes);
                Assert.Equal(1, adapter.CloseCalls);
                Assert.Same(failure, await Assert.ThrowsAsync<IOException>(() => host.DisposeAsync().AsTask()));
            }
            finally
            {
                try { await host.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(3)); }
                catch (IOException) { }
                window.Close();
            }
        });
    }

    /// <summary>:returns: 退出在途时新命令立即明确拒绝，不排进资源关闭占用的gate。</summary>
    [Fact]
    public async Task NewExecutionDuringDisposalIsRejectedWithoutWaitingForResourceClose()
    {
        await OnHiddenDispatcherAsync(async () =>
        {
            var closeEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var closeResult = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var adapter = new ControlledPresentation(() =>
            { closeEntered.TrySetResult(); return new ValueTask(closeResult.Task); });
            var window = new PlayerWindow();
            var host = Host(window, adapter);
            Task? closing = null;
            Task? rejected = null;
            try
            {
                var lease = OpenLease(CreatePackage());
                await host.ExecuteAsync(lease, CancellationToken.None);
                closing = host.DisposeAsync().AsTask();
                await closeEntered.Task.WaitAsync(TimeSpan.FromSeconds(2));
                rejected = host.ExecuteAsync(lease, CancellationToken.None);
                await Assert.ThrowsAsync<ObjectDisposedException>(() => rejected.WaitAsync(TimeSpan.FromSeconds(2)));
                Assert.False(closing.IsCompleted, "拒绝新命令不能伪完成在途外部关闭。");
            }
            finally
            {
                closeResult.TrySetResult();
                if (closing is not null) await closing.WaitAsync(TimeSpan.FromSeconds(3));
                if (rejected is not null)
                {
                    try { await rejected.WaitAsync(TimeSpan.FromSeconds(3)); }
                    catch (ObjectDisposedException) { }
                }
                await host.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(3));
                window.Close();
            }
        });
    }

    /// <summary>:returns: 关闭前已排进Background的晚采样不访问已释放gate，不再生成实际状态。</summary>
    [Fact]
    public async Task QueuedProgressAfterCompletedDisposalReturnsNoSample()
    {
        await OnHiddenDispatcherAsync(async () =>
        {
            var adapter = new ControlledPresentation(() => ValueTask.CompletedTask);
            var window = new PlayerWindow();
            var host = Host(window, adapter);
            try
            {
                await host.ExecuteAsync(OpenLease(CreatePackage()), CancellationToken.None);
                // 两个入口在同一STA依次调用，Background采样确实排队；关闭同步完成后才让消息泵执行它。
                var queued = host.SampleProgressAsync(CancellationToken.None);
                Assert.False(queued.IsCompleted, "必须先建立晚队列采样，不能用关闭后的新调用替代交错。");
                await host.DisposeAsync();
                Assert.Null(await queued.WaitAsync(TimeSpan.FromSeconds(3)));
                Assert.Equal(1, adapter.CloseCalls);
                Assert.False(window.IsVisible);
                Assert.Equal(nint.Zero, window.NativeHandle);
            }
            finally
            {
                await host.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(3));
                window.Close();
            }
        });
    }

    /// <summary>:param window: 未Show的自有窗口。:param adapter: 外部引擎接缝。:returns: 默认包门禁仍存在的真实宿主。</summary>
    private static PlayerRuntimeHost Host(PlayerWindow window, ControlledPresentation adapter) => new(window, 1)
    { PresentationFactory = (_, _, _) => Task.FromResult<IPresentationSurface>(adapter) };

    /// <summary>:param path: 本次只读元数据ZIP。:returns: 生产相同的公开OPEN参数。</summary>
    private static CommandLeaseDto OpenLease(string path) => new()
    {
        CommandId = Guid.NewGuid(), ClaimToken = Guid.NewGuid(), Command = "OPEN", TargetSequence = 1,
        GroupEpoch = 1, OwnerEpoch = 1, SourceRevision = 1, SourceGeneration = 1,
        Args = JsonSerializer.SerializeToElement(new
        { source_id = 101, uri = path, source_type = "ppt", presentation_mode = "libreoffice", autoplay = true })
            .EnumerateObject().ToDictionary(property => property.Name, property => property.Value),
    };

    /// <summary>:returns: 自有最小PPTX元数据容器；不作为可交付文稿或视觉证明，保留临时夹具。</summary>
    private static string CreatePackage()
    {
        var root = Path.Combine(Path.GetTempPath(), "scp-cv-player-lifecycle-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var path = Path.Combine(root, "source.pptx");
        using var file = File.Open(path, FileMode.CreateNew, FileAccess.ReadWrite);
        using var zip = new ZipArchive(file, ZipArchiveMode.Create);
        Add(zip, "ppt/presentation.xml", "<p:presentation xmlns:p=\"http://schemas.openxmlformats.org/presentationml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\"><p:sldIdLst><p:sldId r:id=\"r1\"/></p:sldIdLst></p:presentation>");
        Add(zip, "ppt/_rels/presentation.xml.rels", "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"r1\" Target=\"slides/slide1.xml\"/></Relationships>");
        Add(zip, "ppt/slides/slide1.xml", "<p:sld xmlns:p=\"http://schemas.openxmlformats.org/presentationml/2006/main\"/>");
        return path;
    }

    /// <summary>:param zip: 本次容器。:param path: 固定包条目。:param xml: 固定元数据。:returns: 写完的夹具条目。</summary>
    private static void Add(ZipArchive zip, string path, string xml)
    {
        using var writer = new StreamWriter(zip.CreateEntry(path).Open(), new UTF8Encoding(false));
        writer.Write(xml);
    }

    /// <summary>:param action: 公开宿主行为。:returns: 自有隐藏STA与外部等待均有预算，绝不打开用户窗口。</summary>
    private static async Task OnHiddenDispatcherAsync(Func<Task> action)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Dispatcher? ownedDispatcher = null;
        var thread = new Thread(() =>
        {
            var dispatcher = Dispatcher.CurrentDispatcher;
            ownedDispatcher = dispatcher;
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(dispatcher));
            _ = dispatcher.InvokeAsync(async () =>
            {
                try { await action(); completion.TrySetResult(); }
                catch (Exception error) { completion.TrySetException(error); }
                finally { dispatcher.BeginInvokeShutdown(DispatcherPriority.Background); }
            });
            Dispatcher.Run();
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        try { await completion.Task.WaitAsync(TimeSpan.FromSeconds(15)); }
        finally
        {
            ownedDispatcher?.BeginInvokeShutdown(DispatcherPriority.Send);
            Assert.True(thread.Join(TimeSpan.FromSeconds(5)), "本轮自有隐藏STA未在收尾预算内退出。");
        }
    }

    /// <summary>仅替代外部引擎IO的adapter；不模拟PlayerRuntimeHost自身的生命周期实现。</summary>
    private sealed class ControlledPresentation(Func<ValueTask> close, Action? activate = null) : IPresentationSurface
    {
        internal int CloseCalls { get; private set; }
        public int CurrentSlide => 1;
        public int TotalSlides => 1;

        /// <summary>:returns: 不创建或激活任何原生窗口。</summary>
        public void Activate() => activate?.Invoke();

        /// <summary>:param lease: 租约。:param operation: 动作。:param slide: 页码。:param token: 寿命。:returns: 本用例不测试控制能力。</summary>
        public Task ControlAsync(CommandLeaseDto lease, string operation, int slide, CancellationToken token) => Task.CompletedTask;

        /// <summary>:param token: 寿命。:returns: 固定外部页码采样，不触及原生引擎。</summary>
        public Task ObserveAsync(CancellationToken token) => Task.CompletedTask;

        /// <summary>:returns: 受控外部关闭结果，供真实宿主处理，不替代宿主终态记忆。</summary>
        public ValueTask DisposeAsync() { CloseCalls++; return close(); }
    }
}
