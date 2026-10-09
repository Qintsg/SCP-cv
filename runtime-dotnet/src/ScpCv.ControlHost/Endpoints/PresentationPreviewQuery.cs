// 两个只读文稿预览入口共用严格布局参数；禁止非法或多值查询静默回退。
namespace ScpCv.ControlHost.Endpoints;

internal static class PresentationPreviewQuery
{
    /// <summary>:param request: 当前HTTP请求。:param fitText: 明确布局。:returns: 单值true/false或缺省时为true。</summary>
    internal static bool TryRead(HttpRequest request, out bool fitText)
    {
        fitText = false;
        if (!request.Query.TryGetValue("fit_text", out var values)) return true;
        if (values.Count != 1) return false;
        if (values[0] == "true") { fitText = true; return true; }
        return values[0] == "false";
    }

    internal static IResult Invalid() => ApiEndpointSupport.Error(
        "fit_text 必须为单值 true 或 false。", "invalid_fit_text", StatusCodes.Status400BadRequest);
}
