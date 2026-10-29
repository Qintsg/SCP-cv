// 文件描述：Web维护菜单的窗口置顶控制入口。项目：SCP-cv；文件：WindowForegroundEndpoints.cs；作者：Qintsg；日期：2026-10-29。
using System.Text.Json;
using ScpCv.Infrastructure.Playback;

namespace ScpCv.ControlHost.Endpoints;

public static class WindowForegroundEndpoints
{
    /// <summary>:param endpoints: HTTP路由。:returns: 复用会话及CSRF保护的维护入口。</summary>
    public static IEndpointRouteBuilder MapWindowForegroundEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGroup("/api").RequireSessionAndCsrf().MapPost("/playback/window-foreground/", SetAsync);
        return endpoints;
    }

    /// <summary>:param request: 布尔foreground意图。:param runtime: 正式队列服务。:returns: 入队回执，不提前声明窗口已改变。</summary>
    private static async Task<IResult> SetAsync(HttpRequest request, RuntimeStateService runtime, CancellationToken cancellationToken)
    {
        JsonDocument document;
        try { document = await JsonDocument.ParseAsync(request.Body, cancellationToken: cancellationToken).ConfigureAwait(false); }
        catch (JsonException) { return ApiEndpointSupport.Error("请求JSON无效。", "invalid_json", 400); }
        using (document)
        {
            var body = document.RootElement;
            if (body.ValueKind != JsonValueKind.Object || !body.TryGetProperty("foreground", out var foreground)
                || foreground.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
                return ApiEndpointSupport.Error("foreground必须是布尔值。", "invalid_foreground", 400);
            try
            {
                var sessions = await runtime.SetWindowsForegroundAsync(foreground.GetBoolean(), cancellationToken).ConfigureAwait(false);
                return Results.Json(new { success = true, sessions }, statusCode: 202);
            }
            catch (PlaybackServiceException failure) { return ApiEndpointSupport.Error(failure.Message, failure.Code, failure.Code == "window_state_unavailable" ? 409 : 400); }
        }
    }
}
