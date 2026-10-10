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

    private static Task<IResult> GetAsync(Guid id, HttpContext context, ScreenShareRegistry registry) =>
        ScreenShareHttpSupport.ExecuteAsync(context, () => Task.FromResult(ScreenShareHttpSupport.Response(registry.GetOwned(Owner(context), id))));

    private static Task<IResult> HeartbeatAsync(Guid id, HttpContext context, ScreenShareRegistry registry) =>
        ScreenShareHttpSupport.ExecuteAsync(context, () => Task.FromResult(ScreenShareHttpSupport.Response(registry.Heartbeat(Owner(context), id))));

    private static Task<IResult> StopAsync(Guid id, HttpContext context, ScreenShareService service, CancellationToken cancellationToken) =>
        ScreenShareHttpSupport.ExecuteAsync(context, async () => ScreenShareHttpSupport.Response(
            await service.StopAsync(Owner(context), id, cancellationToken).ConfigureAwait(false)));

    private static string Owner(HttpContext context) => context.User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new ScreenShareException("未登录。", "unauthorized", 401);
}
