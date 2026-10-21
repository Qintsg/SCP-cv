/** 实际Electron44.2只读媒体探针：私有session/app协议/正式Main与preload，仅合成本机输入。 */
import { app, BrowserWindow, protocol, session } from 'electron';
import { createServer } from 'node:https';
import { readFile, writeFile } from 'node:fs/promises';
import { resolve, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { randomUUID } from 'node:crypto';
import { setupNativePreview } from '../../electron-dist/preview/mainPreview.js';
import { PACKAGED_CSP, isTrustedRendererUrl } from '../../electron-dist/security.js';

const root = resolve(fileURLToPath(new URL('../..', import.meta.url)));
const source = process.env.SCP_CV_NATIVE_PREVIEW_TEST_INPUT, report = resolve(process.env.SCP_CV_NATIVE_PREVIEW_TEST_REPORT || '');
// 测试profile与实际用户客户端隔离，不复用或重置已有Electron数据。
app.setPath('userData', join(resolve(report, '..'), `electron-profile-${randomUUID()}`));
if (!/^rt(?:sp|mp):\/\/127\.0\.0\.1:\d+\//.test(source || '')) throw new Error('仅允许明确的本机合成RTSP/RTMP来源');
protocol.registerSchemesAsPrivileged([{ scheme: 'app', privileges: { standard: true, secure: true, supportFetchAPI: true, corsEnabled: true, stream: true } }]);
let native, window, resolved = 0;
const errors = [];
const apiObservations = [];
let finishStarted = false;
/** :param result: 真帧事实，不含源URI。:returns: 先清理本人媒体与窗口再写证据退出。 */
async function finish(result) {
  if (finishStarted) return; finishStarted = true;
  clearTimeout(deadline);
  try { await native?.dispose(); } catch { errors.push('cleanup-pending'); }
  await writeFile(report, `${JSON.stringify({ electron: process.versions.electron, protocol: source.startsWith('rtmp:') ? 'rtmp' : 'rtsp', boundary: '仅本机假认证/实际Main/app协议/自包含宿主/合成RTSP或RTMP，无GUI输入，无D4', resolved, errors, apiObservations, ...result }, null, 2)}\n`, 'utf8');
  window?.destroy(); backend.close(); app.exit(result.success ? 0 : 1);
}
const backend = createServer({ cert: await readFile(process.env.SCP_CV_NATIVE_PREVIEW_TEST_CERT), key: await readFile(process.env.SCP_CV_NATIVE_PREVIEW_TEST_KEY) }, async (request, response) => {
  response.setHeader('Access-Control-Allow-Origin', 'app://scp-cv'); response.setHeader('Access-Control-Allow-Credentials', 'true');
  response.setHeader('Access-Control-Allow-Headers', 'Content-Type,X-CSRFToken'); response.setHeader('Access-Control-Allow-Methods', 'GET,POST,OPTIONS');
  apiObservations.push({ path: request.url, method: request.method, origin: request.headers.origin, hasCookie: Boolean(request.headers.cookie), hasCsrf: Boolean(request.headers['x-csrftoken']) });
  if (request.method === 'OPTIONS') { response.writeHead(204); response.end(); return; }
  if (request.url === '/api/auth/csrf/') { response.writeHead(200, { 'Content-Type': 'application/json' }); response.end(JSON.stringify({ csrfToken: 'c'.repeat(43) })); return; }
  if (request.url === '/api/stream-previews/native/resolve/' && request.method === 'POST' && request.headers.origin === 'app://scp-cv' && request.headers.cookie?.includes('sessionid=fixture-only') && request.headers['x-csrftoken'] === 'c'.repeat(43)) {
    let body = ''; for await (const bytes of request) body += bytes;
    const target = JSON.parse(body); resolved++;
    response.writeHead(200, { 'Content-Type': 'application/json', 'Cache-Control': 'no-store' });
    response.end(JSON.stringify({ uri: source, sharing_key: 'e'.repeat(64), source_id: target.source_id ?? null, source_revision: 1,
      quality: target.quality, expires_at: new Date(Date.now() + 30000).toISOString() })); return;
  }
  response.writeHead(403); response.end();
});
await new Promise(resolveListen => backend.listen(0, '127.0.0.1', resolveListen));
const deadline = setTimeout(() => { void finish({ success: false, reason: 'native-preview-frame-timeout' }); }, 20000);
await app.whenReady();
const ownSession = session.fromPartition(`native-preview-020-${randomUUID()}`);
native = setupNativePreview(ownSession, () => window ?? null, event => {
  if (!isTrustedRendererUrl(event.senderFrame?.url ?? '')) throw new Error('untrusted');
});
ownSession.protocol.handle('app', async request => {
  const path = new URL(request.url).pathname;
  if (path.startsWith('/__preview/')) return native.stream(request);
  const headers = { 'Content-Security-Policy': PACKAGED_CSP, 'X-Content-Type-Options': 'nosniff' };
  if (path === '/') return new Response('<!doctype html><meta charset="UTF-8"><title>Native preview QA</title><video muted autoplay playsinline style="width:640px;height:360px"></video><script src="/mpegts.js"></script><script src="/qa.js"></script>', { headers: { ...headers, 'Content-Type': 'text/html' } });
  if (path === '/mpegts.js') return new Response(await readFile(join(root, 'node_modules/mpegts.js/dist/mpegts.js')), { headers: { ...headers, 'Content-Type': 'text/javascript' } });
  if (path === '/qa.js') return new Response(await readFile(new URL(process.env.SCP_CV_NATIVE_PREVIEW_TEST_HOLD === 'true' ? './hold.js' : './page.js', import.meta.url)), { headers: { ...headers, 'Content-Type': 'text/javascript' } });
  return new Response('', { status: 404, headers });
});
const origin = `https://127.0.0.1:${backend.address().port}`;
await ownSession.cookies.set({ url: origin, name: 'sessionid', value: 'fixture-only', path: '/', secure: true, httpOnly: true, sameSite: 'no_restriction' });
await native.setProfile({ origin, displayName: '合成本机测试' });
window = new BrowserWindow({ show: false, width: 800, height: 600, webPreferences: { session: ownSession,
  preload: join(root, 'electron-dist/preload.js'), nodeIntegration: false, contextIsolation: true, sandbox: true, webSecurity: true } });
window.webContents.on('console-message', (_event, _level, message) => {
  if (typeof message !== 'string') return;
  if (message.startsWith('NATIVE_RESULT:')) { try { void finish(JSON.parse(message.slice(14))); } catch { void finish({ success: false, reason: 'bad-result' }); } }
  else if (message === 'NATIVE_HOLD_READY') void writeFile(report, `${JSON.stringify({ ready: true, mainPid: process.pid, electron: process.versions.electron, boundary: '仅本机停数据源与实际Electron父死亡测试' })}\n`, 'utf8');
  else if (/error|refused|failed/i.test(message)) errors.push(message.replace(/rtsp:\/\/\S+/g, '[redacted]').slice(0, 240));
});
window.webContents.on('did-fail-load', () => { void finish({ success: false, reason: 'app-protocol-load-failed' }); });
await window.loadURL('app://scp-cv/');
