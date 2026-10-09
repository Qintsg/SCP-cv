// 上传专用有限大小与表单解析错误边界，普通JSON请求不放宽、不绕过身份和CSRF。
using Microsoft.AspNetCore.Http.Features;

namespace ScpCv.ControlHost.Endpoints;

public static partial class MediaEndpoints
{
    private const long MaximumUploadRequestBytes = 1024L * 1024 * 1024;

    /// <summary>:param request: 已通过认证/CSRF的上传请求。:param token: 请求寿命。:returns: 有效表单或明确400/413，不登记半份媒体。</summary>
    private static async Task<(IFormCollection? Form, IResult? Error)> ReadUploadFormAsync(HttpRequest request, CancellationToken token)
    {
        if (request.ContentLength > MaximumUploadRequestBytes) return (null, UploadTooLarge());
        var maximum = request.HttpContext.Features.Get<IHttpMaxRequestBodySizeFeature>();
        if (maximum is { IsReadOnly: false }) maximum.MaxRequestBodySize = MaximumUploadRequestBytes;
        request.HttpContext.Features.Set<IFormFeature>(new FormFeature(request,
            new FormOptions { MultipartBodyLengthLimit = MaximumUploadRequestBytes }));
        try
        {
            return (await request.ReadFormAsync(token).ConfigureAwait(false), null);
        }
        catch (BadHttpRequestException exception) when (exception.StatusCode == StatusCodes.Status413PayloadTooLarge)
        {
            return (null, UploadTooLarge());
        }
        catch (InvalidDataException)
        {
            return (null, ApiEndpointSupport.Error("上传表单格式无效，请重新选择文件后上传。", "invalid_upload_form"));
        }
    }

    /// <summary>:returns: 只报告有限请求大小，不打印上传路径或内容。</summary>
    private static IResult UploadTooLarge() => ApiEndpointSupport.Error("上传请求超过1GiB上限，请缩小文件后重试。", "upload_too_large", StatusCodes.Status413PayloadTooLarge);
}
