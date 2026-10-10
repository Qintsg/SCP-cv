// 共享HTTP合同和请求体边界；不记录信令，也不自动序列化私有条目。SCP-cv 2026-10-10。
using System.Text.Json;
using System.Text.Json.Serialization;
using ScpCv.ControlHost.Endpoints;
using ScpCv.Infrastructure.Playback;

namespace ScpCv.ControlHost.ScreenSharing;

internal static class ScreenShareHttpSupport
{
    // 64KiB解码SDP最坏JSON转义需要6倍；先限制线上体积，再由Registry限制UTF8 SDP。
    private const int MaximumBodyBytes = 65536 * 6 + 1024;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) },
    };

    internal static IResult Response(ScreenShareSnapshot share, int status = 200) =>
        Results.Json(new { success = true, share }, JsonOptions, statusCode: status);

    internal static async Task<IResult> ExecuteAsync(HttpContext context, Func<Task<IResult>> operation)
    {
        NoStore(context);
        try { return await operation().ConfigureAwait(false); }
        catch (ScreenShareException error) { return ApiEndpointSupport.Error(error.Message, error.Code, error.StatusCode); }
        catch (PlaybackServiceException error)
        {
            return ApiEndpointSupport.Error(error.Message, error.Code, error.Code switch
            { "share_player_offline" => 503, "share_audio_unavailable" => 409, _ => 400 });
        }
    }

    internal static void NoStore(HttpContext context)
    {
        context.Response.Headers.CacheControl = "no-store";
        context.Response.Headers["Referrer-Policy"] = "no-referrer";
        context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    }

    internal static async Task<JsonElement> ReadBodyAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        if (request.ContentLength > MaximumBodyBytes)
            throw new ScreenShareException("分享请求体超过大小限制。", "share_request_too_large", 413);
        using var buffer = new MemoryStream();
        var chunk = new byte[8192];
        int read;
        while ((read = await request.Body.ReadAsync(chunk, cancellationToken).ConfigureAwait(false)) != 0)
        {
            if (buffer.Length + read > MaximumBodyBytes)
                throw new ScreenShareException("分享请求体超过大小限制。", "share_request_too_large", 413);
            buffer.Write(chunk, 0, read);
        }
        try
        {
            using var document = JsonDocument.Parse(buffer.ToArray(), new JsonDocumentOptions { MaxDepth = 8 });
            if (document.RootElement.ValueKind != JsonValueKind.Object) throw InvalidBody();
            return document.RootElement.Clone();
        }
        catch (JsonException) { throw InvalidBody(); }
    }

    internal static string RequiredString(JsonElement body, string name) =>
        body.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString()! : throw InvalidBody();

    internal static bool Boolean(JsonElement body, string name, bool fallback = false) =>
        !body.TryGetProperty(name, out var value) ? fallback : value.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? value.GetBoolean() : throw InvalidBody();

    internal static ScreenShareException InvalidBody() => new("分享请求体必须符合JSON字段类型。", "share_request_invalid");
}
