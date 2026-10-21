// 受认证的输出路由；前端仅传受信来源，不接受原始帧/目标IP/端口。
using System.Text.Json;
using ScpCv.Contracts.Http;
using ScpCv.Infrastructure.DisplayRouting;

namespace ScpCv.ControlHost.Endpoints;

public static class DisplayRoutingEndpoints
{
    /// <summary>:param endpoints: 主机路由。:returns: 已登记认证与CSRF边界的端点。</summary>
    public static IEndpointRouteBuilder MapDisplayRoutingEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var api = endpoints.MapGroup("/api/display-routing").RequireSessionAndCsrf();
        api.MapGet("/", async (DisplayRoutingService service, CancellationToken token) =>
            Results.Ok(new { success = true, outputs = await service.GetAsync(token).ConfigureAwait(false) }));
        api.MapPut("/televisions/{target}/", ApplyAsync);
        return endpoints;
    }

    /// <summary>:param request: JSON。:param target: 左右电视白名单。:param service: 路由服务。:param token: 取消。</summary>
    private static async Task<IResult> ApplyAsync(HttpRequest request, string target, DisplayRoutingService service, CancellationToken token)
    {
        try
        {
            var body = await request.ReadFromJsonAsync<TelevisionRouteRequest>(token).ConfigureAwait(false);
            if (body?.Input is not { } input) throw new DisplayRoutingException("请选择电视输入。");
            var (output, result) = await service.ApplyAsync(target, input, body.Force, token).ConfigureAwait(false);
            return Results.Ok(new { success = true, output, result });
        }
        catch (JsonException) { return ApiEndpointSupport.Error("电视切换请求格式无效。", "display_routing_invalid"); }
        catch (DisplayRoutingException exception)
        {
            return ApiEndpointSupport.Error(exception.Message, exception.Code,
                exception.Code == "display_routing_invalid" ? StatusCodes.Status400BadRequest : StatusCodes.Status409Conflict);
        }
    }
}
