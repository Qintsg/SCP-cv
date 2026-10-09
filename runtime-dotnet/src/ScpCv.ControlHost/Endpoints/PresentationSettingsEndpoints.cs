// LibreOffice依赖与真实能力报告；旧PowerPoint入口明确退役。
using ScpCv.Contracts.Presentations.Dependencies;

namespace ScpCv.ControlHost.Endpoints;

public static class PresentationSettingsEndpoints
{
    /// <summary>:param endpoints: 控制面路由。:returns: LibreOffice配置与退役合同。</summary>
    public static IEndpointRouteBuilder MapPresentationSettingsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var api = endpoints.MapGroup("/api/settings").RequireSessionAndCsrf();
        api.MapGet("/presentation/", Snapshot);
        api.MapGet("/powerpoint/", Retired);
        api.MapPatch("/powerpoint/", Retired);
        return endpoints;
    }

    /// <summary>:returns: 旧请求不能改为另一个副作用动作。</summary>
    private static IResult Retired() => ApiEndpointSupport.Error("PowerPoint入口已退役，请使用LibreOffice演示文稿配置。", "powerpoint_retired", StatusCodes.Status410Gone);

    /// <summary>:param dependencies: 单次只读依赖检查。:returns: 静态依赖不等于实际出画，能力与播放状态分别报告。</summary>
    private static IResult Snapshot(IPresentationDependencyProbe dependencies)
    {
        var report = dependencies.Inspect();
        return Results.Ok(new
        {
            success = true,
            settings = new
            {
                default_engine = "libreoffice", available = report.Available, dependency_status = report.Available ? "installed" : "unavailable",
                detail = report.Detail,
                components = report.Components.Select(component => new
                { id = component.Id, status = component.Status, detail = component.Detail, reason_code = component.ReasonCode }),
                capabilities = new { slide_navigation = true, effect_navigation = true, media_pause = false, media_resume = false, media_seek = false, media_volume = false, media_mute = false },
            },
        });
    }
}
