// 认证短租约与双FLV出口：先授权再拉流，原始URI和凭据从不发给观看端。
using System.Net.WebSockets;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Options;
using ScpCv.ControlHost.StreamPreview;
using ScpCv.Infrastructure.Auth;

namespace ScpCv.ControlHost.Endpoints;

public static class StreamPreviewEndpoints
{
    public static IEndpointRouteBuilder MapStreamPreviewEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/stream-previews").RequireSessionAndCsrf();
        group.AddEndpointFilter(async (context, next) =>
        { context.HttpContext.Response.Headers.CacheControl = "no-store"; return await next(context).ConfigureAwait(false); });
        group.MapPost("/", CreateAsync);
        group.MapPost("/native/resolve/", ResolveNativeAsync);
        group.MapGet("/status/", (PreviewOptions options, StreamPreviewRegistry registry) => Results.Ok(new
        { lease_count = registry.LeaseCount, upstream_count = registry.FeedCount, max_upstreams = options.MaximumFeeds,
            egress_kbps = options.EgressKilobitsPerSecond, mode = "server_flv", detail = "仅有观看需求时拉流；客户端无需安装Node或FFmpeg" }));
        group.MapGet("/{id:guid}/", Get);
        group.MapPost("/{id:guid}/heartbeat/", HeartbeatAsync);
        group.MapDelete("/{id:guid}/", Delete);
        group.MapGet("/{id:guid}/stream.flv", (Guid id, HttpContext context, StreamPreviewRegistry registry, PreviewBandwidth budget, CsrfTokenService csrf) => StreamAsync(id, false, context, registry, budget, csrf));
        group.MapGet("/{id:guid}/stream.ws", (Guid id, HttpContext context, StreamPreviewRegistry registry, PreviewBandwidth budget, CsrfTokenService csrf) => StreamAsync(id, true, context, registry, budget, csrf));
        return endpoints;
    }

    private static Task<IResult> CreateAsync(PreviewRequest body, HttpContext context, PreviewSourceResolver sources,
        StreamPreviewRegistry registry, CsrfTokenService csrf, PreviewSharingIdentity identity, CancellationToken cancellationToken) => ExecuteAsync(context, async () =>
    {
        var source = await sources.ResolveAsync(body, cancellationToken).ConfigureAwait(false);
        var owner = Owner(context); var origin = Origin(context, csrf);
        var lease = registry.Create(owner, origin, source);
        return Results.Json(new { id = lease.Id, expires_at = lease.ExpiresAt,
            sharing_key = identity.Create(owner, origin, source),
            http_path = $"/api/stream-previews/{lease.Id}/stream.flv", ws_path = $"/api/stream-previews/{lease.Id}/stream.ws" }, statusCode: 201);
    });

    // 只供受信Electron主进程的固定描述符请求；不接受任意URL，不创建代理/媒体进程。
    private static Task<IResult> ResolveNativeAsync(PreviewRequest body, HttpContext context, PreviewSourceResolver sources,
        PreviewOptions options, PreviewSharingIdentity identity, TimeProvider clock, CsrfTokenService csrf,
        CancellationToken cancellationToken) => ExecuteAsync(context, async () =>
    {
        var source = await sources.ResolveAsync(body, cancellationToken).ConfigureAwait(false);
        return Results.Ok(new { uri = source.UpstreamUri, sharing_key = identity.Create(Owner(context), Origin(context, csrf), source),
            source_id = source.SourceId, source_revision = source.SourceRevision,
            expires_at = clock.GetUtcNow().AddSeconds(options.LeaseSeconds), quality = source.Quality });
    });

    private static IResult Get(Guid id, HttpContext context, StreamPreviewRegistry registry, CsrfTokenService csrf)
    {
        try { return Results.Ok(registry.Get(Owner(context), Origin(context, csrf), id)); }
        catch (PreviewException error) { return Error(error); }
    }

    private static Task<IResult> HeartbeatAsync(Guid id, HttpContext context, StreamPreviewRegistry registry,
        PreviewSourceResolver sources, CsrfTokenService csrf, CancellationToken cancellationToken) => ExecuteAsync(context, async () =>
    {
        var owner = Owner(context); var origin = Origin(context, csrf);
        if (await PreviewHeartbeatBody.ReadAsync(context.Request, cancellationToken).ConfigureAwait(false) is { } shared)
        {
            if (shared.Aliases is not { Length: >= 1 and <= 64 } aliases || aliases.Distinct().Count() != aliases.Length)
                throw new PreviewException("预览别名列表无效", "invalid_preview_aliases", 400);
            foreach (var alias in aliases)
                if (!await sources.IsCurrentAsync(registry.Source(owner, origin, alias), cancellationToken).ConfigureAwait(false))
                    throw new PreviewException("预览别名已更新，请重新验证", "preview_alias_changed", 409);
            return Results.Ok(registry.HeartbeatShared(owner, origin, id, aliases));
        }
        if (!await sources.IsCurrentAsync(registry.Source(owner, origin, id), cancellationToken).ConfigureAwait(false))
        {
            registry.Delete(owner, origin, id);
            throw new PreviewException("来源已更新，请重新打开预览", "preview_source_changed", 410);
        }
        return Results.Ok(registry.Heartbeat(owner, origin, id));
    });

    private static IResult Delete(Guid id, HttpContext context, StreamPreviewRegistry registry, CsrfTokenService csrf)
    {
        try { registry.Delete(Owner(context), Origin(context, csrf), id); return Results.NoContent(); }
        catch (PreviewException error) { return Error(error); }
    }

    private static Task<IResult> StreamAsync(Guid id, bool websocket, HttpContext context, StreamPreviewRegistry registry,
        PreviewBandwidth budget, CsrfTokenService csrf) => ExecuteAsync(context, async () =>
    {
        var origin = Origin(context, csrf);
        if (websocket && (!context.WebSockets.IsWebSocketRequest || !context.Request.Headers.ContainsKey("Origin")))
            throw new PreviewException("需要有效的WebSocket来源与升级请求", "invalid_preview_origin", 403);
        using var viewer = registry.Open(Owner(context), origin, id);
        using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted, viewer.Lifetime);
        using var firstFrame = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
        firstFrame.CancelAfter(TimeSpan.FromSeconds(12));
        PreviewGop first;
        try { first = await viewer.Reader.ReadAsync(firstFrame.Token).ConfigureAwait(false); }
        catch (Exception error) when (error is OperationCanceledException or System.Threading.Channels.ChannelClosedException)
        { throw new PreviewException("暂时无法取得预览画面，请检查输入设备或服务器依赖", "preview_first_frame_unavailable"); }
        context.Response.Headers.CacheControl = "no-store"; context.Response.Headers["X-Accel-Buffering"] = "no";
        if (websocket)
        {
            using var socket = await context.WebSockets.AcceptWebSocketAsync().ConfigureAwait(false);
            var receive = ReceiveAsync(socket, lifetime);
            try
            {
                await SendAsync(FlvReader.Header).ConfigureAwait(false);
                await SendAsync(first.Bytes).ConfigureAwait(false);
                await foreach (var gop in viewer.Reader.ReadAllAsync(lifetime.Token).ConfigureAwait(false)) await SendAsync(gop.Bytes).ConfigureAwait(false);
            }
            finally
            {
                await lifetime.CancelAsync().ConfigureAwait(false);
                socket.Abort(); await receive.ConfigureAwait(false);
            }
            async Task SendAsync(ReadOnlyMemory<byte> bytes)
            {
                for (var offset = 0; offset < bytes.Length; offset += PreviewBandwidth.MaximumChunkBytes)
                {
                    var part = bytes.Slice(offset, Math.Min(PreviewBandwidth.MaximumChunkBytes, bytes.Length - offset));
                    await budget.WaitAsync(part.Length, lifetime.Token).ConfigureAwait(false); var started = System.Diagnostics.Stopwatch.GetTimestamp();
                    await socket.SendAsync(part, WebSocketMessageType.Binary, true, lifetime.Token).ConfigureAwait(false);
                    budget.ObserveWrite(System.Diagnostics.Stopwatch.GetElapsedTime(started));
                }
            }
        }
        else
        {
            context.Response.ContentType = "video/x-flv";
            await SendAsync(FlvReader.Header).ConfigureAwait(false); await SendAsync(first.Bytes).ConfigureAwait(false);
            await foreach (var gop in viewer.Reader.ReadAllAsync(lifetime.Token).ConfigureAwait(false)) await SendAsync(gop.Bytes).ConfigureAwait(false);
            async Task SendAsync(ReadOnlyMemory<byte> bytes)
            {
                for (var offset = 0; offset < bytes.Length; offset += PreviewBandwidth.MaximumChunkBytes)
                {
                    var part = bytes.Slice(offset, Math.Min(PreviewBandwidth.MaximumChunkBytes, bytes.Length - offset));
                    await budget.WaitAsync(part.Length, lifetime.Token).ConfigureAwait(false); var started = System.Diagnostics.Stopwatch.GetTimestamp();
                    await context.Response.Body.WriteAsync(part, lifetime.Token).ConfigureAwait(false);
                    await context.Response.Body.FlushAsync(lifetime.Token).ConfigureAwait(false);
                    budget.ObserveWrite(System.Diagnostics.Stopwatch.GetElapsedTime(started));
                }
            }
        }
        return Results.Empty;
    });

    private static async Task ReceiveAsync(WebSocket socket, CancellationTokenSource lifetime)
    {
        var bytes = new byte[128];
        try
        {
            while (!lifetime.IsCancellationRequested)
            {
                var result = await socket.ReceiveAsync(bytes.AsMemory(), lifetime.Token).ConfigureAwait(false);
                // 此连接只供服务端输出；不接受客户端文字／二进制控制数据。
                if (result.MessageType is WebSocketMessageType.Close or WebSocketMessageType.Text or WebSocketMessageType.Binary) return;
            }
        }
        catch (Exception error) when (error is WebSocketException or OperationCanceledException or IOException) { }
        finally { await lifetime.CancelAsync().ConfigureAwait(false); }
    }

    // 绑定加密会话票据摘要，而非只绑定用户ID或可能未轮换的CSRF。框架续签也会安全失效旧预览。
    private static string Owner(HttpContext context)
    {
        var id = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        var options = context.RequestServices.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>().Get(CookieAuthenticationDefaults.AuthenticationScheme);
        var cookie = options.CookieManager.GetRequestCookie(context, options.Cookie.Name!);
        if (id is null || string.IsNullOrEmpty(cookie)) throw new PreviewException("请先登录", "unauthorized", 401);
        return id + ":" + context.Request.Scheme + ":" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(cookie)));
    }

    private static string Origin(HttpContext context, CsrfTokenService csrf)
    {
        if (!csrf.IsOriginAllowed(context) || context.Request.Headers["Sec-Fetch-Site"] == "cross-site" && !context.Request.Headers.ContainsKey("Origin"))
            throw new PreviewException("预览来源不被允许", "invalid_preview_origin", 403);
        return context.Request.Headers.TryGetValue("Origin", out var origin) ? origin.ToString() : $"{context.Request.Scheme}://{context.Request.Host}";
    }

    private static IResult Error(PreviewException error) => ApiEndpointSupport.Error(error.Message, error.Code, error.StatusCode);
    private static async Task<IResult> ExecuteAsync(HttpContext context, Func<Task<IResult>> operation)
    {
        try { return await operation().ConfigureAwait(false); }
        catch (PreviewException error) { if (!context.Response.HasStarted) return Error(error); context.Abort(); return Results.Empty; }
        catch (Exception error) when (error is OperationCanceledException or IOException or WebSocketException or System.Threading.Channels.ChannelClosedException)
        {
            if (context.Response.HasStarted) { context.Abort(); return Results.Empty; }
            return ApiEndpointSupport.Error("预览连接已结束，请重试", "preview_unavailable", 503);
        }
    }
}
