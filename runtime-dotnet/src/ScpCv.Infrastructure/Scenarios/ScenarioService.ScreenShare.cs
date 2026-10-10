// 临时共享不能被预案捕获成空窗口；在同一写事务内预检，失败不新增或覆盖预案。
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ScpCv.Domain.Model;
using ScpCv.Infrastructure.Persistence;

namespace ScpCv.Infrastructure.Scenarios;

public sealed partial class ScenarioService
{
    /// <summary>:param database: 捕获预案的同一事务。:param sessions: 当前两窗意图与实际。:param token: 请求寿命。:raises ScenarioServiceException: 临时共享尚为意图或实际资源。:returns: 仅固定媒体/空闲状态允许持久化。</summary>
    private static async Task RejectTransientCaptureAsync(ControlDbContext database, IReadOnlyList<PlaybackSession> sessions, CancellationToken token)
    {
        foreach (var session in sessions)
        {
            var transient = session.ActualAdapterKind == "screen_share";
            if (!transient && session.MediaSourceId is null && session.DesiredGeneration > 0)
            {
                var args = await database.CommandRecords.AsNoTracking().Where(item => item.TargetKind == CommandTargetKind.Display &&
                    item.TargetId == session.WindowId && item.Command == "OPEN" && item.SourceGeneration == session.DesiredGeneration)
                    .OrderByDescending(item => item.TargetSequence).Select(item => item.ArgsJson).FirstOrDefaultAsync(token).ConfigureAwait(false);
                if (args is not null)
                {
                    try
                    {
                        using var json = JsonDocument.Parse(args);
                        transient = json.RootElement.ValueKind == JsonValueKind.Object && json.RootElement.TryGetProperty("source_type", out var kind) &&
                            kind.ValueKind == JsonValueKind.String && kind.GetString() == "screen_share";
                    }
                    catch (JsonException) { /* 畸形历史审计不恢复共享；实际适配器仍独立判断。 */ }
                }
            }
            if (transient)
                throw new ScenarioServiceException($"窗口{session.WindowId}仍有临时屏幕共享，不能保存为预案。请先停止共享或切换到媒体库中的固定源。",
                    code: "scenario_screen_share_not_persistable");
        }
    }
}
