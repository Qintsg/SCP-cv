// 现场摄像机与音频沿用会话和CSRF；未知绑定不能借直接请求绕过禁用按钮。
using ScpCv.Contracts.Http;
using ScpCv.Infrastructure.Devices;

namespace ScpCv.ControlHost.Endpoints;

public static class VenueDeviceEndpoints
{
    public static IEndpointRouteBuilder MapVenueDeviceEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var api = endpoints.MapGroup("/api/venue").RequireSessionAndCsrf();
        api.MapGet("/", async (VenueDeviceService service, CancellationToken token) =>
        {
            try { return Results.Ok(new { success = true, venue = await service.GetAsync(token).ConfigureAwait(false) }); }
            catch (VenueDeviceException exception) { return Error(exception); }
        });
        api.MapPost("/audio/read/", async (VenueDeviceService service, CancellationToken token) =>
        {
            try { return Results.Ok(new { success = true, audio = await service.RefreshAsync(token).ConfigureAwait(false) }); }
            catch (VenueDeviceException exception) { return Error(exception); }
        });
        api.MapPut("/audio/", async (VenueAudioSettings settings, VenueDeviceService service, CancellationToken token) =>
        {
            try { return Results.Ok(new { success = true, audio = await service.ApplyAsync(settings, token).ConfigureAwait(false) }); }
            catch (VenueDeviceException exception) { return Error(exception); }
        });
        api.MapPost("/cameras/{id}/motion/", RejectCamera);
        api.MapPost("/cameras/{id}/positions/{position:int}/{action}/", RejectPosition);
        return endpoints;
    }

    private static IResult RejectCamera(string id, VenueDeviceService service)
    {
        try { service.ValidateCameraControl(id); return Results.NoContent(); }
        catch (VenueDeviceException exception) { return Error(exception); }
    }
    private static IResult RejectPosition(string id, int position, string action, VenueDeviceService service)
    {
        if (position is < 1 or > 255 || action is not ("save" or "call"))
            return ApiEndpointSupport.Error("位置应为1–255，动作只能save或call。", "invalid_position");
        return RejectCamera(id, service);
    }
    private static IResult Error(VenueDeviceException exception) => ApiEndpointSupport.Error(
        exception.Message, "venue_unavailable", StatusCodes.Status409Conflict);
}
