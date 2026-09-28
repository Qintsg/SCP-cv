// 全部运行角色的 immutable instance/group 身份；登记即绑定，不能借延迟握手或重连换组。
using System.Collections.Concurrent;
using System.Text.Json;
using ScpCv.Contracts.Ipc;
using ScpCv.Domain.Model;

namespace ScpCv.ControlHost.Ipc;

public sealed partial class RuntimePipeBroker
{
    private readonly ConcurrentDictionary<Guid, long> _runtimeInstanceEpochs = new();

    /// <summary>
    /// 所有角色在获得组权限前校验实例归属，早于 ownership 恢复及旧连接替换。
    /// :param identity: 已经 OS PID/start/session/role 验证的实例。
    /// :param groupEpoch: 当前组代次。
    /// :param state: 当前组状态。
    /// :raises UnauthorizedAccessException: 实例已经归属其它运行组。
    /// </summary>
    private void BindRuntimeEpoch(RegisteredProcessIdentity identity, long groupEpoch, RuntimeGroupState state)
    {
        if (state is not (RuntimeGroupState.Starting or RuntimeGroupState.Armed)) return;
        if (!TryBindRuntimeEpoch(identity.InstanceId, groupEpoch))
            throw new UnauthorizedAccessException("运行实例已绑定旧 group epoch，拒绝跨代次重连。");
    }

    /// <summary>同组重复绑定幂等；实例在 broker 寿命内的原组代次不可改写。</summary>
    private bool TryBindRuntimeEpoch(Guid instanceId, long groupEpoch) =>
        _runtimeInstanceEpochs.GetOrAdd(instanceId, groupEpoch) == groupEpoch;

    /// <summary>
    /// 仅由已通过当前组门禁的 Supervisor 调用；先绑定再登记，覆盖子进程延迟首次 hello。
    /// :param frame: 子进程登记请求。
    /// :param groupEpoch: 已认证 Supervisor 所属组代次。
    /// :returns: 明确接受或拒绝结果，拒绝时不替换 registry 中的已有身份。
    /// </summary>
    private IpcFrameDto RegisterChild(IpcFrameDto frame, long groupEpoch)
    {
        var request = frame.Payload.Deserialize<RegisterProcessDto>() ?? new RegisterProcessDto();
        if (!TryValidateChild(request, out var identity, out var reason))
            return Response(frame, "registration_result", new RegistrationResultDto { Accepted = false, Reason = reason });
        if (!TryBindRuntimeEpoch(identity!.InstanceId, groupEpoch))
            return Response(frame, "registration_result", new RegistrationResultDto { Accepted = false, Reason = "group_fenced" });
        processRegistry.Register(identity);
        return Response(frame, "registration_result", new RegistrationResultDto { Accepted = true, Reason = "registered" });
    }
}
