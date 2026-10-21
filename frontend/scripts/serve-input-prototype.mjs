/** 输入与预览原型的回环入口；没有后端代理，不加载生产鉴权或设备控制。 */
import { fileURLToPath } from 'node:url';
import { resolve } from 'node:path';
import { createServer } from 'vite';
import vue from '@vitejs/plugin-vue';

const root = fileURLToPath(new URL('..', import.meta.url));
const entry = '/src/features/runtime/wall/prototype/index.html';
const server = await createServer({
  configFile: false, envDir: false, root, appType: 'mpa', plugins: [vue(), { name: 'prototype-entry', configureServer(instance) {
    instance.middlewares.use((request, response, next) => {
      const url = new URL(request.url ?? '/', 'http://127.0.0.1:18895');
      if (url.pathname === '/') { response.statusCode = 302; response.setHeader('Location', entry + (url.search || '?variant=A')); response.end(); return; }
      if (/^\/(api|events|media|admin)(\/|$)/.test(url.pathname)) { response.statusCode = 404; response.end('Prototype: no backend'); return; }
      next();
    });
  } }],
  resolve: { alias: { '@': resolve(root, 'src') } },
  server: { host: '127.0.0.1', port: 18895, strictPort: true, open: false,
    headers: { 'Content-Security-Policy': "default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self' data:; media-src 'self' blob:; connect-src 'self' ws://127.0.0.1:18895; font-src 'self' data:; object-src 'none'; base-uri 'none'" } },
});
await server.listen();
console.log('输入与预览 Demo：http://127.0.0.1:18895/?variant=A（仅本机，无设备连接）');
/** :returns: 只关闭本次原型服务器。 */
const close = async () => { await server.close(); process.exit(0); };
process.once('SIGINT', close); process.once('SIGTERM', close);
