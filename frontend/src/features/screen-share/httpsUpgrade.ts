/** HTTP共享仅发现与导航：不携带秘密、不猜端口、不调用采集。SCP-cv 2026-10-12。 */
const SECRET_KEY = /(?:token|password|passwd|secret|session|csrf|authorization|cap|credential)/i;
const SECRET_ASSIGN = /(?:token|password|passwd|secret|session|csrf|authorization|cap|credential)[\w-]*\s*[:=]/i;

/** :param href: 当前HTTP页面。:param info: 无秘密入口发现结果。:returns: 同主机HTTPS地址，保留合法位置且移除敏感参数。 */
export function buildHttpsPage(href: string, info: unknown): string {
  const page = new URL(href);
  if (page.protocol !== 'http:' || page.username || page.password) throw new Error('share_https_unavailable');
  if (!info || typeof info !== 'object' || Array.isArray(info)) throw new Error('share_https_unavailable');
  const entry = info as Record<string, unknown>;
  const port = entry.httpsPort;
  if (entry.version !== 1 || Object.keys(entry).sort().join(',') !== 'httpsPort,version'
    || !Number.isInteger(port) || Number(port) < 1 || Number(port) > 65535) throw new Error('share_https_unavailable');
  page.protocol = 'https:'; page.port = Number(port) === 443 ? '' : String(port);
  for (const [key, value] of [...page.searchParams]) {
    // 包含嵌套redirect秘密也不能随导航复制，普通路由参数保持。
    if (SECRET_KEY.test(key) || SECRET_ASSIGN.test(value)) page.searchParams.delete(key);
  }
  if (SECRET_ASSIGN.test(decodeURIComponent(page.hash))) page.hash = '';
  return page.href;
}

/** :param href: 当前HTTP页面。:param signal: 账号/主机/页面寿命及超时取消。:param request: 可测试的只读请求。:returns: 已核协议/端口的同主机URL，不允许发现重定向。 */
export async function discoverHttpsPage(href: string, signal: AbortSignal, request: typeof fetch = fetch): Promise<string> {
  try {
    const response = await request(new URL('/.well-known/scp-cv-access', href).href, {
      method: 'GET', credentials: 'omit', redirect: 'error', cache: 'no-store', signal,
    });
    if (!response.ok) throw new Error('share_https_unavailable');
    return buildHttpsPage(href, await response.json());
  } catch { throw new Error('share_https_unavailable'); }
}
