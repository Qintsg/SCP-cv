// 显式运行组操作的可验证接口；调用方必须区分退出证明与仅启动受理。
namespace ScpCv.ControlHost.Runtime;

/// <summary>真实进程控制与合同测试使用相同接口，不替代原生退出证据。</summary>
public interface IRuntimeSupervisorControl
{
    bool IsConfigured { get; }

    /// <summary>:param action: start/stop/status。:param cancellationToken: 请求寿命。:returns: 实际结果。</summary>
    Task<SupervisorLaunchResult> LaunchAsync(string action, CancellationToken cancellationToken = default);

    /// <summary>:param action: 启动或控制动作。:param expectedGroupEpoch: 明确组代次。:param cancellationToken: 请求寿命。:returns: 实际结果。</summary>
    Task<SupervisorLaunchResult> LaunchAsync(string action, long? expectedGroupEpoch, CancellationToken cancellationToken);
}
