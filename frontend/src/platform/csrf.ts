/** 登录缓存优先，fallback按请求协议隔离，兼容Electron安全Cookie。SCP-cv 2026-10-12。 */
/** :param cachedToken: 当前会话缓存。:param cookieToken: 对应协议Cookie。:returns: 实际防伪令牌。 */
export function resolveCsrfToken(cachedToken: string, cookieToken: string): string {
  return cachedToken || cookieToken;
}

/** :param requestUrl: 实际API目标或相对路由。:param pageHref: 当前页面。:returns: 目标协议对应Cookie名称，不读取另一协议令牌。 */
export function csrfCookieName(requestUrl: string, pageHref: string): string {
  return new URL(requestUrl || pageHref, pageHref).protocol === 'http:' ? 'csrftoken_http' : 'csrftoken';
}
