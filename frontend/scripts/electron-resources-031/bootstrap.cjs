// 文件描述：隔离配置后启动正式Electron入口并读取实际资源行为。
// 项目：SCP-cv；文件：bootstrap.cjs；作者：Qintsg；日期：2026-10-30。
const { app, BrowserWindow } = require('electron');
const { mkdirSync, readFileSync, writeFileSync, readdirSync } = require('node:fs');
const { join, resolve, sep } = require('node:path');
const { pathToFileURL } = require('node:url');
const { createServer } = require('node:http');
const { createHash } = require('node:crypto');

const frontend = resolve(__dirname, '../..'), root = resolve(frontend, '..');
const run = resolve(process.env.SCP_CV_ELECTRON_RESOURCES_RUN || '');
const boundary = join(root, '.validation', 'navigation-031') + sep;
if (!run.startsWith(boundary) || app.isReady()) throw new Error('Electron资源检查目录或启动阶段无效。');
const profile = join(run, 'profile');
for (const part of ['', 'session', 'crashes']) mkdirSync(join(profile, part), { recursive: true });
app.setPath('userData', profile);
app.setPath('sessionData', join(profile, 'session'));
app.setPath('crashDumps', join(profile, 'crashes'));
const report = { success: false, pid: process.pid, electron: process.versions.electron, profile, production: {}, resources: {}, headers: [], errors: [] };
let window, server, finished = false;
const ownCsp = "default-src 'none'; frame-ancestors app://scp-cv";

/** :returns: 只保存当前独占检查的非秘密数据。 */
function save() { writeFileSync(join(run, 'report.json'), JSON.stringify(report, null, 2) + '\n', 'utf8'); }
/** :param failure: 实际失败。:returns: 记录失败并沿正式窗口关闭流程退出。 */
async function finish(failure) {
  if (finished) return; finished = true; clearTimeout(deadline);
  if (failure) report.errors.push(failure.message || String(failure)); else report.success = true;
  save(); if (server) await new Promise(resolveClose => server.close(resolveClose));
  if (window && !window.isDestroyed()) window.close(); else app.quit();
}
const deadline = setTimeout(() => { void finish(new Error('真实Electron资源检查超过30秒。')); }, 30000);
app.on('browser-window-created', (_event, created) => { window = created; });

(async () => {
  const mainPath = join(frontend, 'electron-dist', 'main.js');
  const securityPath = join(frontend, 'electron-dist', 'security.js');
  for (const file of [mainPath, securityPath, join(frontend, 'electron-dist', 'preload.js')]) {
    report.production[file.split(/[\\/]/).at(-1)] = createHash('sha256').update(readFileSync(file)).digest('hex');
  }
  // 路径与配置已经隔离；入口、桥、session、安全策略及协议处理器使用生产实现。
  await import(pathToFileURL(mainPath).href);
  await app.whenReady();
  if (!window) throw new Error('正式主入口未创建窗口。');
  if (window.webContents.isLoading()) await new Promise(resolveLoad => window.webContents.once('did-finish-load', resolveLoad));
  const preferences = window.webContents.getLastWebPreferences();
  report.security = { nodeIntegration: preferences.nodeIntegration, contextIsolation: preferences.contextIsolation,
    sandbox: preferences.sandbox, webSecurity: preferences.webSecurity };
  if (preferences.nodeIntegration || !preferences.contextIsolation || !preferences.sandbox || !preferences.webSecurity) throw new Error('正式窗口安全设置不符。');
  const assets = join(frontend, 'dist-app', 'assets');
  const workerName = readdirSync(assets).find(name => name.startsWith('pdf.worker.min-') && name.endsWith('.mjs'));
  if (!workerName) throw new Error('当前App构建缺少PDF模块Worker。');
  const workerUrl = 'app://scp-cv/assets/' + workerName;
  report.resources.worker = await window.webContents.executeJavaScript(`(async () => {
    const address = ${JSON.stringify(workerUrl)};
    const response = await fetch(address);
    const contentType = response.headers.get('content-type');
    if (!response.ok || !contentType?.startsWith('text/javascript')) throw new Error('真实.mjs资源MIME不符。');
    const worker = new Worker(address, { type: 'module' });
    try {
      await new Promise((resolveReady, reject) => {
        const timer = setTimeout(() => reject(new Error('真实模块Worker未发送ready。')), 10000);
        worker.onmessage = event => { if (event.data?.action === 'ready') { clearTimeout(timer); resolveReady(); } };
        worker.onerror = () => { clearTimeout(timer); reject(new Error('真实模块Worker加载失败。')); };
      });
      return { status: response.status, contentType, ready: true, platform: window.scpCvElectron?.platform,
        nodeExposed: typeof window.require !== 'undefined' || typeof process !== 'undefined',
        sidebar: !!document.querySelector('.console-navigation'), floatingNavigation: !!document.querySelector('.liquid-bottom-navigation'),
        page: location.hash, title: document.title };
    } finally { worker.terminate(); }
  })()`);
  if (report.resources.worker.platform !== 'electron' || report.resources.worker.nodeExposed) throw new Error('正式preload平台桥或Node隔离不符。');
  if (report.resources.worker.floatingNavigation) throw new Error('Electron普通启动页出现悬浮底栏。');
  server = createServer((_request, response) => {
    response.writeHead(200, { 'Content-Type': 'text/html; charset=utf-8', 'Content-Security-Policy': ownCsp, 'X-Frame-Options': 'SAMEORIGIN' });
    response.end('<!doctype html><meta charset="utf-8"><main id="scp031-frame">实际回环网页内容</main>');
  });
  await new Promise(resolveListen => server.listen(0, '127.0.0.1', resolveListen));
  const frameUrl = `http://127.0.0.1:${server.address().port}/frame`;
  window.webContents.session.webRequest.onResponseStarted(details => {
    if (details.url !== frameUrl || details.resourceType !== 'subFrame') return;
    const headers = Object.fromEntries(Object.entries(details.responseHeaders || {}).map(([name, values]) => [name.toLowerCase(), values]));
    report.headers.push({ status: details.statusCode, csp: headers['content-security-policy'], xfo: headers['x-frame-options'] }); save();
  });
  await window.webContents.executeJavaScript(`new Promise(resolveLoad => {
    const frame = document.createElement('iframe'); frame.id = 'scp031-frame-host';
    frame.sandbox = 'allow-scripts allow-same-origin allow-forms allow-presentation'; frame.referrerPolicy = 'no-referrer';
    frame.onload = () => resolveLoad(); frame.src = ${JSON.stringify(frameUrl)}; document.body.append(frame);
  })`);
  const received = report.headers.at(-1);
  if (!received || received.status !== 200 || !received.csp?.includes(ownCsp) || !received.xfo?.includes('SAMEORIGIN')) throw new Error('内嵌网页的实际原响应头未保持。');
  report.resources.frame = { headersPreserved: true, ownCsp, xfo: 'SAMEORIGIN' };
  await window.webContents.executeJavaScript("document.getElementById('scp031-frame-host')?.remove()");
  report.sidebarBoundary = report.resources.worker.sidebar ? '实际侧栏已呈现' : '普通连接页未登录，侧栏未计为验收通过';
  await finish();
})().catch(error => { void finish(error); });
