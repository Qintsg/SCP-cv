/** 双网页入口及按请求协议分流；无自动降级/随机端口/证书暴露。SCP-cv 2026-10-12。 */
import { createServer, type ServerOptions, type Server } from 'node:https';
import type { TLSSocket } from 'node:tls';
import type { Plugin, PreviewServer, ProxyOptions, ViteDevServer } from 'vite';

export const ACCESS_PATH = '/.well-known/scp-cv-access';

/** :param value: 配置值。:param fallback: 默认端口。:returns: 严格的1..65535整数；不接受部分数字。 */
export function accessPort(value: string | undefined, fallback: number): number {
  if (!value?.trim()) return fallback;
  if (!/^\d+$/.test(value.trim())) throw new Error('网页端口必须是1–65535的整数。');
  const port = Number(value);
  if (!Number.isInteger(port) || port < 1 || port > 65535) throw new Error('网页端口必须是1–65535的整数。');
  return port;
}

/** :param value: 主机origin。:returns: 无协议默认HTTP，拒绝凭据及额外路径的origin。 */
function upstream(value: string): URL {
  const raw = value.trim();
  let url: URL;
  try { url = new URL(/^[a-z][\w+.-]*:\/\//i.test(raw) ? raw : `http://${raw}`); }
  catch { throw new Error('网页代理上游地址无效。'); }
  if (!['http:', 'https:'].includes(url.protocol) || !raw || raw.startsWith('//') || url.username || url.password
    || url.pathname !== '/' || url.search || url.hash) throw new Error('网页代理上游须为不含凭据/路径的HTTP或HTTPS主机地址。');
  return url;
}

/** :param env: 服务端环境。:returns: 匹配入口协议的上游；明确单协议配置只覆盖对应侧。 */
export function backendTargets(env: Record<string, string | undefined>): { http: string; https: string } {
  const result = { http: 'http://127.0.0.1:18444', https: 'https://127.0.0.1:18443' };
  if (env.VITE_BACKEND_TARGET?.trim()) {
    const url = upstream(env.VITE_BACKEND_TARGET);
    result[url.protocol === 'https:' ? 'https' : 'http'] = url.origin;
  }
  for (const protocol of ['http', 'https'] as const) {
    const value = env[`VITE_BACKEND_${protocol.toUpperCase()}_TARGET`]?.trim();
    if (!value) continue;
    const url = upstream(value);
    if (url.protocol !== `${protocol}:`) throw new Error('网页入口与其代理上游协议必须一致。');
    result[protocol] = url.origin;
  }
  return result;
}

/** :param targets: 分协议上游。:returns: 每请求副本分流，不修改Vite共享options；HTTPS始终验证。 */
export function protocolProxy(targets: ReturnType<typeof backendTargets>): ProxyOptions {
  return {
    target: targets.http, changeOrigin: false, secure: true,
    configure(proxy) {
      const select = (req: { socket: unknown }, _res: unknown, options: Pick<ProxyOptions, 'target' | 'secure'>): void => {
        const encrypted = Boolean((req.socket as TLSSocket).encrypted);
        options.target = new URL(encrypted ? targets.https : targets.http);
        options.secure = true;
      };
      proxy.before('web', 'stream', select);
      proxy.before('ws', 'stream', select);
    },
  };
}

/** :param tls: 已通过内容/路径校验的证书。:param httpsPort: 固定安全入口端口。:returns: dev/preview共用的发现与TLS监听插件。 */
export function dualProtocolPlugin(tls: ServerOptions | undefined, httpsPort = 5174): Plugin {
  let companion: Server | undefined;
  let closing: Promise<void> | undefined;
  let secureListening = false;
  /** :returns: 仅关闭本插件拥有的HTTPS监听及连接。 */
  function dispose(): Promise<void> {
    secureListening = false;
    if (!companion) return Promise.resolve();
    if (!closing) closing = new Promise(resolve => {
      companion!.close(() => resolve());
      companion!.closeAllConnections();
    });
    return closing;
  }
  /** :param server: Vite正式HTTP服务。:param primaryPort: 主端口。:param host: 当前dev/preview绑定地址。:returns: 同范围发现及TLS监听。 */
  async function configure(server: ViteDevServer | PreviewServer, primaryPort: number, host: string | boolean | undefined): Promise<void> {
    if (tls && primaryPort === httpsPort) throw new Error('HTTP与HTTPS网页入口必须使用不同端口。');
    server.middlewares.use((req, res, next) => {
      if (req.url?.split('?')[0] !== ACCESS_PATH) return next();
      res.setHeader('Cache-Control', 'no-store');
      res.setHeader('X-Content-Type-Options', 'nosniff');
      res.setHeader('Content-Type', 'application/json; charset=utf-8');
      if (req.method !== 'GET') {
        res.statusCode = 405; res.setHeader('Allow', 'GET'); res.end(); return;
      }
      res.end(JSON.stringify({ version: 1, httpsPort: secureListening ? httpsPort : null }));
    });
    if (!tls) return;
    if (!server.httpServer) throw new Error('双协议网页入口不支持无HTTP服务器的middleware模式。');
    companion = createServer(tls, server.middlewares);
    // 使用Node公开事件接口把TLS socket交给Vite已有HMR/proxy升级处理，不访问私有ws对象。
    companion.on('upgrade', (request, socket, head) => server.httpServer!.emit('upgrade', request, socket, head));
    server.httpServer.once('close', () => { void dispose(); });
    server.httpServer.once('error', () => { void dispose(); });
    try {
      await new Promise<void>((resolve, reject) => {
        companion!.once('error', reject);
        companion!.listen(httpsPort, typeof host === 'string' ? host : host ? '0.0.0.0' : 'localhost', () => {
          companion!.removeListener('error', reject); resolve();
        });
      });
      secureListening = true;
      server.config.logger.info(`  HTTPS 网页入口同时监听端口 ${httpsPort}（HTTP入口保持可用）。`);
    } catch {
      await dispose();
      throw new Error('HTTPS网页入口无法监听：请检查配置端口是否被占用。');
    }
  }
  return {
    name: 'scp-cv-dual-protocol',
    configureServer: server => configure(server, server.config.server.port ?? 5173, server.config.server.host),
    configurePreviewServer: server => configure(server, server.config.preview.port ?? 4173, server.config.preview.host),
    closeBundle: dispose,
  };
}
