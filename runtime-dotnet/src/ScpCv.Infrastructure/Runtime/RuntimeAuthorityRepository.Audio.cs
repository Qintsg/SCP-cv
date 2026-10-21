// 当前音频实例的空资源观测代次。项目：SCP-cv；文件：RuntimeAuthorityRepository.Audio.cs；作者：Qintsg；日期：2026-10-21。
using Microsoft.EntityFrameworkCore;
using ScpCv.Domain.Model;

namespace ScpCv.Infrastructure.Runtime;

public sealed partial class RuntimeAuthorityRepository
{
    /// <summary>:param groupEpoch: 当前握手组代次。:param cancellationToken: 请求寿命。:returns: 没有来源或在途命令时的当前音频代次。</summary>
    public async Task<long?> GetInitialIdleAudioGenerationAsync(long groupEpoch, CancellationToken cancellationToken)
    {
        await using var database = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var group = await database.RuntimeGroupControls.AsNoTracking().SingleAsync(cancellationToken).ConfigureAwait(false);
        if (group.GroupEpoch != groupEpoch || group.State is not (RuntimeGroupState.Starting or RuntimeGroupState.Armed)) return null;
        var state = await database.BackgroundAudioStates.AsNoTracking().SingleAsync(cancellationToken).ConfigureAwait(false);
        if (state.CurrentSourceId is not null || state.PendingCommand.Length != 0) return null;
        var pending = await database.CommandRecords.AnyAsync(item => item.TargetKind == CommandTargetKind.Audio && item.TargetId == 1 &&
            (item.Status == CommandStatus.Pending || item.Status == CommandStatus.Processing), cancellationToken).ConfigureAwait(false);
        return pending ? null : state.DesiredGeneration;
    }
}
