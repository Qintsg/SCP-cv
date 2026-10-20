// 预案墙面与系统音量先下发、运行态原子提交及部分失败报告。
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using ScpCv.Domain.Model;
using ScpCv.Domain.Rules;
using ScpCv.Infrastructure.Playback;
using ScpCv.Infrastructure.VideoWall;

namespace ScpCv.Infrastructure.Scenarios;

public sealed partial class ScenarioService
{
    /// <summary>
    /// 按墙面、音量、持久化顺序执行；外部副作用不占数据库写锁。
    /// :param scenario: 已通过目标与文稿校验的预案。
    /// :param cancellationToken: 激活请求的取消信号。
    /// :raises ScenarioServiceException: 外部动作失败或运行态提交未确认。
    /// </summary>
    private async Task ApplyRuntimeAsync(Scenario scenario, CancellationToken cancellationToken)
    {
        using var operation = await _wallGate.EnterAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await ApplyRuntimeCoreAsync(scenario, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception) when (HasWallChange(scenario))
        {
            try
            {
                await writes.ExecuteAsync(async (database, token) =>
                {
                    var state = await database.RuntimeStates.SingleAsync(token).ConfigureAwait(false);
                    state.WallLayoutDispatchStatus = "uncertain";
                    state.WallLayoutDispatchDetail = "预案中墙面动作未完整确认；保留上次布局记录，请核对实体画面，不要自动重放。";
                    state.UpdatedAt = _timeProvider.GetUtcNow();
                }, CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is DbUpdateException or DbException or InvalidOperationException)
            {
                // 库不可写时保留原始部分失败异常；状态标记不构成设备回滚。
            }
            throw;
        }
    }

    /// <summary>墙面与声卡副作用在数据库提交之前执行，失败禁止派发媒体命令。</summary>
    private async Task ApplyRuntimeCoreAsync(Scenario scenario, CancellationToken cancellationToken)
    {
        var code = ReadAdvanced(scenario.AdvancedJson).WallLayoutCode;
        var layout = !string.IsNullOrEmpty(code) ? WallLayoutCode.Decode(code) :
            scenario.BigScreenModeState == ScenarioValueState.Set ? VideoWallLayoutPlanner.Preset(EnumName(scenario.BigScreenMode)) : null;
        if (layout is not null)
        {
            // 保留旧预案语义：墙面失败即中止，网络重试不能持有数据库写锁。
            try
            {
                if (!string.IsNullOrEmpty(code)) await _videoWall.DispatchLayoutAsync(layout, cancellationToken).ConfigureAwait(false);
                else await _videoWall.DispatchAsync(EnumName(scenario.BigScreenMode), cancellationToken).ConfigureAwait(false);
            }
            catch (VideoWallException exception)
            {
                throw new ScenarioServiceException(exception.Message);
            }
        }

        cancellationToken.ThrowIfCancellationRequested();
        SystemAudioSnapshot? appliedVolume = null;
        if (scenario.VolumeState == ScenarioValueState.Set && _systemAudio.IsHardware)
        {
            // 与系统音量入口共用控制器；null 静音意图保留声卡当前静音，落库只用真实读回值。
            appliedVolume = _systemAudio.Apply(scenario.VolumeLevel, null);
            if (!appliedVolume.Available)
                throw new ScenarioServiceException(
                    (layout is not null ? "墙面已下发；运行状态未提交、媒体命令未派发。" : string.Empty)
                    + appliedVolume.Detail,
                    code: "system_audio_unavailable");
        }

        try
        {
            await writes.ExecuteAsync(async (database, token) =>
            {
                var runtime = await database.RuntimeStates.SingleAsync(token).ConfigureAwait(false);
                var sessions = await database.PlaybackSessions
                    .Where(item => item.WindowId <= WindowId.Maximum).ToListAsync(token).ConfigureAwait(false);
                var runtimeChanged = false;
                if (layout is not null)
                {
                    VideoWallStateProjection.Record(runtime,
                        layout, 0, _videoWall.IsHardware);
                    var mutedWindows = VideoWallStateProjection.MutedWindows(layout).ToHashSet();
                    foreach (var session in sessions) session.IsMuted = mutedWindows.Contains(session.WindowId);
                    runtimeChanged = true;
                }
                if (scenario.VolumeState == ScenarioValueState.Set)
                {
                    runtime.VolumeLevel = appliedVolume?.Level ?? scenario.VolumeLevel;
                    if (appliedVolume is not null) runtime.VolumeMuted = appliedVolume.Muted;
                    runtimeChanged = true;
                }
                if (runtimeChanged) runtime.UpdatedAt = _timeProvider.GetUtcNow();
            }, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is DbUpdateException or DbException or InvalidOperationException)
        {
            // 提交回执异常不证明事务未提交；不擅自补偿声卡或墙面，也不继续派发媒体命令。
            var completed = (layout is not null && _videoWall.IsHardware ? "墙面已下发；" : string.Empty)
                + (appliedVolume is not null ? "系统音量已下发；" : string.Empty);
            throw new ScenarioServiceException(
                $"{completed}运行状态提交未确认，媒体命令未派发，请核对实际状态。{exception.Message}",
                code: "scenario_state_persistence_failed");
        }
    }

    private static bool HasWallChange(Scenario scenario) => scenario.BigScreenModeState == ScenarioValueState.Set ||
        !string.IsNullOrEmpty(ReadAdvanced(scenario.AdvancedJson).WallLayoutCode);
}
