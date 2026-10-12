// 会话Cookie按入口协议分名，保持框架分块/读写/注销规则及HTTPS保护。
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;

namespace ScpCv.Infrastructure.Auth;

public sealed class ProtocolCookieManager : ICookieManager
{
    public const string TicketProtocolKey = "scp-cv:cookie-protocol";
    private readonly ChunkingCookieManager _inner = new();

    /// <summary>读取当前协议Cookie，不回退到另一协议名称。</summary>
    public string? GetRequestCookie(HttpContext context, string key) =>
        _inner.GetRequestCookie(context, Name(context, key));

    /// <summary>写当前协议Cookie；HTTP仅同站使用，不影响HTTPS跨站配置。</summary>
    public void AppendResponseCookie(HttpContext context, string key, string? value, CookieOptions options) =>
        _inner.AppendResponseCookie(context, Name(context, key), value, ForProtocol(context, options));

    /// <summary>只删除当前协议Cookie，包括框架分块。</summary>
    public void DeleteCookie(HttpContext context, string key, CookieOptions options) =>
        _inner.DeleteCookie(context, Name(context, key), ForProtocol(context, options));

    /// <summary>复制HTTP选项，避免修改框架共享配置；HTTPS保持Secure及原SameSite。</summary>
    private static CookieOptions ForProtocol(HttpContext context, CookieOptions options) =>
        context.Request.IsHttps ? options : new CookieOptions(options) { Secure = false, SameSite = SameSiteMode.Lax };

    /// <summary>HTTPS沿用旧名称，HTTP显式分离。</summary>
    public static string Name(HttpContext context, string key) => context.Request.IsHttps ? key : $"{key}_http";
}
