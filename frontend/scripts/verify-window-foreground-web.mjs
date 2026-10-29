// 文件描述：只读检查真实主机状态对应的Web维护菜单，不模拟输入。
// 项目：SCP-cv；文件：verify-window-foreground-web.mjs；作者：Qintsg；日期：2026-10-29。
import assert from 'node:assert/strict';
import { pathToFileURL, fileURLToPath } from 'node:url';
import { mkdir, writeFile } from 'node:fs/promises';
import { createServer } from 'vite';
import { createServer as createSocket } from 'node:net';

const backend = process.env.SCP_WINDOW_BACKEND, password = process.env.SCP_WINDOW_PASSWORD;
const expected = process.env.SCP_WINDOW_EXPECTED, playwright = process.env.SCP_PLAYWRIGHT_MODULE, executablePath = process.env.SCP_BROWSER;
if (!backend || !password || !expected || !playwright || !executablePath) throw new Error('缺少真实主机、账号或浏览器检查参数。');
const root = fileURLToPath(new URL('../', import.meta.url));
const output = fileURLToPath(new URL('../../.validation/window-029/web/', import.meta.url)); await mkdir(output, { recursive: true });
const socket = createSocket(); await new Promise(resolve => socket.listen(0, '127.0.0.1', resolve));
const port = socket.address().port; await new Promise(resolve => socket.close(resolve));
process.env.VITE_BACKEND_TARGET = backend; process.env.VITE_FRONTEND_PORT = String(port);
const server = await createServer({ root, mode: 'web', server: { host: '127.0.0.1', port, strictPort: true }, logLevel: 'error' });
const { chromium, request } = await import(pathToFileURL(playwright).href);
let browser, http;
const observations = [], errors = [];
try {
  await server.listen(); const address = server.httpServer.address(); const origin = `http://127.0.0.1:${address.port}`;
  http = await request.newContext({ baseURL: origin });
  assert.ok((await http.get('/api/auth/csrf/')).ok());
  assert.ok((await http.post('/api/auth/login/', { data: { username: 'window-admin', password }, headers: { Origin: origin } })).ok());
  browser = await chromium.launch({ executablePath, headless: true, args: ['--no-proxy-server'] });
  for (const [width, height] of [[390, 844], [800, 1280], [1365, 900]]) {
    const context = await browser.newContext({ storageState: await http.storageState(), viewport: { width, height }, hasTouch: width < 1000, isMobile: width < 1000 });
    try {
      const page = await context.newPage(); page.on('pageerror', error => errors.push(error.message));
      await page.goto(origin + '/dashboard', { waitUntil: 'domcontentloaded' });
      await page.locator('button[aria-label="应急控制"]').waitFor();
      await page.waitForFunction(() => {
        let instance = document.querySelector('button[aria-label="应急控制"]').__vueParentComponent;
        while (instance && instance.type.__name !== 'EmergencyMenu') instance = instance.parent;
        return instance?.setupState.options.flatMap(option => option.children ?? [option]).find(option => option.key === 'window-foreground')?.disabled === false;
      });
      const item = await page.evaluate(() => {
        let instance = document.querySelector('button[aria-label="应急控制"]').__vueParentComponent;
        while (instance && instance.type.__name !== 'EmergencyMenu') instance = instance.parent;
        if (!instance) throw new Error('维护菜单组件未挂载。');
        const entries = instance.setupState.options.flatMap(option => option.children ?? [option]);
        const entry = entries.find(option => option.key === 'window-foreground');
        return { label: entry.label, disabled: entry.disabled, overflow: document.documentElement.scrollWidth > innerWidth };
      });
      assert.equal(item.label, expected); assert.equal(item.disabled, false); assert.equal(item.overflow, false);
      observations.push({ width, height, ...item });
      await page.locator('.app-shell__bar').screenshot({ path: `${output}/${expected}-${width}.png` });
    } finally { await context.close(); }
  }
  assert.deepEqual(errors, []);
  await writeFile(`${output}/${expected}.json`, JSON.stringify({ observations, errors }, null, 2));
  console.log(JSON.stringify({ expected, pages: observations.length, errors: errors.length }));
} finally { await browser?.close(); await http?.dispose(); await server.close(); }
