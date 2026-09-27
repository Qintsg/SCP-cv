// 直播源登记的受保护 REST 入口。
using ScpCv.Infrastructure.Media;

namespace ScpCv.ControlHost.Endpoints;

public static partial class MediaEndpoints
{
    private static async Task<IResult> AddStreamSourceAsync(
        HttpRequest request,
        MediaSourceService media,
        CancellationToken cancellationToken)
    {
        var body = await ReadBodyAsync(request, cancellationToken).ConfigureAwait(false);
        if (body.Error is not null) return body.Error;
        try
        {
            var source = await media.AddStreamAsync(
                String(body.Value, "source_type"),
                String(body.Value, "url"),
                EmptyToNull(String(body.Value, "name")),
                NullableInt64(body.Value, "folder_id"),
                Boolean(body.Value, "preheat_enabled", false),
                cancellationToken).ConfigureAwait(false);
            return Results.Json(new { success = true, source }, statusCode: StatusCodes.Status201Created);
        }
        catch (MediaServiceException exception)
        {
            return MediaError(exception);
        }
    }
}
