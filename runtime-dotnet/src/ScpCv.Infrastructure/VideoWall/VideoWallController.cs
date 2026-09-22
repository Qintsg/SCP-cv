using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace ScpCv.Infrastructure.VideoWall;

/// <summary>视频墙控制过程中的业务异常。</summary>
public sealed class VideoWallException : Exception
{
    public VideoWallException(string message)
        : base(message)
    {
    }

    public VideoWallException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>
/// 视频墙控制包下发通道。真实实现只在 SafetyMode=Hardware 时注册，与显示器拓扑、系统音频的
/// 既有 SafetyMode 分工保持一致。
/// </summary>
public interface IVideoWallController
{
    /// <summary>是否访问真实视频墙网络。</summary>
    bool IsHardware { get; }

    /// <summary>按运行态大屏模式（<c>single</c> / <c>double</c>）下发完整控制序列。</summary>
    /// <exception cref="VideoWallException">模式无效，或任一节点在重试后仍未收到控制包时。</exception>
    Task DispatchAsync(string bigScreenMode, CancellationToken cancellationToken = default);
}

/// <summary>
/// Simulation 模式的空实现。开发机不在视频墙网段（192.168.5.0/24），真实下发会让每次切换都等满
/// 50 个节点各自的连接超时后整体失败，所以这里保持“切换成功但不发网络包”。
/// 仍然构造下发序列，使非法模式在 Simulation 下立即报错，而不是留到现场才暴露。
/// </summary>
public sealed class SimulationVideoWallController(ILogger<SimulationVideoWallController>? logger = null)
    : IVideoWallController
{
    private readonly ILogger _logger = (ILogger?)logger ?? NullLogger.Instance;

    public bool IsHardware => false;

    public Task DispatchAsync(string bigScreenMode, CancellationToken cancellationToken = default)
    {
        var sequence = VideoWallSequenceBuilder.Build(bigScreenMode);
        VideoWallLog.DispatchSkipped(_logger, bigScreenMode, sequence.Count);
        return Task.CompletedTask;
    }
}

/// <summary>视频墙下发的节流与重试参数，默认值与清理前提交 <c>e822be9</c> 的旧实现一致。</summary>
public sealed record VideoWallDispatchOptions
{
    /// <summary>单个控制包的最大尝试次数。</summary>
    public int RetryAttempts { get; init; } = 5;

    /// <summary>首次重试前的退避时间，按 2 的幂增长。</summary>
    public TimeSpan RetryBaseDelay { get; init; } = TimeSpan.FromMilliseconds(200);

    /// <summary>退避时间的上限。</summary>
    public TimeSpan RetryMaxDelay { get; init; } = TimeSpan.FromSeconds(1);

    /// <summary>清屏与映射之间的等待：设备端需要时间清完，否则映射会被旧画面覆盖。</summary>
    public TimeSpan ClearToMappingDelay { get; init; } = TimeSpan.FromMilliseconds(200);

    /// <summary>单个阶段内并发的连接数上限。</summary>
    public int MaxParallelSends { get; init; } = 25;

    /// <summary>等待实现。回归测试替换为瞬时返回，避免真的 sleep。</summary>
    public Func<TimeSpan, CancellationToken, Task> Wait { get; init; } =
        static (delay, token) => Task.Delay(delay, token);
}

/// <summary>单个视频墙控制包的发送通道。</summary>
public interface IVideoWallTransport
{
    /// <summary>向指定节点发送单个控制包。</summary>
    /// <exception cref="VideoWallException">连接或发送失败时。</exception>
    Task SendAsync(string ip, int port, byte[] packet, CancellationToken cancellationToken);
}

/// <summary>
/// 真实 TCP 发送。与 Python 版一致：2 秒超时，并开启 TCP_NODELAY——控制帧只有 11~45 字节，
/// 不关 Nagle 会等不到后续数据才发出，设备端迟迟收不到。
/// </summary>
public sealed class TcpVideoWallTransport : IVideoWallTransport
{
    /// <summary>单次连接与发送的超时。</summary>
    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(2);

    public async Task SendAsync(string ip, int port, byte[] packet, CancellationToken cancellationToken)
    {
        using var client = new TcpClient();
        try
        {
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            deadline.CancelAfter(Timeout);
            await client.ConnectAsync(IPAddress.Parse(ip), port, deadline.Token).ConfigureAwait(false);
            client.NoDelay = true;
            using var stream = client.GetStream();
            await stream.WriteAsync(packet, deadline.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (exception is SocketException or IOException or OperationCanceledException or FormatException)
        {
            throw new VideoWallException($"发送视频墙控制包失败：{ip}:{port} {exception.Message}", exception);
        }
    }
}

/// <summary>
/// 视频墙下发：清屏 → 映射 → 提交 → 刷新，阶段内并行、阶段间串行，顺序、并行上限与重试退避
/// 均与清理前提交 <c>e822be9</c> 的旧实现一致。整个序列串行化，避免两次切换的控制包交错到达节点。
/// </summary>
public sealed class TcpVideoWallController : IVideoWallController, IDisposable
{
    private const string MappingPhase = "mapping";

    private readonly IVideoWallTransport _transport;
    private readonly VideoWallDispatchOptions _options;
    private readonly ILogger _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public TcpVideoWallController(
        IVideoWallTransport transport,
        VideoWallDispatchOptions? options = null,
        ILogger<TcpVideoWallController>? logger = null)
    {
        _transport = transport;
        _options = options ?? new VideoWallDispatchOptions();
        // 下发日志是现场唯一的“墙面到底动没动”线索（节点不回读状态），缺省实现保证直接 new 的
        // 调用方与测试不必构造日志设施。
        _logger = (ILogger?)logger ?? NullLogger.Instance;
    }

    public bool IsHardware => true;

    public void Dispose() => _gate.Dispose();

    public async Task DispatchAsync(string bigScreenMode, CancellationToken cancellationToken = default)
    {
        var sequence = VideoWallSequenceBuilder.Build(bigScreenMode);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var failure = await SendSequenceAsync(sequence, cancellationToken).ConfigureAwait(false);
            if (failure is { } failed)
            {
                // 取消（OperationCanceledException）不会走到这里，FR-017：它既不算节点失败，也不记成功。
                VideoWallLog.DispatchFailed(
                    _logger,
                    bigScreenMode,
                    failed.Phase,
                    failed.Failures.Count,
                    failed.Failures[0]);
                throw new VideoWallException($"发送视频墙控制包失败：{FormatFailures(failed.Failures)}");
            }

            VideoWallLog.DispatchSucceeded(_logger, bigScreenMode, sequence.Count);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>整段下发；全部成功返回 <c>null</c>，否则返回中止时的阶段与该阶段的失败描述。</summary>
    private async Task<PhaseFailure?> SendSequenceAsync(
        IReadOnlyList<VideoWallSendItem> sequence,
        CancellationToken cancellationToken)
    {
        var (order, groups) = GroupByPhase(sequence);
        foreach (var phase in order)
        {
            if (phase == MappingPhase)
            {
                await _options.Wait(_options.ClearToMappingDelay, cancellationToken).ConfigureAwait(false);
            }

            var phaseFailures = await SendPhaseAsync(groups[phase], cancellationToken).ConfigureAwait(false);
            if (phaseFailures.Count > 0)
            {
                // 阶段内有节点没收到就到此为止：提交一份残缺映射比不切换更糟。
                return new PhaseFailure(phase, phaseFailures);
            }
        }

        return null;
    }

    /// <summary>中止时的阶段与失败节点描述，供错误信息与日志共用（FR-007、FR-018）。</summary>
    private sealed record PhaseFailure(string Phase, List<string> Failures);

    private async Task<List<string>> SendPhaseAsync(
        List<VideoWallSendItem> items,
        CancellationToken cancellationToken)
    {
        var parallelLimit = Math.Max(1, Math.Min(_options.MaxParallelSends, items.Count));
        using var throttle = new SemaphoreSlim(parallelLimit, parallelLimit);
        var sends = items
            .Select(item => SendWithRetryAsync(item, throttle, cancellationToken))
            .ToArray();
        var results = await Task.WhenAll(sends).ConfigureAwait(false);
        return results.Where(result => result is not null).Select(result => result!).ToList();
    }

    private async Task<string?> SendWithRetryAsync(
        VideoWallSendItem item,
        SemaphoreSlim throttle,
        CancellationToken cancellationToken)
    {
        await throttle.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            for (var attempt = 1; ; attempt++)
            {
                try
                {
                    await _transport.SendAsync(item.Ip, item.Port, item.Packet, cancellationToken).ConfigureAwait(false);
                    if (attempt > 1)
                    {
                        // FR-006：没有这条记录，现场只看到切换变慢，不知道有节点在掉线。
                        VideoWallLog.RetrySucceeded(
                            _logger,
                            item.Ip,
                            item.Port,
                            item.Phase,
                            attempt,
                            _options.RetryAttempts);
                    }

                    return null;
                }
                catch (VideoWallException exception)
                {
                    if (attempt >= _options.RetryAttempts)
                    {
                        return $"{item.Ip}:{item.Port} phase={item.Phase} error={exception.Message}";
                    }

                    // 设备端 accept 队列忙碌时立刻重撞只会继续失败，退避后再试。
                    await _options.Wait(RetryDelay(attempt), cancellationToken).ConfigureAwait(false);
                }
            }
        }
        finally
        {
            throttle.Release();
        }
    }

    /// <summary>第 <paramref name="failedAttempt"/> 次尝试失败后的退避时间：base × 2^(n-1)，上限 max。</summary>
    private TimeSpan RetryDelay(int failedAttempt)
    {
        var exponent = Math.Min(failedAttempt - 1, 30);
        var scaled = _options.RetryBaseDelay.Ticks * (1L << exponent);
        return TimeSpan.FromTicks(Math.Min(scaled, _options.RetryMaxDelay.Ticks));
    }

    private static (List<string> Order, Dictionary<string, List<VideoWallSendItem>> Groups) GroupByPhase(
        IReadOnlyList<VideoWallSendItem> sequence)
    {
        var order = new List<string>();
        var groups = new Dictionary<string, List<VideoWallSendItem>>(StringComparer.Ordinal);
        foreach (var item in sequence)
        {
            var phase = item.Phase.StartsWith("mapping_", StringComparison.Ordinal) ? MappingPhase : item.Phase;
            if (!groups.TryGetValue(phase, out var items))
            {
                items = [];
                groups[phase] = items;
                order.Add(phase);
            }

            items.Add(item);
        }

        return (order, groups);
    }

    private static string FormatFailures(List<string> failures)
    {
        var preview = string.Join("; ", failures.Take(5));
        return failures.Count > 5 ? $"{preview}; 其余 {failures.Count - 5} 个失败" : preview;
    }
}
