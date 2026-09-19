using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using ScpCv.Infrastructure.VideoWall;

namespace ScpCv.Infrastructure.Tests;

/// <summary>
/// 视频墙下发的日志要求（FR-018，以及 FR-006 的「重试成功必须留痕」）。节点只接受写入、不回读状态，
/// 所以日志是现场判断「墙面到底动没动」的唯一线索——只写在异常里等于没写。
/// </summary>
public sealed class VideoWallLoggingTests
{
    [Fact]
    public async Task SuccessfulDispatchLogsModeAndPacketCount()
    {
        var logger = new RecordingLogger<TcpVideoWallController>();
        var controller = new TcpVideoWallController(new StubTransport(), FastOptions(), logger);

        await controller.DispatchAsync("single");

        var message = Assert.Single(logger.MessagesAt(LogLevel.Information));
        Assert.Contains("模式 single", message, StringComparison.Ordinal);
        Assert.Contains("控制包 200 个", message, StringComparison.Ordinal);
        Assert.Empty(logger.MessagesAt(LogLevel.Error));
    }

    [Fact]
    public async Task FailedDispatchLogsThePhaseNodeCountAndReason()
    {
        var logger = new RecordingLogger<TcpVideoWallController>();
        var transport = new StubTransport { FailuresBeforeSuccess = int.MaxValue };
        var controller = new TcpVideoWallController(transport, FastOptions(), logger);

        await Assert.ThrowsAsync<VideoWallException>(() => controller.DispatchAsync("double"));

        var message = Assert.Single(logger.MessagesAt(LogLevel.Error));
        Assert.Contains("模式 double", message, StringComparison.Ordinal);
        Assert.Contains("中止于阶段 clear", message, StringComparison.Ordinal);
        Assert.Contains("失败节点 50 个", message, StringComparison.Ordinal);
        Assert.Contains("192.168.5.101:4830", message, StringComparison.Ordinal);
        // 失败时不得留下「下发完成」，否则现场会以为墙面已经跟着切了。
        Assert.DoesNotContain(logger.Messages, entry => entry.Contains("下发完成", StringComparison.Ordinal));
    }

    [Fact]
    public async Task RetryThatSucceedsIsLoggedWithTheAttemptCount()
    {
        var logger = new RecordingLogger<TcpVideoWallController>();
        var transport = new StubTransport { FailuresBeforeSuccess = 2 };
        var controller = new TcpVideoWallController(transport, FastOptions(), logger);

        await controller.DispatchAsync("single");

        // 200 个包每个都失败两次后成功：现场要能看出「切换变慢是因为节点在掉线」。
        var retries = logger.MessagesAt(LogLevel.Information)
            .Where(entry => entry.Contains("重试后成功", StringComparison.Ordinal))
            .ToArray();
        Assert.Equal(200, retries.Length);
        Assert.All(retries, entry => Assert.Contains("第 3/5 次尝试", entry, StringComparison.Ordinal));
    }

    [Fact]
    public async Task CancelledDispatchIsLoggedAsNeitherFailureNorSuccess()
    {
        using var cancellation = new CancellationTokenSource();
        var logger = new RecordingLogger<TcpVideoWallController>();
        var controller = new TcpVideoWallController(
            new CancellingTransport(cancellation),
            FastOptions(),
            logger);

        // FR-017：调用方取消不是节点失败，也不能对外声称成功。
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => controller.DispatchAsync("single", cancellation.Token));
        Assert.Empty(logger.Entries);
    }

    [Fact]
    public async Task SimulationLogsTheSkipInsteadOfASuccessfulDispatch()
    {
        var logger = new RecordingLogger<SimulationVideoWallController>();
        var controller = new SimulationVideoWallController(logger);

        await controller.DispatchAsync("double");

        // Simulation 下切换在开发机上「成功」，但墙上不会动；日志必须说清楚这一点。
        var message = Assert.Single(logger.Entries).Message;
        Assert.Contains("模式 double", message, StringComparison.Ordinal);
        Assert.Contains("本应下发 200 个控制包", message, StringComparison.Ordinal);
        Assert.DoesNotContain(logger.Messages, entry => entry.Contains("下发完成", StringComparison.Ordinal));
    }

    private static VideoWallDispatchOptions FastOptions() => new()
    {
        RetryBaseDelay = TimeSpan.FromMilliseconds(1),
        RetryMaxDelay = TimeSpan.FromMilliseconds(1),
        ClearToMappingDelay = TimeSpan.FromMilliseconds(1),
        Wait = static (_, _) => Task.CompletedTask,
    };

    /// <summary>
    /// 可编排失败的节点集合。按 (节点, 包类型) 分别计数：同一节点在四个阶段各收一个包，
    /// 重试是逐包重试的，用全局计数会把「第几个包」错当成「第几次尝试」。
    /// </summary>
    private sealed class StubTransport : IVideoWallTransport
    {
        private readonly ConcurrentDictionary<(string Ip, byte Code), int> _attempts = new();

        /// <summary>每个包在前 N 次尝试返回失败；<see cref="int.MaxValue"/> 表示始终不可达。</summary>
        public int FailuresBeforeSuccess { get; init; }

        public Task SendAsync(string ip, int port, byte[] packet, CancellationToken cancellationToken)
        {
            var attempt = _attempts.AddOrUpdate((ip, packet[3]), 1, (_, current) => current + 1);
            return attempt <= FailuresBeforeSuccess
                ? Task.FromException(new VideoWallException($"模拟发送失败：{ip}:{port}"))
                : Task.CompletedTask;
        }
    }

    /// <summary>发送时取消调用方令牌，模拟操作员中途放弃切换。</summary>
    private sealed class CancellingTransport(CancellationTokenSource cancellation) : IVideoWallTransport
    {
        public Task SendAsync(string ip, int port, byte[] packet, CancellationToken cancellationToken)
        {
            cancellation.Cancel();
            return Task.FromException(new OperationCanceledException(cancellation.Token));
        }
    }
}
