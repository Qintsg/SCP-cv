// 精确查询已持久化显示命令；只读，不泄露领取凭证、参数或本机路径。
using Microsoft.EntityFrameworkCore;
using ScpCv.Domain.Model;
using ScpCv.Infrastructure.Persistence;

namespace ScpCv.ControlHost.Endpoints;

public static class PlaybackCommandEndpoints
{
    /// <summary>:param endpoints: HTTP路由。:returns: 受会话认证保护的只读命令查询。</summary>
    public static IEndpointRouteBuilder MapPlaybackCommandEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGroup("/api").RequireSessionAndCsrf()
            .MapGet("/playback/{windowId:int}/commands/{commandId:guid}/", GetAsync);
        return endpoints;
    }

    /// <summary>:param windowId: 两块输出之一。:param commandId: 提交回执。:returns: 精确目标的持久状态，未知身份统一404。</summary>
    private static async Task<IResult> GetAsync(int windowId, Guid commandId, HttpResponse response,
        IDbContextFactory<ControlDbContext> contexts, CancellationToken cancellationToken)
    {
        response.Headers.CacheControl = "no-store";
        if (windowId is not (1 or 2)) return Missing();
        await using var database = await contexts.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var command = await database.CommandRecords.AsNoTracking()
            .SingleOrDefaultAsync(item => item.CommandId == commandId
                && item.TargetKind == CommandTargetKind.Display && item.TargetId == windowId, cancellationToken)
            .ConfigureAwait(false);
        if (command is null) return Missing();
        return Results.Ok(new
        {
            success = true,
            command = new
            {
                command_id = command.CommandId,
                window_id = command.TargetId,
                command = command.Command,
                source_generation = command.SourceGeneration,
                status = command.Status.ToString().ToLowerInvariant(),
                result_code = command.ResultCode,
                completed_at = command.CompletedAt?.ToString("O"),
            },
        });
    }

    /// <summary>:returns: 不区分其它目标与不存在的身份。</summary>
    private static IResult Missing() => ApiEndpointSupport.Error("未找到该窗口的命令。", "command_not_found", 404);
}
