// 两块大屏显示器的选择、恢复与对外投影。
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ScpCv.Contracts.Http;
using ScpCv.Domain.Model;

namespace ScpCv.Infrastructure.Playback;

public sealed partial class RuntimeStateService
{
    /// <summary>重启后只恢复两块经配置核验的大屏输出，不将旧电视目标重新下发。</summary>
    public async Task<int> ReapplyDisplayTargetsAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<(int WindowId, string Label)> targets;
        if (_bigScreenOutputs.HardwareBindingRequired)
        {
            _bigScreenOutputs.ValidateHardware();
            targets = Windows.Select(windowId => (windowId, _bigScreenOutputs.ForWindow(windowId))).ToArray();
        }
        else
        {
            await using var database = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
            targets = await database.PlaybackSessions.AsNoTracking()
                .Where(session => session.WindowId <= WindowId.Maximum && session.TargetDisplayLabel != string.Empty)
                .OrderBy(session => session.WindowId)
                .Select(session => new ValueTuple<int, string>(session.WindowId, session.TargetDisplayLabel))
                .ToListAsync(cancellationToken).ConfigureAwait(false);
        }

        var applied = 0;
        foreach (var (windowId, label) in targets)
        {
            try
            {
                await SelectDisplayAsync(windowId, "single", label, cancellationToken).ConfigureAwait(false);
                applied++;
            }
            catch (PlaybackServiceException) when (!_bigScreenOutputs.HardwareBindingRequired)
            {
                // Simulation 的旧目标失效时保留记录，交由操作者重新选择。
            }
        }

        return applied;
    }

    public Task<IReadOnlyList<PlaybackSessionDto>> SelectDisplayAsync(
        int windowId,
        string displayMode,
        string targetLabel,
        CancellationToken cancellationToken = default)
    {
        ValidateWindow(windowId);
        if (!string.Equals(displayMode, "single", StringComparison.OrdinalIgnoreCase))
            throw new PlaybackServiceException($"无效的显示模式：{displayMode}");

        var topology = _displayTopology.GetCurrent();
        if (!topology.Available)
            throw new PlaybackServiceException(topology.Detail, "display_topology_unavailable");
        if (!_bigScreenOutputs.IsAllowed(windowId, targetLabel) ||
            !topology.Targets.Any(target => string.Equals(target.Name, targetLabel, StringComparison.OrdinalIgnoreCase)))
            throw new PlaybackServiceException($"显示器目标不可用：{targetLabel}", "display_target_unavailable");

        return EnqueueDisplayAsync(
            windowId,
            "SELECT_DISPLAY",
            JsonSerializer.Serialize(new { display_mode = "single", target_label = targetLabel }),
            (session, command) =>
            {
                session.DisplayMode = DisplayMode.Single;
                session.TargetDisplayLabel = targetLabel;
                session.PendingCommand = command.Command;
            },
            cancellationToken);
    }

    public IReadOnlyList<DisplayTargetDto> ListDisplays()
    {
        var topology = _displayTopology.GetCurrent();
        if (!topology.Available)
            throw new PlaybackServiceException(topology.Detail, "display_topology_unavailable");
        return topology.Targets.Select(target =>
        {
            var role = _bigScreenOutputs.RoleFor(target.Name);
            return target with
            {
                PlaybackRole = role,
                IsPlaybackTarget = !_bigScreenOutputs.HardwareBindingRequired || role.Length > 0,
            };
        }).ToArray();
    }
}
