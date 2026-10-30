// 文件描述：真实Web/App导航、Pad旋转命中与玻璃材质验收，不派发输入事件。
// 项目：SCP-cv；文件：verify-navigation-031.mjs；作者：Qintsg；日期：2026-10-30。
import assert from 'node:assert/strict';
import { parseArgs } from 'node:util';
import { pathToFileURL, fileURLToPath } from 'node:url';
import { mkdir, writeFile } from 'node:fs/promises';
import { createServer } from 'vite';
import vue from '@vitejs/plugin-vue';
import tailwind from '@tailwindcss/vite';

const { values } = parseArgs({ options: { playwright: { type: 'string' }, browser: { type: 'string' }, mode: { type: 'string' }, theme: { type: 'string' }, width: { type: 'string' } } });
if (values.mode && !['web', 'app'].includes(values.mode)) throw new Error('--mode仅支持web或app。');
if (values.theme && !['light', 'dark'].includes(values.theme)) throw new Error('--theme仅支持light或dark。');
if (values.width && (!Number.isSafeInteger(Number(values.width)) || Number(values.width) <= 0)) throw new Error('--width必须是正整数。');
const playwrightPath = values.playwright || process.env.SCP_PLAYWRIGHT_MODULE;
const executablePath = values.browser || process.env.SCP_BROWSER;
if (!playwrightPath || !executablePath) throw new Error('请提供真实Playwright模块和浏览器路径。');
const { chromium } = await import(pathToFileURL(playwrightPath).href);
const frontend = fileURLToPath(new URL('../', import.meta.url));
const output = fileURLToPath(new URL(`../../.validation/navigation-031/run-${Date.now()}/`, import.meta.url));
await mkdir(output, { recursive: true });
const observations = [], rotations = [], preferences = [], errors = [], apiRequests = [];
let browser;

/** :param mode: 实际Vite构建目标。:returns: 仅包含真实外壳与路由的本机服务。 */
async function serverFor(mode) {
  const server = await createServer({ root: frontend, configFile: false, mode, logLevel: 'error',
    plugins: [vue({ template: { compilerOptions: { isCustomElement: tag => tag === 'liquid-glass' } } }), tailwind(), {
      name: 'navigation-031-real-shell', configureServer(server) {
        server.middlewares.use('/navigation-031.html', async (_request, response) => {
          response.setHeader('Content-Type', 'text/html; charset=utf-8');
          response.end(await server.transformIndexHtml('/navigation-031.html', '<!doctype html><html lang="zh-CN"><meta name="viewport" content="width=device-width,initial-scale=1"><title>031导航几何验收</title><style>.navigation-031-content{min-height:140vh}.navigation-031-colors{height:110vh;background:repeating-linear-gradient(120deg,#c2ddff 0 44px,#e6e0ff 44px 88px,#d4eff3 88px 132px);border-radius:16px}</style><body><div id="app"></div><script type="module" src="/scripts/navigation-031/page.ts"></script></body></html>'));
        });
      },
    }], resolve: { alias: { '@': `${frontend.replaceAll('\\', '/')}/src` } },
    server: { host: '127.0.0.1', port: 0 },
  });
  await server.listen();
  return { server, origin: `http://127.0.0.1:${server.httpServer.address().port}` };
}

/** :param page: 实际Chromium页面。:param expectedBottom: 当前目标是否应有底栏。:returns: 字体、布局和实际玻璃准备完成。 */
async function ready(page, expectedBottom) {
  await page.waitForFunction(() => window.navigation031 !== undefined);
  await page.evaluate(() => document.fonts.ready);
  if (expectedBottom) await page.waitForFunction(() => {
    const value = window.navigation031.snapshot();
    if (!value.glass || value.active < 0 || value.glass.revealMethod !== 'function') return false;
    const selected = value.buttons[value.active], lens = value.glass.rect;
    return value.glass.strategy === 'svg' && value.glass.displacement > 0 && Math.abs((selected.left + selected.right) / 2 - (lens.left + lens.right) / 2) < 1.5;
  });
}

/** :param value: 当前实际快照。:param bottom: 目标导航。:returns: 正式几何与DOM命中一致。 */
function validate(value, bottom) {
  assert.equal(value.overflow, false, '页面不应水平溢出');
  assert.equal(value.touchNavigation, String(bottom));
  if (!bottom) {
    assert.ok(value.sidebar && value.sidebar.width >= 70, '网页必须保留左侧导航');
    assert.equal(value.nav, null); assert.equal(value.glass, null);
    for (const path of ['/dashboard', '/display/big-left', '/display/big-right', '/sources', '/settings']) assert.ok(value.links.includes(path));
    return;
  }
  assert.equal(value.sidebar, null); assert.ok(value.nav);
  assert.equal(value.buttons.length, 5);
  assert.ok(value.nav.top >= 0 && value.nav.bottom <= value.height);
  assert.ok(value.buttons.every(button => button.width >= 44 && button.height >= 44));
  for (const center of value.centers) assert.ok(Math.abs(center.position - center.expected) < .00001, '真实按钮中心应对应同一入口');
  for (const hit of value.hits) { assert.equal(hit.actual, hit.expected, 'DOM实际命中被其它层覆盖'); assert.equal(hit.rounded, hit.expected, '生产坐标计算偏离实际按钮'); }
  assert.equal(value.glass.strategy, 'svg'); assert.ok(value.glass.displacement > 0);
  assert.equal(value.glass.revealMethod, 'function'); assert.equal(value.glass.rootTransform, 'none');
}

try {
  browser = await chromium.launch({ executablePath, headless: true, args: ['--no-proxy-server'] });
  for (const mode of values.mode ? [values.mode] : ['web', 'app']) {
    const { server, origin } = await serverFor(mode);
    try {
      const sizes = mode === 'web' ? [[390, 844], [800, 1280], [1280, 800], [1365, 900]] : [[390, 844], [844, 390], [600, 960], [960, 600], [800, 1280], [1280, 800]];
      for (const [width, height] of sizes.filter(size => !values.width || size[0] === Number(values.width))) for (const touch of mode === 'web' ? [false, true] : [true]) for (const theme of values.theme ? [values.theme] : ['light', 'dark']) {
        const context = await browser.newContext({ viewport: { width, height }, hasTouch: touch, isMobile: touch, colorScheme: theme });
        try {
          for (const route of ['/dashboard', '/devices']) {
            const page = await context.newPage();
            page.on('pageerror', error => errors.push({ mode, width, height, route, message: error.message }));
            page.on('request', request => { if (new URL(request.url()).pathname.startsWith('/api/')) apiRequests.push({ method: request.method(), url: request.url() }); });
            await page.goto(`${origin}/navigation-031.html?theme=${theme}&route=${encodeURIComponent(route)}`, { waitUntil: 'domcontentloaded' });
            const bottom = mode === 'app'; await ready(page, bottom);
            const value = await page.evaluate(() => window.navigation031.snapshot()); validate(value, bottom);
            assert.equal(value.mode, mode); assert.equal(value.theme, theme);
            if (bottom && theme === 'dark') assert.equal(value.glass.activeColor, 'rgb(214, 234, 255)');
            observations.push({ mode, touch, route, ...value });
            if (bottom && route === '/dashboard' && theme === 'light' && width === 1280 && height === 800) {
              for (const [nextWidth, nextHeight] of [[800, 1280], [1280, 800]]) {
                await page.setViewportSize({ width: nextWidth, height: nextHeight }); await ready(page, true);
                const next = await page.evaluate(() => window.navigation031.snapshot()); validate(next, true); rotations.push({ from: [width, height], ...next });
              }
            }
            if (route === '/dashboard' && (width === 800 || width === 1280 || width === 390)) await page.screenshot({ path: `${output}/${mode}-${width}-${height}-${touch}-${theme}.png` });
            if (bottom && route === '/dashboard' && width === 800 && height === 1280 && theme === 'light') {
              await page.emulateMedia({ reducedMotion: 'reduce' });
              await page.waitForFunction(() => window.navigation031.snapshot().reducedMotion);
              const connection = await context.newCDPSession(page);
              await connection.send('Emulation.setEmulatedMedia', { features: [{ name: 'prefers-reduced-motion', value: 'reduce' }, { name: 'prefers-reduced-transparency', value: 'reduce' }] });
              await page.waitForFunction(() => { const value = window.navigation031.snapshot(); return value.glass?.effect === 'off' && value.glass.trackFilter === 'none'; });
              preferences.push(await page.evaluate(() => window.navigation031.snapshot())); await connection.detach();
            }
            await page.close();
          }
        } finally { await context.close(); }
      }
    } finally { await server.close(); }
  }
  assert.deepEqual(errors, []); assert.deepEqual(apiRequests, []);
  await writeFile(`${output}/results.json`, JSON.stringify({ observations, rotations, preferences, errors, apiRequests }, null, 2));
  console.log(JSON.stringify({ output, pages: observations.length, rotations: rotations.length, preferences: preferences.length, errors: errors.length, apiRequests: apiRequests.length }));
} catch (failure) {
  await writeFile(`${output}/failure.json`, JSON.stringify({ message: failure instanceof Error ? failure.message : String(failure), observations, rotations, preferences, errors, apiRequests }, null, 2));
  throw failure;
} finally { await browser?.close(); }
