/** 可选用户本机预览桥：127.0.0.1固定端口、Origin/Host/随机token门禁，不运行于D4服务组。 */
import { createServer } from 'node:http';
import { randomBytes, timingSafeEqual } from 'node:crypto';
import { pathToFileURL } from 'node:url';
import { createPreviewPool } from './preview-core.mjs';

/** :param options: 明确允许的网页来源/临时令牌/本机FFmpeg。:returns: 未监听服务器，调用方负责关闭。 */
export function createPreviewServer({ origins, token, pool = createPreviewPool(), port = 18765 }) {
  const allowed = new Set(origins.map(value => {
    const url = new URL(value);
    if (!['http:', 'https:'].includes(url.protocol) || url.username || url.password || url.pathname !== '/' || url.search || url.hash)
      throw new Error('只允许完整的HTTP/HTTPS网页Origin，不含路径或凭据');
    return url.origin;
  }));
  const expected = Buffer.from(`Bearer ${token}`);
  const server = createServer(async (request, response) => {
    response.setHeader('Cache-Control', 'no-store'); response.setHeader('X-Content-Type-Options', 'nosniff');
    const origin = request.headers.origin;
    if (request.headers.host !== `127.0.0.1:${port}` || !origin || !allowed.has(origin)) { response.writeHead(403).end(); return; }
    response.setHeader('Access-Control-Allow-Origin', origin); response.setHeader('Vary', 'Origin');
    if (request.method === 'OPTIONS') {
      response.setHeader('Access-Control-Allow-Methods', 'POST,GET,DELETE');
      response.setHeader('Access-Control-Allow-Headers', 'Authorization,Content-Type');
      response.setHeader('Access-Control-Allow-Private-Network', 'true'); response.writeHead(204).end(); return;
    }
    const auth = Buffer.from(request.headers.authorization || '');
    if (auth.length !== expected.length || !timingSafeEqual(auth, expected)) { response.writeHead(401).end(); return; }
    const match = /^\/sessions\/([a-f0-9]{32})(\/frame)?$/.exec(request.url || '');
    try {
      if (request.method === 'POST' && request.url === '/sessions') {
        if (!request.headers['content-type']?.startsWith('application/json')) throw new Error('invalid_body');
        const chunks = []; let length = 0;
        for await (const chunk of request) { length += chunk.length; if (length > 8192) throw new Error('invalid_body'); chunks.push(chunk); }
        const input = JSON.parse(Buffer.concat(chunks).toString('utf8')); const id = await pool.open(input.url, input.quality);
        if (response.destroyed) { pool.release(id); return; }
        response.writeHead(201, { 'Content-Type': 'application/json' }).end(JSON.stringify({ id })); return;
      }
      if (request.method === 'GET' && match?.[2]) { const frame = pool.frame(match[1]); response.writeHead(200, { 'Content-Type': 'image/jpeg' }).end(frame); return; }
      if (request.method === 'DELETE' && match && !match[2]) { pool.release(match[1]); response.writeHead(204).end(); return; }
      response.writeHead(404).end();
    } catch {
      if (!response.headersSent) response.writeHead(503, { 'Content-Type': 'application/json' });
      response.end(JSON.stringify({ error: '本机预览不可用，请检查流与本机服务' }));
    }
  });
  server.requestTimeout = 15000; server.headersTimeout = 5000;
  const cleanup = setInterval(() => pool.sweep(), 5000); cleanup.unref();
  server.once('close', () => { clearInterval(cleanup); pool.close(); }); return server;
}

if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
  const origins = process.argv.slice(2).filter(arg => arg.startsWith('--origin=')).map(arg => arg.slice(9));
  if (!origins.length) { process.stderr.write('请使用 --origin=https://控制台地址 明确允许的网页来源；本服务只在用户本机启动。\n'); process.exitCode = 1; }
  else {
    const token = randomBytes(24).toString('hex');
    const server = createPreviewServer({ origins, token, pool: createPreviewPool({ ffmpeg: process.env.SCP_PREVIEW_FFMPEG || 'ffmpeg' }) });
    server.listen(18765, '127.0.0.1', () => process.stdout.write(`本机预览已启动（无声），仅允许指定来源。\n临时连接码：${token}\n在控制台 设置→运行态→本机预览 中填入；退出此进程即失效。\n`));
    server.once('error', () => { process.stderr.write('本机预览端口不可用。\n'); process.exitCode = 1; });
    const close = () => { server.closeAllConnections(); server.close(); };
    process.once('SIGINT', close); process.once('SIGTERM', close);
  }
}
