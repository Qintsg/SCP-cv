// 文件描述：真实本机主机、转换器、MediaMTX及只读浏览器验收。项目：SCP-cv；文件：verify-media-preview-real.mjs；作者：Qintsg；日期：2026-10-30。
import { spawn } from 'node:child_process';
import { mkdir, readFile, writeFile } from 'node:fs/promises';
import { randomBytes } from 'node:crypto';
import { createServer as socket } from 'node:net';
import { join, resolve } from 'node:path';
import { pathToFileURL } from 'node:url';
import assert from 'node:assert/strict';
import { setTimeout as delay } from 'node:timers/promises';
const root = resolve('.'), directory = join(root, '.validation/media-030/run-' + Date.now()); await mkdir(directory, { recursive: true });
const { createServer } = await import(pathToFileURL(join(root, 'frontend/node_modules/vite/dist/node/index.js')));
const { default: vue } = await import(pathToFileURL(join(root, 'frontend/node_modules/@vitejs/plugin-vue/dist/index.mjs')));
const { default: tailwind } = await import(pathToFileURL(join(root, 'frontend/node_modules/@tailwindcss/vite/dist/index.mjs')));
if (!process.env.SCP_PLAYWRIGHT_MODULE || !process.env.SCP_BROWSER || !process.env.SCP_MEDIA_PPTX || !process.env.SCP_MEDIA_PDF) throw new Error('需要实际Playwright、浏览器、PPTX和PDF路径。');
const { chromium, request } = await import(pathToFileURL(process.env.SCP_PLAYWRIGHT_MODULE));
const children = [], logs = new Map(), report = { directory, observations: [], errors: [], requests: [] };
let vite, browser, http;
/** :param executable: 实际工具。:param args: 结构化参数。:param env: 进程设置。:returns: 自有子进程与退出凭据。 */
function start(executable, args, env = {}) {
  const child = spawn(executable, args, { cwd: root, windowsHide: true, shell: false, stdio: ['pipe', 'pipe', 'pipe'], env: { ...process.env, ...env } });
  let text = ''; const exited = new Promise((resolveExit, reject) => { child.once('error', reject); child.once('exit', (code, signal) => resolveExit({ code, signal })); });
  child.stdout.on('data', bytes => { text += bytes; }); child.stderr.on('data', bytes => { text += bytes; });
  const own = { child, exited, text: () => text }; children.push(own); return own;
}
/** :param args: 实际编码参数。:returns: 编码器成功退出。 */
async function ffmpeg(args) { const own = start('E:/Data/Dev/Scoop/shims/ffmpeg.exe', ['-nostdin', '-hide_banner', '-loglevel', 'error', '-y', ...args]); assert.equal((await own.exited).code, 0, own.text()); }
/** :param check: 实际就绪条件。:param timeout: 上限。:returns: 满足条件。 */
async function wait(check, timeout = 30000) { const end = Date.now() + timeout; while (Date.now() < end) { if (await check()) return; await delay(200); } throw new Error('实际资源等待超时。'); }
try {
  for (const port of [19231, 19232, 19233, 19234, 19235, 19236]) { const server = socket(); await new Promise((ok, fail) => { server.once('error', fail); server.listen(port, '127.0.0.1', ok); }); await new Promise(ok => server.close(ok)); }
  await ffmpeg(['-f', 'lavfi', '-i', 'testsrc2=size=640x360:rate=25', '-t', '5', '-an', '-c:v', 'libx264', '-preset', 'ultrafast', '-pix_fmt', 'yuv420p', '-g', '10', join(directory, 'video.mp4')]);
  await ffmpeg(['-f', 'lavfi', '-i', 'sine=frequency=440:sample_rate=48000', '-t', '3', '-c:a', 'pcm_s16le', join(directory, 'audio.wav')]);
  start(join(root, 'tools/third_party/mediamtx/mediamtx.exe'), [join(root, '.validation/media-030/mediamtx.yml')]);
  await wait(async () => { try { return (await fetch('http://127.0.0.1:19233/v3/paths/list')).ok; } catch { return false; } });
  start('E:/Data/Dev/Scoop/shims/ffmpeg.exe', ['-hide_banner', '-loglevel', 'error', '-re', '-stream_loop', '-1', '-i', join(directory, 'video.mp4'), '-an', '-c:v', 'copy', '-f', 'rtsp', '-rtsp_transport', 'tcp', 'rtsp://127.0.0.1:19234/live']);
  const password = randomBytes(24).toString('hex');
  start(join(root, '.validation/dotnet-sdk-026/dotnet.exe'), [join(root, 'runtime-dotnet/src/ScpCv.ControlHost/bin/Release/net10.0/ScpCv.ControlHost.dll'), '--contentRoot', join(root, 'runtime-dotnet/src/ScpCv.ControlHost')], {
    DOTNET_ROOT: join(root, '.validation/dotnet-sdk-026'), ASPNETCORE_URLS: 'http://127.0.0.1:19231', ASPNETCORE_ENVIRONMENT: 'Testing',
    DataRoot__RootPath: join(directory, 'data'), SafetyMode: 'Hardware', Authentication__CrossSiteCookies: 'false',
    Authentication__DevelopmentAccount__Username: 'media-admin', Authentication__DevelopmentAccount__Password: password,
    Authentication__DevelopmentAccount__IsStaff: 'true', Authentication__DevelopmentAccount__IsSuperuser: 'true',
    StreamPreview__ApiBase: 'http://127.0.0.1:19233/', StreamPreview__LocalRtspPort: '19234', StreamPreview__FfmpegPath: 'E:/Data/Dev/Scoop/shims/ffmpeg.exe',
    Logging__LogLevel__Default: 'Warning',
  });
  await wait(async () => { try { return (await fetch('http://127.0.0.1:19231/health/live')).ok; } catch { return false; } });
  vite = await createServer({ configFile: false, root: join(root, 'frontend'), mode: 'web', plugins: [vue(), tailwind(), { name: 'actual-media-check', configureServer(server) {
    server.middlewares.use((req, res, next) => {
      if (req.url === '/web-content.html') { res.setHeader('Content-Type', 'text/html'); res.end('<!doctype html><html lang="zh-CN"><meta charset="utf-8"><body><h1>真实网页预览</h1><p>本机HTTP页面</p></body></html>'); return; }
      if (!req.url.startsWith('/qa-media.html')) return next(); res.setHeader('Content-Type', 'text/html'); res.end('<!doctype html><html lang="zh-CN"><meta name="viewport" content="width=device-width,initial-scale=1"><body><div id="app"></div><script type="module" src="/@fs/' + root.replaceAll('\\', '/') + '/frontend/scripts/media-preview-real/probe.ts"></script></body></html>'); });
  } }], resolve: { alias: { '@': join(root, 'frontend/src') }, dedupe: ['vue', 'pinia', 'vue-router', 'ant-design-vue'] }, server: { host: '127.0.0.1', port: 19232, strictPort: true, fs: { allow: [root] }, proxy: { '/api': { target: 'http://127.0.0.1:19231', changeOrigin: false } } }, logLevel: 'error' });
  await vite.listen(); const origin = 'http://127.0.0.1:19232';
  http = await request.newContext({ baseURL: origin }); assert.ok((await http.get('/api/auth/csrf/')).ok());
  assert.ok((await http.post('/api/auth/login/', { data: { username: 'media-admin', password } })).ok());
  const csrf = (await (await http.get('/api/auth/csrf/')).json()).csrfToken;
  const headers = { Origin: origin, 'X-CSRFToken': csrf };
  /** :param path: 正式API。:param data: 实际输入。:returns: 成功的原始响应。 */
  async function post(path, data) { const response = await http.post(path, { data, headers }); assert.ok(response.ok(), path + ': ' + await response.text()); return response.json(); }
  const sources = [];
  for (const [path, name, type] of [
    [join(root, 'frontend/src/assets/sspu-ailab-transparent.png'), '图片预览', 'image/png'],
    [join(directory, 'video.mp4'), '视频预览', 'video/mp4'], [join(directory, 'audio.wav'), '音频预览', 'audio/wav'],
    [resolve(process.env.SCP_MEDIA_PPTX), '文稿预览', 'application/vnd.openxmlformats-officedocument.presentationml.presentation'],
    [resolve(process.env.SCP_MEDIA_PDF), 'PDF预览', 'application/pdf'],
  ]) {
    const response = await http.post('/api/sources/upload/', { headers, multipart: { name, preheat_enabled: 'false', file: { name: path.split(/[\\/]/).at(-1), mimeType: type, buffer: await readFile(path) } } });
    assert.ok(response.ok(), name + ': ' + await response.text()); sources.push((await response.json()).source);
  }
  sources.push((await post('/api/sources/web/', { name: '网页预览', url: origin + '/web-content.html' })).source);
  sources.push((await post('/api/sources/streams/', { name: 'RTSP预览', source_type: 'rtsp_stream', url: 'rtsp://127.0.0.1:19234/live' })).source);
  sources.push((await post('/api/sources/streams/', { name: 'RTMP预览', source_type: 'custom_stream', url: 'rtmp://127.0.0.1:19235/live' })).source);
  sources.push((await post('/api/sources/streams/', { name: 'SRT预览', source_type: 'srt_stream', url: 'srt://127.0.0.1:19236?streamid=read:live' })).source);
  const ppt = sources.find(source => source.name === '文稿预览');
  await wait(async () => { const list = await (await http.get(`/api/sources/${ppt.id}/ppt-resources/`)).json(); return list.resources.filter(page => page.slide_image).length > 1; }, 180000);
  browser = await chromium.launch({ executablePath: process.env.SCP_BROWSER, headless: true, args: ['--no-proxy-server'] });
  for (const width of [390, 800, 1440]) {
    const context = await browser.newContext({ storageState: await http.storageState(), viewport: { width, height: width === 390 ? 844 : 1000 }, hasTouch: width < 1000 });
    try {
      for (const source of sources) {
        const page = await context.newPage(); page.on('pageerror', error => report.errors.push(error.message));
        page.on('request', req => { if (req.method() !== 'GET' && req.url().includes('/api/')) report.requests.push({ method: req.method(), path: new URL(req.url()).pathname }); });
        await page.goto(origin + `/qa-media.html?id=${source.id}&page=2`, { waitUntil: 'domcontentloaded' });
        await page.waitForFunction(() => window.media030?.ready === true);
        await page.locator('.source-preview').waitFor();
        if (source.name === '图片预览' || source.name === '文稿预览') await page.waitForFunction(() => { const image = document.querySelector('.source-preview img'); return image?.complete && image.naturalWidth > 0; });
        if (source.name === '文稿预览') await page.waitForFunction(() => document.querySelector('.presentation-preview__controls')?.textContent.includes('2 /'));
        if (source.name === 'PDF预览') await page.waitForFunction(() => { const canvas = document.querySelector('.pdf-preview canvas'); return canvas?.width > 0 && document.querySelector('.pdf-preview__controls')?.textContent.includes('2 /'); });
        if (source.name === '文稿预览' || source.name === 'PDF预览') await page.waitForFunction(() => !document.querySelector('.presentation-preview__controls button,.pdf-preview__controls button')?.disabled);
        if (source.name === '视频预览' || source.name === '音频预览') await page.waitForFunction(() => { const media = document.querySelector('.source-preview video,.source-preview audio'); return media?.readyState >= 1 && media.duration > 0; });
        if (source.name.includes('RTSP') || source.name.includes('RTMP') || source.name.includes('SRT')) await page.locator('[data-preview-state="live"]').waitFor({ timeout: 40000 });
        const observed = await page.evaluate(() => ({ width: innerWidth, overflow: document.documentElement.scrollWidth > innerWidth, checks: window.media030.checks.length, name: window.media030.name, diagnostics: window.media030.diagnostics(), error: document.querySelector('.source-preview__error,.pdf-preview__error,.presentation-preview__error')?.textContent }));
        assert.equal(observed.overflow, false); assert.equal(observed.checks, 10); assert.equal(observed.error, '');
        await page.screenshot({ path: join(directory, `${source.id}-${width}.png`) }); report.observations.push(observed);
        await writeFile(join(directory, 'report.json'), JSON.stringify(report, null, 2));
        await page.close();
      }
    } finally { await context.close(); }
  }
  assert.deepEqual(report.errors, []); assert.ok(report.requests.every(req => req.path.startsWith('/api/stream-previews/')));
  await wait(async () => { const status = await (await http.get('/api/stream-previews/status/')).json(); report.previewAfter = status; return status.upstream_count === 0 && status.lease_count === 0; }, 35000);
  console.log(JSON.stringify({ directory, pages: report.observations.length, errors: report.errors.length }));
} catch (failure) { report.failure = failure instanceof Error ? failure.message : String(failure); throw failure; }
finally {
  await browser?.close(); await http?.dispose(); await vite?.close();
  for (const own of children.reverse()) { if (own.child.exitCode === null && own.child.signalCode === null) own.child.kill(); await own.exited; logs.set(String(own.child.pid), own.text()); }
  for (const [pid, text] of logs) await writeFile(join(directory, `process-${pid}.log`), text);
  await writeFile(join(directory, 'report.json'), JSON.stringify(report, null, 2));
}
