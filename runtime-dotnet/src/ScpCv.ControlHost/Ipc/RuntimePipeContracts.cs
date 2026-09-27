// 运行组管道的就绪、协作退出和持久命令唤醒边界。
using ScpCv.Infrastructure.Commands;

namespace ScpCv.ControlHost.Ipc;

public interface IRuntimeReadinessGate
{
    Task<RuntimeReadinessResult> WaitForRuntimeReadyAsync(
        long groupEpoch,
        TimeSpan timeout,
        CancellationToken cancellationToken = default);
}

/// <summary>只向本 ControlHost 已认证的受管 Worker 发送协作退出通知。</summary>
public interface IRuntimeShutdownNotifier
{
    Task NotifyRuntimeShutdownAsync(string reason, CancellationToken cancellationToken = default);
}

/// <summary>延迟解析 broker，避免音频完成处理与命令协调器的单例构造环。</summary>
public sealed class RuntimeCommandWakeNotifier(IServiceProvider services) : ICommandWakeNotifier
{
    public ValueTask WakeAsync(CommandWakeSignal signal, CancellationToken cancellationToken = default) =>
        services.GetRequiredService<RuntimePipeBroker>().WakeAsync(signal, cancellationToken);
}
