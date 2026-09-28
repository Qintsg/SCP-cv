// 有限本机 LibVLC 异步失败验证：隐藏 STA、受控 RTSP 404，无 D4、Office 或音视频内容输出。
using System.Text.Json;
using System.Collections.Concurrent;
using System.IO;
using System.Windows.Threading;
using LibVLCSharp.Shared;
using ScpCv.Contracts.Ipc;
using ScpCv.PlayerWorker;
using ScpCv.PlayerWorker.Playback;
using Xunit.Abstractions;

namespace ScpCv.Windows.Tests;

public sealed class PlayerRuntimeHostVlcErrorTests(ITestOutputHelper output)
{
    /// <summary>直接核验库的异步错误与后续状态，回调只记录信号，不调用原生 API。</summary>
    [Fact]
    [Trait("Category", "Physical")]
    public async Task NativePlayAcceptancePrecedesRtspErrorAndEndedState()
    {
        await using var server = new RejectingRtspServer();
        Core.Initialize();
        using var lib = new LibVLC("--no-video", "--no-audio", "--vout=dummy", "--aout=dummy");
        using var media = new Media(lib, server.Uri, FromType.FromLocation);
        using var player = new MediaPlayer(lib);
        var signals = new ConcurrentQueue<string>();
        var failed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        void Error(object? _, EventArgs __) { signals.Enqueue("EncounteredError"); failed.TrySetResult(); }
        void Stopped(object? _, EventArgs __) => signals.Enqueue("Stopped");
        player.EncounteredError += Error;
        player.Stopped += Stopped;
        try
        {
            var accepted = player.Play(media);
            output.WriteLine($"native Play returned {accepted}");
            Assert.True(accepted);
            var previous = VLCState.NothingSpecial;
            var deadline = DateTimeOffset.UtcNow.AddSeconds(8);
            do
            {
                var state = player.State;
                if (state != previous) { output.WriteLine($"native State: {previous} -> {state}"); previous = state; }
                if (failed.Task.IsCompleted && state == VLCState.Ended) break;
                await Task.Delay(10);
            } while (DateTimeOffset.UtcNow < deadline);
            await failed.Task.WaitAsync(TimeSpan.FromSeconds(2));
            foreach (var signal in signals) output.WriteLine($"native event: {signal}");
            output.WriteLine($"final native State: {player.State}");
            Assert.Equal(VLCState.Ended, player.State);
            Assert.Contains("EncounteredError", signals);
        }
        finally
        {
            player.EncounteredError -= Error;
            player.Stopped -= Stopped;
            await Task.Run(player.Stop);
        }
    }

    /// <summary>独立第三方差分：同库、CPU/dummy、同媒体 Stop/Play，但无 Host/WPF/新状态标记。</summary>
    [Fact]
    [Trait("Category", "Physical")]
    public async Task NativeOnlyRepeatedStopPlayCompletesTwoEndEvents()
    {
        Core.Initialize();
        using var lib = new LibVLC("--no-audio", "--vout=dummy", "--aout=dummy", "--avcodec-hw=none", "--no-osd");
        using var media = new Media(lib, FixturePath(), FromType.FromPath);
        using var player = new MediaPlayer(lib) { EnableHardwareDecoding = false };
        output.WriteLine("native-only explicit per-player EnableHardwareDecoding=false");
        var ended = 0;
        void Ended(object? _, EventArgs __) => Interlocked.Increment(ref ended);
        player.EndReached += Ended;
        try
        {
            for (var cycle = 1; cycle <= 3; cycle++)
            {
                Assert.True(player.Play(media));
                var deadline = DateTimeOffset.UtcNow.AddSeconds(4);
                do
                {
                    output.WriteLine($"native-only cycle={cycle} end={Volatile.Read(ref ended)} state={player.State} pos={player.Time}");
                    if (Volatile.Read(ref ended) >= cycle) break;
                    await Task.Delay(100);
                } while (DateTimeOffset.UtcNow < deadline);
                Assert.True(Volatile.Read(ref ended) >= cycle, "独立 native-only 没有真实自然结束事件。");
                await Task.Run(player.Stop);
            }
        }
        finally { player.EndReached -= Ended; await Task.Run(player.Stop); }
    }

    /// <summary>请求被原生 Play 接受不等于流可播；真实 RTSP 404 必须出现在公共状态采样中。</summary>
    [Fact]
    [Trait("Category", "Physical")]
    public async Task AcceptedNativePlayReportsErrorAfterControlledRtsp404()
    {
        await using var server = new RejectingRtspServer();
        await OnHiddenDispatcherAsync(async () =>
        {
            var window = new PlayerWindow();
            try
            {
                await using var host = new PlayerRuntimeHost(window, 1);
                var accepted = await host.ExecuteAsync(OpenLease(101, 1, server.Uri), CancellationToken.None);
                Assert.Equal("completed", accepted.Status);
                await server.DescribeRejected.Task.WaitAsync(TimeSpan.FromSeconds(8));
                var deadline = DateTimeOffset.UtcNow.AddSeconds(5);
                JsonElement observed = accepted.ActualState;
                do
                {
                    var sample = await host.SampleProgressAsync(CancellationToken.None);
                    if (sample is not null) observed = sample.State;
                    if (observed.GetProperty("playback_state").GetString() == "error") break;
                    await Task.Delay(25);
                } while (DateTimeOffset.UtcNow < deadline);

                Assert.False(window.IsVisible);
                Assert.Equal(nint.Zero, window.NativeHandle);
                Assert.Equal(101, observed.GetProperty("source_id").GetInt64());
                Assert.Equal(1, observed.GetProperty("source_generation").GetInt64());
                Assert.Equal("error", observed.GetProperty("playback_state").GetString());
                Assert.False(string.IsNullOrWhiteSpace(observed.GetProperty("error_message").GetString()));
            }
            finally { window.Close(); }
        });
    }

    /// <summary>Stop 后 Play 使用有效长寿命 Media；至少两次真实进度回绕证明循环继续工作。</summary>
    [Fact]
    [Trait("Category", "Physical")]
    public async Task StopThenPlayKeepsOwnedMediaAliveAcrossTwoNativeLoops()
    {
        var path = FixturePath();
        await OnHiddenDispatcherAsync(async () =>
        {
            var window = new PlayerWindow();
            try
            {
                await using var host = SilentHost(window);
                await host.ExecuteAsync(FileLease(101, 1, path, loop: true), CancellationToken.None);
                await WaitForProgressAsync(host, 300);
                var stopped = await host.ExecuteAsync(ControlLease("STOP", 1), CancellationToken.None);
                Assert.Equal("stopped", stopped.ActualState.GetProperty("playback_state").GetString());
                await host.ExecuteAsync(ControlLease("PLAY", 1), CancellationToken.None);
                var previous = -1L;
                var wraps = 0;
                var deadline = DateTimeOffset.UtcNow.AddSeconds(12);
                do
                {
                    var state = await SampleAsync(host);
                    if (state is null) { await Task.Delay(50); continue; }
                    var observed = state.Value;
                    if (observed.GetProperty("playback_state").GetString() == "error")
                        Assert.Fail(observed.GetRawText());
                    var position = observed.GetProperty("position_ms").GetInt64();
                    if (observed.GetProperty("playback_state").GetString() == "playing")
                    {
                        if (previous > 500 && position + 250 < previous) wraps++;
                        previous = position;
                    }
                    output.WriteLine($"after STOP/PLAY wraps={wraps}: {observed.GetRawText()}");
                    if (wraps >= 2) break;
                    await Task.Delay(100);
                } while (DateTimeOffset.UtcNow < deadline);
                Assert.True(wraps >= 2, "没有观察到 STOP→PLAY 后至少两次真实视频进度回绕。");
                Assert.False(window.IsVisible);
                Assert.Equal(nint.Zero, window.NativeHandle);
            }
            finally { window.Close(); }
        });
    }

    /// <summary>健康同源打开和暂停恢复仅换公开代次，不将已经播放的媒体归零。</summary>
    [Fact]
    [Trait("Category", "Physical")]
    public async Task HealthySameSourceOpenAndPausedResumeKeepNativePosition()
    {
        var path = FixturePath();
        await OnHiddenDispatcherAsync(async () =>
        {
            var window = new PlayerWindow();
            try
            {
                await using var host = SilentHost(window);
                await host.ExecuteAsync(FileLease(101, 1, path, loop: false), CancellationToken.None);
                var before = await WaitForProgressAsync(host, 500);
                var reopened = await host.ExecuteAsync(FileLease(101, 2, path, loop: false), CancellationToken.None);
                output.WriteLine($"healthy same-source before: {before.GetRawText()}");
                output.WriteLine($"healthy same-source after: {reopened.ActualState.GetRawText()}");
                Assert.Equal(2, reopened.ActualState.GetProperty("source_generation").GetInt64());
                Assert.True(reopened.ActualState.GetProperty("position_ms").GetInt64() >= before.GetProperty("position_ms").GetInt64() - 100);
                await host.ExecuteAsync(ControlLease("PAUSE", 2), CancellationToken.None);
                await Task.Delay(100);
                var paused = (await SampleAsync(host)) ?? throw new InvalidOperationException("暂停后没有公开样本。");
                var pausedOpen = await host.ExecuteAsync(FileLease(101, 3, path, loop: false, autoplay: false), CancellationToken.None);
                output.WriteLine($"paused same-source before: {paused.GetRawText()}");
                output.WriteLine($"paused same-source after: {pausedOpen.ActualState.GetRawText()}");
                Assert.Equal("paused", pausedOpen.ActualState.GetProperty("playback_state").GetString());
                Assert.True(pausedOpen.ActualState.GetProperty("position_ms").GetInt64() >= paused.GetProperty("position_ms").GetInt64() - 100);
                await host.ExecuteAsync(ControlLease("PLAY", 3), CancellationToken.None);
                var resumed = await WaitForProgressAsync(host, paused.GetProperty("position_ms").GetInt64() + 200);
                output.WriteLine($"pause PLAY resume: {resumed.GetRawText()}");
                Assert.Equal(3, resumed.GetProperty("source_generation").GetInt64());
                Assert.False(window.IsVisible);
                Assert.Equal(nint.Zero, window.NativeHandle);
            }
            finally { window.Close(); }
        });
    }

    /// <summary>首次只预备不播放，同源再次要求自动播放必须真正启动原生输入。</summary>
    [Fact]
    [Trait("Category", "Physical")]
    public async Task PreparedSameSourceStartsNativePlaybackWhenAutoplayIsEnabled()
    {
        var path = FixturePath();
        await OnHiddenDispatcherAsync(async () =>
        {
            var window = new PlayerWindow();
            try
            {
                await using var host = SilentHost(window);
                var prepared = await host.ExecuteAsync(FileLease(101, 1, path, loop: false, autoplay: false), CancellationToken.None);
                Assert.Equal("paused", prepared.ActualState.GetProperty("playback_state").GetString());
                Assert.Equal(0, prepared.ActualState.GetProperty("position_ms").GetInt64());
                await host.ExecuteAsync(FileLease(101, 2, path, loop: false, autoplay: true), CancellationToken.None);
                var playing = await WaitForProgressAsync(host, 300);
                output.WriteLine($"prepared false -> same-source autoplay true: {playing.GetRawText()}");
                Assert.Equal(2, playing.GetProperty("source_generation").GetInt64());
                Assert.False(window.IsVisible);
                Assert.Equal(nint.Zero, window.NativeHandle);
            }
            finally { window.Close(); }
        });
    }

    /// <summary>本轮显式 Physical 夹具只读取文件，不生成、移动或覆盖用户资源。</summary>
    private static string FixturePath()
    {
        var path = Environment.GetEnvironmentVariable("SCP_CV_VLC_FAILURE_FIXTURE");
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            throw new InvalidOperationException("显式 Physical 循环回归需配置 SCP_CV_VLC_FAILURE_FIXTURE 为有限静音测试视频。");
        return Path.GetFullPath(path);
    }

    /// <summary>与生产相同的 Host、加载与共享实例；CPU 解码维持时钟，输出仅 dummy、音频禁用。</summary>
    private static PlayerRuntimeHost SilentHost(PlayerWindow window) => new(window, 1,
        libVlcFactory: () => new LibVLC("--no-audio", "--vout=dummy", "--aout=dummy", "--avcodec-hw=none", "--no-osd"),
        configureVlcPlayer: player => player.EnableHardwareDecoding = false);

    /// <summary>创建与生产媒体相同的公开租约字段，不访问私有播放器。</summary>
    private static CommandLeaseDto FileLease(long sourceId, long generation, string path, bool loop, bool autoplay = true) =>
        OpenLease(sourceId, generation, path) with
        {
            Args = JsonSerializer.SerializeToElement(new { source_id = sourceId, uri = path, source_type = "video", autoplay, loop })
                .EnumerateObject().ToDictionary(property => property.Name, property => property.Value),
        };

    /// <summary>本轮控制保留当前源代次，不写系统音量。</summary>
    private static CommandLeaseDto ControlLease(string action, long generation) => new()
    {
        CommandId = Guid.NewGuid(), Command = action, TargetSequence = generation,
        SourceGeneration = generation, SourceRevision = 1,
    };

    /// <summary>从公开采样或只改内存循环意图的公开回执读取状态。</summary>
    private static async Task<JsonElement?> SampleAsync(PlayerRuntimeHost host)
    {
        var sample = await host.SampleProgressAsync(CancellationToken.None);
        if (sample is not null) return sample.State;
        await Task.Delay(25);
        sample = await host.SampleProgressAsync(CancellationToken.None);
        return sample?.State;
    }

    /// <summary>既检查真实状态又检查原生进度，不以 loop_enabled/Play 返回值宣称持续播放。</summary>
    private static async Task<JsonElement> WaitForProgressAsync(PlayerRuntimeHost host, long minimum)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(6);
        JsonElement state = default;
        do
        {
            var sample = await SampleAsync(host);
            if (sample is null) { await Task.Delay(50); continue; }
            state = sample.Value;
            if (state.GetProperty("playback_state").GetString() == "error") Assert.Fail(state.GetRawText());
            if (state.GetProperty("playback_state").GetString() == "playing" && state.GetProperty("position_ms").GetInt64() >= minimum)
                return state;
            await Task.Delay(25);
        } while (DateTimeOffset.UtcNow < deadline);
        Assert.Fail($"未在有限预算内观察到真实进度：{state.GetRawText()}");
        return state;
    }

    /// <summary>只经公开播控合同构造本轮独立源意图。</summary>
    private static CommandLeaseDto OpenLease(long sourceId, long generation, string uri) => new()
    {
        CommandId = Guid.NewGuid(), Command = "OPEN", TargetSequence = generation,
        SourceGeneration = generation, SourceRevision = 1,
        Args = JsonSerializer.SerializeToElement(new { source_id = sourceId, uri, source_type = "rtsp", autoplay = true })
            .EnumerateObject().ToDictionary(property => property.Name, property => property.Value),
    };

    /// <summary>自有 STA 消息泵不调用 Show；有限源没有任何可解码音视频载荷。</summary>
    private static async Task OnHiddenDispatcherAsync(Func<Task> action)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            var dispatcher = Dispatcher.CurrentDispatcher;
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(dispatcher));
            _ = dispatcher.InvokeAsync(async () =>
            {
                try { await action(); completion.TrySetResult(); }
                catch (Exception exception) { completion.TrySetException(exception); }
                finally { dispatcher.BeginInvokeShutdown(DispatcherPriority.Background); }
            });
            Dispatcher.Run();
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        try { await completion.Task.WaitAsync(TimeSpan.FromSeconds(20)); }
        finally { Assert.True(thread.Join(TimeSpan.FromSeconds(5)), "有限 VLC 失败回归的自有 STA 未退出。"); }
    }
}
