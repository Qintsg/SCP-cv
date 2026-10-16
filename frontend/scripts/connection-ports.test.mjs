/*
 * 设置页连接摘要回归：当前公开连接配置应与实际 REST 请求使用同一目标。
 * 挂载真实 Vue/Ant Design Vue，只在 HTTP 边界返回测试数据，不连接播放主机。
 * @Project : SCP-cv
 * @File : connection-ports.test.mjs
 * @Author : Qintsg
 * @Date : 2026-10-03
 */
import assert from 'node:assert/strict';
import { setImmediate as nextTurn } from 'node:timers/promises';
import test, { after, afterEach, before } from 'node:test';
import { Window } from 'happy-dom';
import { createDomTestServer } from './dom-test-server.mjs';

const browser = new Window({ url: 'http://127.0.0.1:20880/settings' });
const globalNames = ['window', 'document', 'navigator', 'history', 'HTMLElement', 'HTMLInputElement',
  'HTMLTextAreaElement', 'Element', 'Node', 'SVGElement', 'Document', 'MutationObserver',
  'ResizeObserver', 'Image', 'InputEvent', 'getComputedStyle', 'requestAnimationFrame', 'cancelAnimationFrame'];
const previousGlobals = new Map(globalNames.map((name) => [name, Object.getOwnPropertyDescriptor(globalThis, name)]));
for (const name of globalNames) {
  const value = typeof browser[name] === 'function' && /^[a-z]/.test(name)
    ? browser[name].bind(browser) : browser[name];
  Object.defineProperty(globalThis, name, { value, configurable: true, writable: true });
}

const originalFetch = globalThis.fetch;
let server;
let vue;
let createPinia;
let routerModule;
let apiModule;
let SettingsView;
let DevSettingsTab;
let i18n;
let app;

before(async () => {
  [vue, { createPinia }, routerModule, server] = await Promise.all([
    import('vue'), import('pinia'), import('vue-router'), createDomTestServer(),
  ]);
  // 构建地址与当前保存配置故意不同，覆盖真实发布版的配置优先级分歧。
  server.config.env.VITE_BACKEND_TARGET = 'https://build-target.example.test:18443';
  apiModule = await server.ssrLoadModule('/src/services/api.ts');
  ({ default: SettingsView } = await server.ssrLoadModule('/src/features/settings/SettingsView.vue'));
  ({ default: DevSettingsTab } = await server.ssrLoadModule('/src/features/settings/tabs/DevSettingsTab.vue'));
  ({ i18n } = await server.ssrLoadModule('/src/locales/index.ts'));
});

afterEach(async () => {
  app?.unmount();
  app = undefined;
  browser.document.body.replaceChildren();
  await browser.happyDOM.abort();
  globalThis.fetch = originalFetch;
});

after(async () => {
  await server?.close();
  await browser.happyDOM.close();
  for (const [name, descriptor] of previousGlobals) {
    if (descriptor) Object.defineProperty(globalThis, name, descriptor);
    else delete globalThis[name];
  }
});

/** :returns: Vue 渲染及已完成请求回调已结束当前一轮。 */
async function flushUi() {
  await vue.nextTick();
  await nextTurn();
  await vue.nextTick();
}

/**
 * 经公开 API 配置切换当前主机，不直接更改连接协调器内部状态。
 * :param origin: 测试网页主机地址，HTTP/HTTPS均允许。
 * :returns: 当前公开连接配置已恢复。
 */
function configureServer(origin) {
  apiModule.configureApiServer({ origin, displayName: '测试播放主机', allowInsecureDevelopment: true });
}

/**
 * 挂载实际设置组件，只读请求由边界假数据响应。
 * :param component: 设置容器或开发诊断 Tab。
 * :returns: 当前界面已渲染。
 */
async function mountSettings(component) {
  globalThis.fetch = async () => new Response(JSON.stringify({
    success: true, settings: { default_engine: 'libreoffice', available: true, detail: '测试依赖' },
    layout: { draft: null, applied: null, presets: [] },
  }), { headers: { 'Content-Type': 'application/json' } });
  const host = browser.document.createElement('div');
  browser.document.body.append(host);
  app = vue.createApp(component);
  app.use(createPinia());
  app.use(i18n);
  const router = routerModule.createRouter({ history: routerModule.createMemoryHistory(), routes: [
    { path: '/', component: { render: () => null } },
    { path: '/settings', component: { render: () => null } },
  ] });
  app.use(router);
  await router.push('/settings');
  app.mount(host);
  await flushUi();
}

test('网页未选显式主机时使用同源入口，构建目标只作为代理上游', () => {
  assert.equal(apiModule.buildBackendUrl('/api/runtime/'), '/api/runtime/');
  assert.deepEqual(apiModule.getConnectionPorts(), { backendPort: '20880', frontendPort: '20880' });
});

test('保存的主机端口优先于构建目标，且与实际 REST 地址一致', () => {
  configureServer('http://127.0.0.1:20881');
  assert.equal(apiModule.buildBackendUrl('/api/runtime/'), 'http://127.0.0.1:20881/api/runtime/');
  assert.deepEqual(apiModule.getConnectionPorts(), { backendPort: '20881', frontendPort: '20880' });
});

test('默认协议端口及 IPv6 显式端口来自当前主机，不泄露主机配置内容', () => {
  for (const [origin, port] of [['https://host.example.test', '443'],
    ['http://127.0.0.1', '80'], ['https://[::1]:24443', '24443']]) {
    configureServer(origin);
    assert.deepEqual(apiModule.getConnectionPorts(), { backendPort: port, frontendPort: '20880' });
  }
});

test('切换当前主机后重新读取摘要不保留上一主机端口', () => {
  configureServer('https://first.example.test:24443');
  assert.equal(apiModule.getConnectionPorts().backendPort, '24443');
  configureServer('https://second.example.test:25443');
  assert.equal(apiModule.getConnectionPorts().backendPort, '25443');
});

test('真实设置页品牌摘要显示已保存的当前主机端口', async () => {
  configureServer('http://127.0.0.1:20881');
  await mountSettings(SettingsView);
  const caption = browser.document.querySelector('.settings-view__brand-caption')?.textContent ?? '';
  assert.match(caption, /REST :20881/);
  assert.match(caption, /Vite :20880/);
  assert.doesNotMatch(caption, /18443|8000/);
});

test('真实开发诊断 Tab 的端口汇总同样使用当前主机', async () => {
  configureServer('http://127.0.0.1:20881');
  await mountSettings(DevSettingsTab);
  const caption = [...browser.document.querySelectorAll('.settings-view__hint')]
    .map((element) => element.textContent).find((text) => text.startsWith('REST :')) ?? '';
  assert.match(caption, /REST :20881/);
  assert.match(caption, /Vite :20880/);
  assert.doesNotMatch(caption, /18443|8000/);
});

test('真实开发诊断 Tab 的 API 链接和显示地址使用实际 REST 目标', async () => {
  configureServer('http://127.0.0.1:20881');
  await mountSettings(DevSettingsTab);
  const links = [...browser.document.querySelectorAll('.settings-view__api-list a')];
  const paths = ['/api/sources/', '/api/scenarios/', '/api/sessions/',
    '/api/runtime/', '/api/video-wall/layout/', '/api/events/'];
  assert.equal(links.length, paths.length);
  assert.deepEqual(links.map((link) => link.href), paths.map((path) => apiModule.buildBackendUrl(path)));
  assert.deepEqual(links.map((link) => link.textContent), paths.map((path) => apiModule.buildBackendUrl(path)));
});
