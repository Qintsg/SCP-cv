// 文件描述：只读检查真实发布与接收，不模拟输入。项目：SCP-cv；文件：verify-media-sharing-real.mjs；作者：Qintsg；日期：2026-10-30。
import { createRequire } from 'node:module';
import { pathToFileURL } from 'node:url';
import { mkdir, readFile, writeFile } from 'node:fs/promises';
import { join, resolve } from 'node:path';
import assert from 'node:assert/strict';
import { spawn } from 'node:child_process';
const root = resolve('.'), frontend = join(root, 'frontend');
const require = createRequire(join(frontend, 'package.json'));
const { createServer } = await import(pathToFileURL(require.resolve('vite')));
const { default: vue } = await import(pathToFileURL(require.resolve('@vitejs/plugin-vue')));
const modulePath = process.env.SCP_PLAYWRIGHT_MODULE, executablePath = process.env.SCP_BROWSER;
if (!modulePath || !executablePath) throw new Error('需要SCP_PLAYWRIGHT_MODULE与SCP_BROWSER的实际路径。');
const { chromium, request } = await import(pathToFileURL(modulePath));
const backend = process.env.SCP_MEDIA_BACKEND, password = process.env.SCP_MEDIA_PASSWORD;
if (!backend || !password) throw new Error('真实主机和账号参数缺失。');
await mkdir(join(root, '.validation/media-030'), { recursive: true });
const mediaPath = join(root, '.validation/media-030/share-input-' + Date.now() + '.mp4');
const encoder = spawn(process.env.SCP_FFMPEG || 'ffmpeg', ['-nostdin', '-hide_banner', '-loglevel', 'error', '-f', 'lavfi', '-i', 'testsrc2=size=640x360:rate=25', '-t', '5', '-an', '-c:v', 'libx264', '-preset', 'ultrafast', '-g', '10', mediaPath], { windowsHide: true, shell: false, stdio: ['ignore', 'ignore', 'pipe'] });
let encoderError = ''; encoder.stderr.on('data', bytes => { encoderError += bytes; });
const code = await new Promise((resolveExit, reject) => { encoder.once('error', reject); encoder.once('exit', resolveExit); });
assert.equal(code, 0, encoderError); const video = await readFile(mediaPath);
const server = await createServer({ configFile: false, root: frontend, mode: 'web', plugins: [vue(), { name: 'actual-share-page', configureServer(server) { server.middlewares.use((req, res, next) => {
  if (req.url === '/actual-video.mp4') { res.setHeader('Content-Type', 'video/mp4'); res.end(video); return; }
  if (req.url !== '/share.html') return next(); res.setHeader('Content-Type', 'text/html'); res.end('<!doctype html><html><body><div id="app"></div><script type="module" src="/@fs/' + root.replaceAll('\\', '/') + '/frontend/scripts/media-sharing-real/probe.ts"></script></body></html>');
}); } }], resolve: { alias: { '@': join(frontend, 'src') }, dedupe: ['vue', 'pinia', 'vue-router'] }, server: { host: '127.0.0.1', port: 19242, strictPort: true, fs: { allow: [root] }, proxy: { '/api': { target: backend, changeOrigin: false } } }, logLevel: 'error' });
let browser, http;
try {
  await server.listen(); const origin = 'http://127.0.0.1:19242';
  http = await request.newContext({ baseURL: origin }); assert.ok((await http.get('/api/auth/csrf/')).ok());
  assert.ok((await http.post('/api/auth/login/', { data: { username: 'window-admin', password } })).ok());
  browser = await chromium.launch({ executablePath, headless: true, args: ['--no-proxy-server'],
    ignoreDefaultArgs: ['--disable-background-timer-throttling', '--disable-backgrounding-occluded-windows', '--disable-renderer-backgrounding'] });
  const context = await browser.newContext({ storageState: await http.storageState() }); const page = await context.newPage(); const errors = [];
  page.on('pageerror', error => errors.push(error.message)); await page.goto(origin + '/share.html', { waitUntil: 'domcontentloaded' });
  await page.waitForFunction(() => window.share030?.checks.length > 0, null, { timeout: 30000 });
  const other = await context.newPage(); await other.goto('about:blank'); await other.bringToFront();
  const background = await page.evaluate(() => document.visibilityState);
  await page.waitForFunction(() => window.share030?.done === true, null, { timeout: 100000 });
  await page.bringToFront(); const foreground = await page.evaluate(() => document.visibilityState);
  const result = await page.evaluate(() => window.share030); await writeFile(join(root, '.validation/media-030/share-result.json'), JSON.stringify({ result, errors, background, foreground }, null, 2));
  assert.deepEqual(errors, []); assert.equal(result.error, ''); assert.equal(result.checks.length, 10); assert.equal(result.phase, 'closed');
  console.log(JSON.stringify(result));
} finally { await browser?.close(); await http?.dispose(); await server.close(); }
