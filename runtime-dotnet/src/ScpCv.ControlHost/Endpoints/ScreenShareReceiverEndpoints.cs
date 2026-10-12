// 原生接收页私有端点：loopback与cap缺一不可，发布者cookie不能代替接收权限。SCP-cv 2026-10-10。
using System.Globalization;
using System.Net;
using System.Text.Json;
using ScpCv.ControlHost.ScreenSharing;

namespace ScpCv.ControlHost.Endpoints;

public static class ScreenShareReceiverEndpoints
{
    public static IEndpointRouteBuilder MapScreenShareReceiverEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var receiver = endpoints.MapGroup("/api/screen-share/receiver/{id:guid}");
        receiver.MapGet("/", PageAsync);
        receiver.MapGet("/offer/", OfferAsync);
        receiver.MapGet("/status/", StatusAsync);
        receiver.MapPost("/answer/", AnswerAsync);
        receiver.MapPost("/frames/", FramesAsync);
        receiver.MapPost("/end/", EndAsync);
        return endpoints;
    }

    private static Task<IResult> PageAsync(Guid id, HttpContext context, ScreenShareRegistry registry) =>
        ScreenShareHttpSupport.ExecuteAsync(context, () =>
        {
            _ = Authorize(id, context, registry);
            context.Response.Headers["Content-Security-Policy"] = ReceiverPage.ContentSecurityPolicy;
            context.Response.Headers["Permissions-Policy"] = "camera=(), microphone=(), display-capture=()";
            return Task.FromResult<IResult>(Results.Content(ReceiverPage.Html, "text/html; charset=utf-8"));
        });

    private static Task<IResult> OfferAsync(Guid id, HttpContext context, ScreenShareRegistry registry) =>
        ScreenShareHttpSupport.ExecuteAsync(context, () =>
        {
            var grant = Authorize(id, context, registry);
            var offer = registry.ReadOffer(id, grant.WindowId, grant.SourceGeneration, grant.Token);
            return Task.FromResult<IResult>(Results.Ok(new { type = offer.Type, sdp = offer.Sdp }));
        });

    private static Task<IResult> StatusAsync(Guid id, HttpContext context, ScreenShareRegistry registry) =>
        ScreenShareHttpSupport.ExecuteAsync(context, () =>
        {
            _ = Authorize(id, context, registry);
            return Task.FromResult<IResult>(Results.Ok(new { success = true }));
        });

    private static Task<IResult> AnswerAsync(Guid id, HttpContext context, ScreenShareRegistry registry, CancellationToken cancellationToken) =>
        ScreenShareHttpSupport.ExecuteAsync(context, async () =>
        {
            var grant = Authorize(id, context, registry);
            var body = await ScreenShareHttpSupport.ReadBodyAsync(context.Request, cancellationToken).ConfigureAwait(false);
            registry.SetAnswer(id, grant.WindowId, grant.SourceGeneration, grant.Token,
                ScreenShareHttpSupport.RequiredString(body, "type"), ScreenShareHttpSupport.RequiredString(body, "sdp"));
            return Results.Ok(new { success = true });
        });

    private static Task<IResult> FramesAsync(Guid id, HttpContext context, ScreenShareRegistry registry, CancellationToken cancellationToken) =>
        ScreenShareHttpSupport.ExecuteAsync(context, async () =>
        {
            var grant = Authorize(id, context, registry);
            var body = await ScreenShareHttpSupport.ReadBodyAsync(context.Request, cancellationToken).ConfigureAwait(false);
            if (!body.TryGetProperty("frames", out var value) || value.ValueKind != JsonValueKind.Number ||
                !value.TryGetInt64(out var frames) || !ScreenShareHttpSupport.Boolean(body, "activated"))
                throw new ScreenShareException("未激活或无实际帧的接收不能授予播出状态。", "share_frame_invalid");
            registry.ReportFrame(id, grant.WindowId, grant.SourceGeneration, grant.Token, frames,
                ScreenShareHttpSupport.Boolean(body, "has_audio"), ReceiverQuality.Read(body));
            return Results.Ok(new { success = true });
        });

    private static Task<IResult> EndAsync(Guid id, HttpContext context, ScreenShareRegistry registry, ScreenShareService service, CancellationToken cancellationToken) =>
        ScreenShareHttpSupport.ExecuteAsync(context, async () =>
        {
            var grant = Authorize(id, context, registry);
            await service.StopReceiverAsync(id, grant.WindowId, grant.SourceGeneration, grant.Token, cancellationToken).ConfigureAwait(false);
            return Results.Ok(new { success = true });
        });

    private static ScreenShareReceiverGrant Authorize(Guid id, HttpContext context, ScreenShareRegistry registry)
    {
        var request = context.Request;
        var remote = context.Connection.RemoteIpAddress;
        if (remote is null || !IPAddress.IsLoopback(remote) ||
            request.Query.Keys.Any(key => key is not ("window_id" or "source_generation")) ||
            request.Query["window_id"].Count != 1 || request.Query["source_generation"].Count != 1 ||
            !int.TryParse(request.Query["window_id"], NumberStyles.None, CultureInfo.InvariantCulture, out var window) ||
            !long.TryParse(request.Query["source_generation"], NumberStyles.None, CultureInfo.InvariantCulture, out var generation))
            throw Denied();
        if (request.Headers.TryGetValue("Origin", out var origins) &&
            (origins.Count != 1 || !string.Equals(origins[0], $"{request.Scheme}://{request.Host}", StringComparison.OrdinalIgnoreCase)))
            throw Denied();
        var authorization = request.Headers.Authorization;
        if (authorization.Count != 1 || authorization[0] is not { } header ||
            !header.StartsWith("Bearer ", StringComparison.Ordinal) || header.Length != 71)
            throw Denied();
        var token = header[7..];
        _ = registry.ReadOffer(id, window, generation, token);
        return new(id, window, generation, token);
    }

    private static ScreenShareException Denied() => new("分享接收授权已失效。", "share_receiver_denied", 404);
}
