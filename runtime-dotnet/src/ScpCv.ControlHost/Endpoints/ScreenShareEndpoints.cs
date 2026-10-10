// 发布者共享入口：沿用会话/CSRF，预约和受理不冒称已经出画。SCP-cv 2026-10-10。
using System.Security.Claims;
using System.Text.Json;
using ScpCv.ControlHost.ScreenSharing;

namespace ScpCv.ControlHost.Endpoints;

public static class ScreenShareEndpoints
{
    public static IEndpointRouteBuilder MapScreenShareEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var shares = endpoints.MapGroup("/api/screen-shares").RequireSessionAndCsrf();
        shares.MapPost("/", CreateAsync);
        shares.MapPost("/{id:guid}/offer/", OfferAsync);
        shares.MapGet("/{id:guid}/", GetAsync);
        shares.MapPost("/{id:guid}/heartbeat/", HeartbeatAsync);
        shares.MapDelete("/{id:guid}/", StopAsync);
        shares.MapPatch("/{id:guid}/audio/", AudioAsync);
        return endpoints;
    }

    private static Task<IResult> CreateAsync(HttpContext context, ScreenShareService service, CancellationToken cancellationToken) =>
        ScreenShareHttpSupport.ExecuteAsync(context, async () =>
        {
            var body = await ScreenShareHttpSupport.ReadBodyAsync(context.Request, cancellationToken).ConfigureAwait(false);
            if (!body.TryGetProperty("window_id", out var raw) || raw.ValueKind != JsonValueKind.Number || !raw.TryGetInt32(out var window))
                throw ScreenShareHttpSupport.InvalidBody();
            var share = await service.CreateAsync(Owner(context), window, ScreenShareHttpSupport.Boolean(body, "has_audio"), cancellationToken).ConfigureAwait(false);
            return ScreenShareHttpSupport.Response(share, 201);
        });

    private static Task<IResult> OfferAsync(Guid id, HttpContext context, ScreenShareService service, CancellationToken cancellationToken) =>
        ScreenShareHttpSupport.ExecuteAsync(context, async () =>
        {
            var body = await ScreenShareHttpSupport.ReadBodyAsync(context.Request, cancellationToken).ConfigureAwait(false);
            var share = await service.OfferAsync(Owner(context), id, ScreenShareHttpSupport.RequiredString(body, "type"),
                ScreenShareHttpSupport.RequiredString(body, "sdp"), cancellationToken).ConfigureAwait(false);
            return ScreenShareHttpSupport.Response(share, 202);
        });

    private static Task<IResult> GetAsync(Guid id, HttpContext context, ScreenShareService service, CancellationToken cancellationToken) =>
        ScreenShareHttpSupport.ExecuteAsync(context, async () => ScreenShareHttpSupport.Response(
            await service.GetOwnedAsync(Owner(context), id, cancellationToken).ConfigureAwait(false)));

    /// <summary>:returns: 202仅表示排队，不能以请求值覆盖实际音量；额外/重复/缺少字段均拒绝。</summary>
    private static Task<IResult> AudioAsync(Guid id, HttpContext context, ScreenShareService service, CancellationToken cancellationToken) =>
        ScreenShareHttpSupport.ExecuteAsync(context, async () =>
        {
            var body = await ScreenShareHttpSupport.ReadBodyAsync(context.Request, cancellationToken).ConfigureAwait(false);
            if (body.EnumerateObject().Count() != 2 || !body.TryGetProperty("volume", out var level) ||
                level.ValueKind != JsonValueKind.Number || !level.TryGetInt32(out var volume) || volume is < 0 or > 100 ||
                !body.TryGetProperty("muted", out var mute) || mute.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
                throw ScreenShareHttpSupport.InvalidBody();
            var receipt = await service.SetAudioAsync(Owner(context), id, volume, mute.GetBoolean(), cancellationToken).ConfigureAwait(false);
            return Results.Json(new { success = true, submitted_command_id = receipt.CommandId, window_id = receipt.WindowId,
                share_id = receipt.ShareId, source_generation = receipt.SourceGeneration }, statusCode: 202);
        });

    private static Task<IResult> HeartbeatAsync(Guid id, HttpContext context, ScreenShareRegistry registry) =>
        ScreenShareHttpSupport.ExecuteAsync(context, () => Task.FromResult(ScreenShareHttpSupport.Response(registry.Heartbeat(Owner(context), id))));

    private static Task<IResult> StopAsync(Guid id, HttpContext context, ScreenShareService service, CancellationToken cancellationToken) =>
        ScreenShareHttpSupport.ExecuteAsync(context, async () => ScreenShareHttpSupport.Response(
            await service.StopAsync(Owner(context), id, cancellationToken).ConfigureAwait(false)));

    private static string Owner(HttpContext context) => context.User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new ScreenShareException("未登录。", "unauthorized", 401);
}
