// PPT 默认页图播放与实验性 Office 放映的共享设置入口。
using System.Text.Json;
using ScpCv.ControlHost.Configuration;
using ScpCv.Infrastructure.Playback;

namespace ScpCv.ControlHost.Endpoints;

public static class PowerPointSettingsEndpoints
{
    public static IEndpointRouteBuilder MapPowerPointSettingsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var api = endpoints.MapGroup("/api/settings/powerpoint").RequireSessionAndCsrf();
        api.MapGet("/", GetAsync);
        api.MapPatch("/", UpdateAsync);
        return endpoints;
    }

    private static async Task<IResult> GetAsync(
        PowerPointSettingsService settings,
        SafetyModeOptions safetyMode,
        CancellationToken cancellationToken) =>
        Results.Ok(new
        {
            success = true,
            settings = Snapshot(await settings.GetAsync(cancellationToken).ConfigureAwait(false), safetyMode),
        });

    private static async Task<IResult> UpdateAsync(
        HttpRequest request,
        PowerPointSettingsService settings,
        SafetyModeOptions safetyMode,
        CancellationToken cancellationToken)
    {
        JsonElement body;
        try { body = await request.ReadFromJsonAsync<JsonElement>(cancellationToken).ConfigureAwait(false); }
        catch (JsonException) { return ApiEndpointSupport.Error("请求 JSON 无效。", "invalid_request"); }
        if (body.ValueKind != JsonValueKind.Object ||
            !body.TryGetProperty("experimental_enabled", out var value) ||
            value.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
            return ApiEndpointSupport.Error("experimental_enabled 必须为布尔值。", "invalid_request");

        var enabled = value.GetBoolean();
        if (enabled && !Available(safetyMode))
            return ApiEndpointSupport.Error("当前主机未提供可用的实验性 PowerPoint 放映。", "powerpoint_unavailable",
                StatusCodes.Status503ServiceUnavailable);
        await settings.SetAsync(enabled, cancellationToken).ConfigureAwait(false);
        return Results.Ok(new { success = true, settings = Snapshot(enabled, safetyMode) });
    }

    private static object Snapshot(bool enabled, SafetyModeOptions safetyMode) => new
    {
        experimental_enabled = enabled,
        available = Available(safetyMode),
        detail = Available(safetyMode)
            ? "实验性原生放映可能与现场 Office 文稿冲突；仅影响下一次打开。"
            : "当前为 Simulation 或未安装 PowerPoint，原生放映不可用。",
    };

    private static bool Available(SafetyModeOptions safetyMode) =>
        !safetyMode.IsSimulation && OperatingSystem.IsWindows() &&
        Type.GetTypeFromProgID("PowerPoint.Application") is not null;
}
