/**
 * 文件描述：只读无头浏览器验证共享折叠栏、尺寸与资源身份。
 * 项目：SCP-cv；文件：verify-share-disclosure-032.mjs；作者：Qintsg；日期：2026-11-05。
 */
import assert from 'node:assert/strict';
import { parseArgs } from 'node:util';
import { pathToFileURL, fileURLToPath } from 'node:url';
import { mkdir, writeFile } from 'node:fs/promises';
import { createServer } from 'vite';
import vue from '@vitejs/plugin-vue';

const { values } = parseArgs({ options: { playwright: { type: 'string' }, browser: { type: 'string' } } });
const modulePath = values.playwright || process.env.SCP_PLAYWRIGHT_MODULE;
const executablePath = values.browser || process.env.SCP_BROWSER;
if (!modulePath || !executablePath) throw new Error('需要真实Playwright模块和浏览器路径。');
const { chromium } = await import(pathToFileURL(modulePath).href);
const frontend = fileURLToPath(new URL('../', import.meta.url));
const output = fileURLToPath(new URL(`../../.validation/share-032/disclosure-${Date.now()}/`, import.meta.url));
await mkdir(output, { recursive: true });
const observations = [], errors = [], requests = [];
let browser;
let lastSnapshot;
const server = await createServer({ root: frontend, configFile: false, mode: 'web', logLevel: 'error',
  cacheDir: `${output}/vite-cache`,
  plugins: [vue(), { name: 'share-disclosure-032', configureServer(server) {
    server.middlewares.use('/share-disclosure-032.html', async (_request, response) => {
      response.setHeader('Content-Type', 'text/html; charset=utf-8');
      response.end(await server.transformIndexHtml('/share-disclosure-032.html', '<!doctype html><html lang="zh-CN"><meta name="viewport" content="width=device-width,initial-scale=1"><title>共享折叠栏验收</title><body><div id="app"></div><script type="module" src="/scripts/share-disclosure-032/page.ts"></script></body></html>'));
    });
  } }], resolve: { alias: { '@': `${frontend.replaceAll('\\', '/')}/src` } }, server: { host: '127.0.0.1', port: 0 },
});

/** :param value: 真实DOM快照。:returns: 校验实际栏位、共享身份与页面宽度。 */
function validate(value) {
  assert.equal(value.overflow, false); assert.ok(value.summaryHeight >= 44); assert.ok(value.summaryWidth > 200);
  assert.equal(value.border, '1px'); assert.notEqual(value.background, 'rgba(0, 0, 0, 0)'); assert.equal(value.arrowSvg, 1);
  assert.equal(value.sameOwner, true); assert.equal(value.samePanel, true); assert.equal(value.activeOwners, 0); assert.equal(value.phase, 'idle');
}

try {
  await server.listen();
  const origin = `http://127.0.0.1:${server.httpServer.address().port}`;
  browser = await chromium.launch({ executablePath, headless: true, args: ['--no-proxy-server'] });
  for (const width of [390, 800, 1440]) for (const theme of ['light', 'dark']) {
    const context = await browser.newContext({ viewport: { width, height: 960 }, colorScheme: theme });
    try {
      const page = await context.newPage();
      page.on('pageerror', error => errors.push({ width, theme, message: error.message }));
      page.on('request', request => { if (new URL(request.url()).pathname.startsWith('/api/')) requests.push(request.url()); });
      await page.goto(`${origin}/share-disclosure-032.html?theme=${theme}`, { waitUntil: 'domcontentloaded', timeout: 120000 });
      await page.waitForFunction(() => window.shareDisclosure032 !== undefined, undefined, { timeout: 120000 }); await page.evaluate(() => document.fonts.ready);
      const closed = await page.evaluate(() => window.shareDisclosure032.snapshot()); lastSnapshot = closed; validate(closed); assert.equal(closed.open, false); assert.equal(closed.arrowTransform, 'none');
      await page.evaluate(() => window.shareDisclosure032.focusSummary());
      const focused = await page.evaluate(() => window.shareDisclosure032.snapshot());
      assert.equal(focused.focused, true); assert.equal(focused.focusVisible, true); assert.equal(focused.outlineStyle, 'solid'); assert.equal(focused.outlineWidth, '2px');
      await page.screenshot({ path: `${output}/${width}-${theme}-closed.png` });
      await page.evaluate(() => window.shareDisclosure032.setOpen(true));
      await page.waitForFunction(() => window.shareDisclosure032.snapshot().arrowTransform === 'matrix(0, 1, -1, 0, 0, 0)');
      const opened = await page.evaluate(() => window.shareDisclosure032.snapshot()); validate(opened); assert.equal(opened.open, true);
      await page.screenshot({ path: `${output}/${width}-${theme}-open.png` });
      await page.evaluate(() => window.shareDisclosure032.setOpen(false));
      const restored = await page.evaluate(() => window.shareDisclosure032.snapshot()); validate(restored); assert.equal(restored.open, false);
      observations.push({ width, theme, closed, focused, opened, restored });
      await page.close();
    } finally { await context.close(); }
  }
  assert.deepEqual(errors, []); assert.deepEqual(requests, []);
  await writeFile(`${output}/results.json`, JSON.stringify({ observations, errors, requests }, null, 2));
  console.log(JSON.stringify({ output, pages: observations.length, screenshots: observations.length * 2, errors: errors.length, apiRequests: requests.length }));
} catch (error) {
  await writeFile(`${output}/failure.json`, JSON.stringify({ message: error.message, lastSnapshot, observations, errors, requests }, null, 2)); throw error;
} finally { await browser?.close(); await server.close(); }
