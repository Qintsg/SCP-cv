// 可选共享心跳仅允许有界JSON；分块/未知长度请求同样先限制字节再分配别名对象。
using System.Text.Json;
using Microsoft.AspNetCore.Http.Features;

namespace ScpCv.ControlHost.StreamPreview;

public static class PreviewHeartbeatBody
{
    private const int MaximumBytes = 4096;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { MaxDepth = 8 };

    /// <summary>:param request: 已经过认证的固定心跳。:param cancellationToken: 原请求取消。:returns: 无body保持旧语义；非法/超限明确400，不续租。</summary>
    public static async Task<PreviewHeartbeatRequest?> ReadAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        var hasBody = request.HttpContext.Features.Get<IHttpRequestBodyDetectionFeature>()?.CanHaveBody
            ?? (request.ContentLength is > 0 || request.Headers.TransferEncoding.Count > 0);
        if (!hasBody) return null;
        if (request.ContentLength > MaximumBytes) throw Invalid();
        var bytes = new byte[MaximumBytes + 1]; var used = 0;
        while (used < bytes.Length)
        {
            var read = await request.Body.ReadAsync(bytes.AsMemory(used), cancellationToken).ConfigureAwait(false);
            if (read == 0) break; used += read;
        }
        if (used > MaximumBytes) throw Invalid();
        try { return JsonSerializer.Deserialize<PreviewHeartbeatRequest>(bytes.AsSpan(0, used), Json) ?? throw Invalid(); }
        catch (JsonException) { throw Invalid(); }
    }

    private static PreviewException Invalid() => new("预览别名列表无效", "invalid_preview_aliases", 400);
}
