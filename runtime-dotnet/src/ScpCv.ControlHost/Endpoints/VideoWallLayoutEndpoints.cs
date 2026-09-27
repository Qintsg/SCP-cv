// 大屏布局草稿、预览与固定预设的 REST 入口。
using System.Text.Json;
using ScpCv.Contracts.Http;
using ScpCv.Domain.Model;
using ScpCv.Infrastructure.Playback;
using ScpCv.Infrastructure.VideoWall;

namespace ScpCv.ControlHost.Endpoints;

public static class VideoWallLayoutEndpoints
{
    public static IEndpointRouteBuilder MapVideoWallLayoutEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var api = endpoints.MapGroup("/api/video-wall").RequireSessionAndCsrf();
        api.MapGet("/layout/", GetAsync);
        api.MapPut("/layout/", SaveAsync);
        api.MapPost("/layout/apply/", ApplyDraftAsync);
        api.MapPost("/presets/{preset}/apply/", ApplyPresetAsync);
        return endpoints;
    }

    private static async Task<IResult> GetAsync(VideoWallLayoutService layouts, CancellationToken cancellationToken)
    {
        try
        {
            return Results.Ok(new { success = true, layout = await layouts.GetAsync(cancellationToken).ConfigureAwait(false) });
        }
        catch (VideoWallLayoutException exception) { return Error(exception); }
    }

    private static async Task<IResult> SaveAsync(
        HttpRequest request,
        VideoWallLayoutService layouts,
        CancellationToken cancellationToken)
    {
        try
        {
            var submitted = await request.ReadFromJsonAsync<VideoWallLayoutDto>(cancellationToken).ConfigureAwait(false)
                ?? throw new VideoWallLayoutException("缺少大屏布局内容。", "layout_invalid");
            if (submitted.Mappings is null || submitted.Mappings.Any(mapping => mapping.Input is null))
                throw new VideoWallLayoutException("大屏映射内容不完整。", "layout_invalid");
            var layout = new WallLayout(submitted.Name, submitted.Mappings.Select(mapping =>
                new WallMapping(ParseRegion(mapping.Region), new WallInput(
                    ParseInput(mapping.Input.Kind), mapping.Input.IpAddress))).ToArray());
            return Results.Ok(new { success = true, layout = await layouts.SaveDraftAsync(layout, cancellationToken).ConfigureAwait(false) });
        }
        catch (JsonException exception)
        {
            return ApiEndpointSupport.Error($"大屏布局 JSON 无效：{exception.Message}", "layout_invalid");
        }
        catch (VideoWallLayoutException exception) { return Error(exception); }
    }

    private static async Task<IResult> ApplyDraftAsync(
        VideoWallLayoutService layouts,
        RuntimeStateService runtime,
        CancellationToken cancellationToken)
    {
        try
        {
            var layout = await layouts.ApplyDraftAsync(cancellationToken).ConfigureAwait(false);
            return Results.Ok(new
            {
                success = true,
                layout,
                runtime = await runtime.GetRuntimeAsync(cancellationToken).ConfigureAwait(false),
                sessions = await runtime.GetSessionsAsync(cancellationToken).ConfigureAwait(false),
            });
        }
        catch (VideoWallLayoutException exception) { return Error(exception); }
        catch (PlaybackServiceException exception) { return ApiEndpointSupport.Error(exception.Message, exception.Code); }
    }

    private static async Task<IResult> ApplyPresetAsync(
        string preset,
        VideoWallLayoutService layouts,
        RuntimeStateService runtime,
        CancellationToken cancellationToken)
    {
        try
        {
            var layout = await layouts.ApplyPresetAsync(preset, cancellationToken).ConfigureAwait(false);
            return Results.Ok(new
            {
                success = true,
                layout,
                runtime = await runtime.GetRuntimeAsync(cancellationToken).ConfigureAwait(false),
                sessions = await runtime.GetSessionsAsync(cancellationToken).ConfigureAwait(false),
            });
        }
        catch (VideoWallLayoutException exception) { return Error(exception); }
        catch (PlaybackServiceException exception) { return ApiEndpointSupport.Error(exception.Message, exception.Code); }
    }

    private static WallRegion ParseRegion(string value) => value switch
    {
        "fullscreen" => WallRegion.Fullscreen,
        "left" => WallRegion.Left,
        "right" => WallRegion.Right,
        _ => throw new VideoWallLayoutException($"未知的大屏区域：{value}。", "layout_invalid"),
    };

    private static WallInputKind ParseInput(string value) => value switch
    {
        "window_1" => WallInputKind.Window1,
        "window_2" => WallInputKind.Window2,
        "laptop" => WallInputKind.Laptop,
        "ip_stream" => WallInputKind.IpStream,
        _ => throw new VideoWallLayoutException($"未知的大屏输入：{value}。", "layout_invalid"),
    };

    private static IResult Error(VideoWallLayoutException exception) => ApiEndpointSupport.Error(
        exception.Message,
        exception.Code,
        exception.Code == "protocol_unavailable"
            ? StatusCodes.Status409Conflict
            : StatusCodes.Status400BadRequest);
}
