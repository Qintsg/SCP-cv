// Supervisor 原生退出的原子故障闩锁；只撤销当前组权限，不伪称其它进程已停止。
using Microsoft.EntityFrameworkCore;
using ScpCv.Domain.Model;

namespace ScpCv.Infrastructure.Runtime;

public sealed partial class RuntimeAuthorityRepository
{
    /// <summary>仅当前已授权代次接受确证的 Supervisor 退出；保留旧命令和源供诊断。</summary>
    /// <remarks>:param expectedGroupEpoch: 观察时捕获的组代次。:param processId: 原进程 PID。:param instanceId: 已认证实例。:param cancellationToken: 宿主寿命。:returns: 是否提交了新的故障状态。</remarks>
    public Task<bool> TryFaultSupervisorExitAsync(long expectedGroupEpoch, int processId, Guid instanceId,
        CancellationToken cancellationToken = default) => writes.ExecuteAsync(async (database, token) =>
        {
            var group = await database.RuntimeGroupControls.SingleAsync(token).ConfigureAwait(false);
            var reason = $"supervisor_exited:{processId}:{instanceId:N}";
            // 提交后回执也可能失败：同一死亡证据已持久化时继续发布 SSE，不重复改变会话。
            if (group.GroupEpoch == expectedGroupEpoch && group.State == RuntimeGroupState.Faulted && group.StopReason == reason)
                return true;
            if (group.GroupEpoch != expectedGroupEpoch || group.State is not (RuntimeGroupState.Starting or RuntimeGroupState.Armed))
                return false;

            group.State = RuntimeGroupState.Faulted;
            group.StopReason = reason;
            var ownerships = await database.WorkerOwnerships.Where(owner =>
                (owner.TargetKind == CommandTargetKind.Display && owner.TargetId >= 1 && owner.TargetId <= 2) ||
                (owner.TargetKind == CommandTargetKind.Audio && owner.TargetId == 1)).ToListAsync(token).ConfigureAwait(false);
            foreach (var ownership in ownerships) ownership.Status = WorkerOwnershipState.Faulted;

            const string detail = "运行组 Supervisor 已退出，播出状态未确认；请检查残余进程后显式重启。";
            var now = _timeProvider.GetUtcNow();
            var sessions = await database.PlaybackSessions.Where(session => session.WindowId >= 1 && session.WindowId <= 2)
                .ToListAsync(token).ConfigureAwait(false);
            foreach (var session in sessions)
            {
                session.PlaybackState = PlaybackState.Error;
                session.ErrorMessage = detail;
                session.PlayerLastSeenAt = null;
                session.LastUpdatedAt = now > session.LastUpdatedAt ? now : session.LastUpdatedAt.AddMilliseconds(1);
            }
            var audio = await database.BackgroundAudioStates.SingleAsync(token).ConfigureAwait(false);
            audio.PlaybackState = PlaybackState.Error;
            audio.ErrorMessage = detail;
            audio.UpdatedAt = now > audio.UpdatedAt ? now : audio.UpdatedAt.AddMilliseconds(1);
            // 没有整组原生退出证明，不能 CompleteStop 或重新发放旧 Processing 租约。
            return true;
        }, cancellationToken);
}
