// 预案输出快照及锁内TV派发；不改变原音量/媒体命令与失败后的中止规则。
using System.Text.Json;
using ScpCv.Contracts.Http;
using ScpCv.Domain.Model;
using ScpCv.Domain.Rules;
using ScpCv.Infrastructure.DisplayRouting;
using ScpCv.Infrastructure.VideoWall;

namespace ScpCv.Infrastructure.Scenarios;

public sealed partial class ScenarioService
{
    /// <summary>:param advanced: 全部已预检的高级配置。:param token: 取消。:remarks: 调用方持有同一输出门禁，每台成功独立记账。</summary>
    private async Task ApplyTelevisionsLockedAsync(ScenarioAdvancedDto advanced, CancellationToken token)
    {
        foreach (var (target, input) in ScenarioTelevisionPolicy.Changes(advanced.Televisions))
        {
            if (displayRouting is null) throw new ScenarioServiceException("电视路由服务未装配。", code: "scenario_advanced_unavailable");
            try { await displayRouting.ApplyLockedAsync(target, input, false, token).ConfigureAwait(false); }
            catch (DisplayRoutingException exception)
            { throw new ScenarioServiceException("预案电视阶段未完整确认：" + exception.Message, code: "scenario_display_routing_uncertain"); }
        }
    }

    /// <summary>:param state: 当前持久快照。:param previous: 保留其它高级项。:returns: 只把可信最后下发意图作为可编辑预案内容。</summary>
    private static string CaptureOutputRouting(RuntimeState state, ScenarioAdvancedDto previous)
    {
        ScenarioTelevisionDto Capture(string target)
        {
            var record = DisplayRoutingState.Read(state, target);
            return record?.Status is "sent_unconfirmed" or "simulated" && record.LastSent?.Input is { } input
                ? new() { State = "set", Input = input } : new();
        }
        var applied = state.WallLayoutDispatchStatus is "sent_unconfirmed" or "simulated" ? VideoWallStateProjection.ReadApplied(state) : null;
        return ValidateAdvanced(previous with
        {
            WallLayoutCode = applied is null ? null : WallLayoutCode.Encode(applied),
            Televisions = new() { TvLeft = Capture("tv_left"), TvRight = Capture("tv_right") },
        });
    }
}
