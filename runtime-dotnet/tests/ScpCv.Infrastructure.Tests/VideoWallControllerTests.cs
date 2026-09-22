using ScpCv.Infrastructure.VideoWall;

namespace ScpCv.Infrastructure.Tests;

/// <summary>校对视频墙下发顺序、重试与失败语义（对应清理前提交 <c>e822be9</c> 的旧实现）。</summary>
public sealed class VideoWallControllerTests
{
    [Fact]
    public async Task DispatchSendsClearThenWaitsThenMappingCommitRefresh()
    {
        var timeline = new List<string>();
        var transport = new RecordingTransport(timeline);
        var options = FastOptions() with
        {
            ClearToMappingDelay = TimeSpan.FromMilliseconds(37),
            Wait = (delay, _) =>
            {
                timeline.Add($"wait:{delay.TotalMilliseconds:0}");
                return Task.CompletedTask;
            },
        };
        var controller = new TcpVideoWallController(transport, options);

        await controller.DispatchAsync("single");

        // 阶段内并行、阶段间串行：清屏全部发完 → 等设备清完 → 映射 → 提交 → 刷新。
        Assert.Equal(
            ["send:clear", "wait:37", "send:mapping", "send:commit", "send:refresh"],
            Collapse(timeline));
        Assert.Equal(200, transport.Sends.Count);
        Assert.Equal(
            [0xFF, 0x02, 0xB8, 0xB9],
            transport.Sends
                .Where(send => send.Ip == "192.168.5.101")
                .Select(send => send.Packet[3])
                .ToArray());
    }

    [Fact]
    public async Task DispatchRetriesTransientFailuresUntilTheNodeAccepts()
    {
        var transport = new RecordingTransport([]) { FailuresBeforeSuccess = 2 };
        var controller = new TcpVideoWallController(transport, FastOptions());

        await controller.DispatchAsync("double");

        // 同一节点在 4 个阶段各收 1 个包，每个包各重试到第 3 次才成功。
        Assert.Equal(200, transport.AttemptsByPhase.Count);
        Assert.All(transport.AttemptsByPhase.Values, attempts => Assert.Equal(3, attempts));
        Assert.Equal(200, transport.Sends.Count);
    }

    [Fact]
    public async Task DispatchStopsAfterTheFirstPhaseWithFailures()
    {
        var transport = new RecordingTransport([]) { FailuresBeforeSuccess = int.MaxValue };
        var controller = new TcpVideoWallController(transport, FastOptions());

        var error = await Assert.ThrowsAsync<VideoWallException>(() => controller.DispatchAsync("single"));

        // 重试到上限即放弃，且清屏失败后不再下发映射/提交/刷新：半套映射提交上去比不切换更糟。
        Assert.Equal(50, transport.AttemptsByPhase.Count);
        Assert.All(transport.AttemptsByPhase.Values, attempts => Assert.Equal(5, attempts));
        Assert.Empty(transport.Sends);
        Assert.Contains("192.168.5.101:4830 phase=clear", error.Message);
        Assert.Contains("其余 45 个失败", error.Message);
    }

    [Fact]
    public async Task SimulationControllerSendsNothingButStillValidatesTheMode()
    {
        var controller = new SimulationVideoWallController();
        Assert.False(controller.IsHardware);

        // 开发机不在视频墙网段：切换必须直接成功，不得尝试连接。
        await controller.DispatchAsync("double");
        var error = await Assert.ThrowsAsync<VideoWallException>(() => controller.DispatchAsync("triple"));
        Assert.Contains("未知的视频墙模式", error.Message);
    }

    [Fact]
    public async Task DispatchingTheSameModeTwiceSendsTheWholeSequenceTwice()
    {
        var transport = new RecordingTransport([]);
        var controller = new TcpVideoWallController(transport, FastOptions());

        await controller.DispatchAsync("single");
        await controller.DispatchAsync("single");

        // 重复下发已生效的模式是现场补救路径（切换成功但墙面没动时重来一次），控制器不得去重或短路。
        Assert.Equal(400, transport.Sends.Count);
        Assert.Equal(200, transport.AttemptsByPhase.Count);
        Assert.All(transport.AttemptsByPhase.Values, attempts => Assert.Equal(2, attempts));
    }

    [Fact]
    public async Task ConcurrentDispatchesAreSerializedInsteadOfInterleaving()
    {
        var timeline = new List<string>();
        var releaseMapping = new TaskCompletionSource();
        var transport = new InterleavingProbeTransport(timeline, releaseMapping.Task);
        var options = FastOptions() with
        {
            Wait = (delay, _) =>
            {
                timeline.Add($"wait:{delay.TotalMilliseconds:0}");
                return Task.CompletedTask;
            },
        };
        var controller = new TcpVideoWallController(transport, options);

        var first = controller.DispatchAsync("single");
        await transport.FirstMappingStarted;
        var second = controller.DispatchAsync("double");

        // 第一次下发正卡在映射阶段；串行化生效时第二次必须还在 _gate 上等，一个包都不能发出去。
        // 摘掉 _gate 后第二次的清屏包会在这里就混进来，两段序列交错到达节点。
        Assert.Equal(["send:clear", "wait:1", "send:mapping"], Collapse(timeline));

        releaseMapping.SetResult();
        await Task.WhenAll(first, second);

        Assert.Equal(
            [
                "send:clear", "wait:1", "send:mapping", "send:commit", "send:refresh",
                "send:clear", "wait:1", "send:mapping", "send:commit", "send:refresh",
            ],
            Collapse(timeline));
        Assert.Equal(400, transport.Sent);
    }

    [Fact]
    public void DefaultOptionsMatchLegacyVideoWallConstants()
    {
        var options = new VideoWallDispatchOptions();
        Assert.Equal(5, options.RetryAttempts);
        Assert.Equal(TimeSpan.FromMilliseconds(200), options.RetryBaseDelay);
        Assert.Equal(TimeSpan.FromSeconds(1), options.RetryMaxDelay);
        Assert.Equal(TimeSpan.FromMilliseconds(200), options.ClearToMappingDelay);
        Assert.Equal(25, options.MaxParallelSends);
        Assert.Equal(TimeSpan.FromSeconds(2), TcpVideoWallTransport.Timeout);
    }

    private static VideoWallDispatchOptions FastOptions() => new()
    {
        RetryBaseDelay = TimeSpan.FromMilliseconds(1),
        RetryMaxDelay = TimeSpan.FromMilliseconds(1),
        ClearToMappingDelay = TimeSpan.FromMilliseconds(1),
        Wait = static (_, _) => Task.CompletedTask,
    };

    private static string[] Collapse(List<string> timeline)
    {
        var collapsed = new List<string>();
        foreach (var entry in timeline)
        {
            if (collapsed.Count == 0 || collapsed[^1] != entry)
            {
                collapsed.Add(entry);
            }
        }

        return [.. collapsed];
    }

    /// <summary>
    /// 在第一次「映射」发送上卡住，用来把两次并发的下发真正重叠起来观察。位置选在映射而不是清屏：
    /// 若卡在首个清屏包，第二次下发即便没有串行化也只能看到清屏+等待，交错特征出不来。
    /// </summary>
    private sealed class InterleavingProbeTransport(List<string> timeline, Task releaseMapping) : IVideoWallTransport
    {
        private readonly Lock _gate = new();
        private readonly TaskCompletionSource _mappingReached = new();

        /// <summary>第一次下发进入映射阶段时完成。</summary>
        public Task FirstMappingStarted => _mappingReached.Task;

        public int Sent { get; private set; }

        public async Task SendAsync(string ip, int port, byte[] packet, CancellationToken cancellationToken)
        {
            var phase = packet[3] switch
            {
                0xFF => "clear",
                0x02 => "mapping",
                0xB8 => "commit",
                0xB9 => "refresh",
                _ => "unknown",
            };
            lock (_gate)
            {
                Sent++;
                timeline.Add($"send:{phase}");
            }

            if (phase == "mapping")
            {
                _mappingReached.TrySetResult();
                await releaseMapping.ConfigureAwait(false);
            }
        }
    }

    private sealed class RecordingTransport(List<string> timeline) : IVideoWallTransport
    {
        private readonly Lock _gate = new();

        public List<(string Ip, byte[] Packet)> Sends { get; } = [];

        /// <summary>按 (节点, 阶段) 统计尝试次数：同一节点在每个阶段各收一个包，重试需分别计数。</summary>
        public Dictionary<(string Ip, string Phase), int> AttemptsByPhase { get; } = [];

        /// <summary>前 N 次尝试返回失败，用于验证重试与放弃。</summary>
        public int FailuresBeforeSuccess { get; init; }

        public Task SendAsync(string ip, int port, byte[] packet, CancellationToken cancellationToken)
        {
            // 同阶段最多 25 个并发发送，记录必须串行化。
            lock (_gate)
            {
                var key = (ip, PhaseOf(packet));
                var attempt = AttemptsByPhase.GetValueOrDefault(key) + 1;
                AttemptsByPhase[key] = attempt;
                if (attempt <= FailuresBeforeSuccess)
                {
                    return Task.FromException(new VideoWallException($"模拟发送失败：{ip}:{port}"));
                }

                Sends.Add((ip, packet));
                timeline.Add($"send:{PhaseOf(packet)}");
                return Task.CompletedTask;
            }
        }

        /// <summary>按控制码（包内第 4 字节）判定该包属于哪个阶段，避免依赖构造顺序。</summary>
        private static string PhaseOf(byte[] packet) => packet[3] switch
        {
            0xFF => "clear",
            0x02 => "mapping",
            0xB8 => "commit",
            0xB9 => "refresh",
            _ => "unknown",
        };
    }
}
