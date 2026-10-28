// 文件描述：使用真实浏览器、生产运动模块与本机文件记录验证底栏。
// 项目：SCP-cv；文件：verify-glass-motion.mjs；作者：Qintsg；日期：2026-10-28。
import assert from 'node:assert/strict';
import { parseArgs } from 'node:util';
import { pathToFileURL, fileURLToPath } from 'node:url';
import { mkdir, appendFile, readFile, writeFile } from 'node:fs/promises';
import { randomBytes } from 'node:crypto';
import { createServer } from 'vite';

const { values } = parseArgs({ options: { playwright: { type: 'string' }, browser: { type: 'string' } } });
if (!values.playwright || !values.browser) throw new Error('请提供--playwright模块路径和--browser浏览器路径。');
const { chromium } = await import(pathToFileURL(values.playwright).href);
const root = fileURLToPath(new URL('../', import.meta.url));
const output = fileURLToPath(new URL(`../../.validation/glass-028/motion-${randomBytes(6).toString('hex')}/`, import.meta.url));
await mkdir(output, { recursive: true });
const server = await createServer({ root, configFile: false, logLevel: 'error', optimizeDeps: { noDiscovery: true, include: ['motion', 'simple-liquid-glass/web-component'] }, server: { host: '127.0.0.1', port: 0 },
  plugins: [{ name: 'actual-glass-verification', configureServer(vite) {
    vite.middlewares.use('/record', async (request, response, next) => {
      if (request.method !== 'POST') return next();
      const chunks = []; for await (const chunk of request) chunks.push(chunk);
      const index = JSON.parse(Buffer.concat(chunks).toString());
      assert.ok(Number.isInteger(index) && index >= 0 && index < 5);
      await appendFile(`${output}/selection.jsonl`, JSON.stringify(index) + '\n'); response.end('saved');
    });
    vite.middlewares.use('/verify.html', async (_request, response) => {
      response.setHeader('Content-Type', 'text/html; charset=utf-8');
      response.end(await vite.transformIndexHtml('/verify.html', `<!doctype html><html lang="zh-CN"><meta charset="utf-8"><title>玻璃运动技术验证</title>
        <style>body{margin:0;background:repeating-linear-gradient(90deg,#176ec4 0 15px,#efba45 15px 30px);min-height:100vh}liquid-glass{position:fixed!important;left:30px;top:120px;width:90px;height:60px}</style>
        <liquid-glass renderer="svg" reveal="immediate" lens="rim" lens-strength="0.6" scale="26" radius="30" frost="0.04"></liquid-glass>
        <script type="module" src="/scripts/glass-motion-page.ts"></script></html>`));
    });
  } }] });
let browser;
try {
  await server.listen();
  browser = await chromium.launch({ executablePath: values.browser, headless: true, args: ['--no-proxy-server'] });
  const page = await browser.newPage({ viewport: { width: 600, height: 360 } });
  const errors = []; page.on('pageerror', error => errors.push(error.message));
  await page.goto(`http://127.0.0.1:${server.httpServer.address().port}/verify.html`);
  await page.waitForFunction(() => window.glassVerification !== undefined);
  const result = await page.evaluate(() => window.glassVerification);
  assert.deepEqual(errors, []);
  const recorded = (await readFile(`${output}/selection.jsonl`, 'utf8')).trim().split('\n').map(JSON.parse);
  assert.deepEqual(recorded, result.records);
  await page.screenshot({ path: `${output}/glass.png` });
  await writeFile(`${output}/result.json`, JSON.stringify({ ...result, errors }, null, 2));
  console.log(JSON.stringify({ ...result, output }));
} finally { await browser?.close(); await server.close(); }
