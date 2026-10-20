/** 直播凭据展示及真实Ant地址输入/抽屉回归；仅内存DOM/fetch，禁止任何现场请求。 */
import assert from 'node:assert/strict';
import test, { before, after, afterEach } from 'node:test';
import { setImmediate as nextTurn } from 'node:timers/promises';
import { Window } from 'happy-dom';
import { createDomTestServer, visibleText } from './dom-test-server.mjs';
import { displayMediaAddress } from '../src/services/mediaAddress.ts';
const browser = new Window({ url: 'http://localhost:5173/sources' });
const names = ['window','document','navigator','HTMLElement','HTMLInputElement','HTMLTextAreaElement','Element','Node','SVGElement','Document','MutationObserver','ResizeObserver','Image','InputEvent','getComputedStyle','requestAnimationFrame','cancelAnimationFrame'];
const globals = new Map(names.map(name => [name, Object.getOwnPropertyDescriptor(globalThis, name)]));
for (const name of names) Object.defineProperty(globalThis, name, { configurable: true, writable: true,
  value: typeof browser[name] === 'function' && /^[a-z]/.test(name) ? browser[name].bind(browser) : browser[name] });
const previousFetch = globalThis.fetch;
const uri = 'rtsp://fixture-user:p%40ss%23word@192.0.2.188:554/streaming/channels/501?token=fixture-token&channel=5';
let server, vue, createPinia, disposePinia, Component, Add, Edit, i18n, useAuth, app, pinia, model, active, calls;
before(async () => {
  [vue, { createPinia, disposePinia }, server] = await Promise.all([import('vue'), import('pinia'), createDomTestServer()]);
  ({ default: Component } = await server.ssrLoadModule('/src/features/sources/StreamAddressInput.vue'));
  ({ default: Add } = await server.ssrLoadModule('/src/features/sources/AddSourceDrawer.vue'));
  ({ default: Edit } = await server.ssrLoadModule('/src/features/sources/EditSourceDrawer.vue'));
  ({ i18n } = await server.ssrLoadModule('/src/locales/index.ts'));
  ({ useAuthStore: useAuth } = await server.ssrLoadModule('/src/stores/auth.ts'));
});
afterEach(async () => { app?.unmount(); app = undefined; if (pinia) disposePinia(pinia); await flush(); browser.document.body.replaceChildren(); await browser.happyDOM.abort(); globalThis.fetch = previousFetch; });
after(async () => { await server.close(); await browser.happyDOM.close(); for (const [name, descriptor] of globals) { if (descriptor) Object.defineProperty(globalThis, name, descriptor); else delete globalThis[name]; } });
/** :returns: 已完成HTTP与Vue微任务。 */
async function flush() { await vue.nextTick(); await nextTurn(); await vue.nextTick(); }
/** :param label: 可见按钮。:returns: 真实DOM。 */
function button(label) { return [...browser.document.querySelectorAll('button')].find(node => visibleText(node.textContent) === visibleText(label)); }
/** :param kind: 公开地址输入或正式抽屉。:returns: 保留正式Store/API的隔离界面。 */
async function mount(kind = 'input') {
  calls = []; model = vue.ref(uri); active = vue.ref(true); pinia = createPinia(); useAuth(pinia).user = { id: 1, username: 'memory' };
  const source = { id: 1, name: '测试摄像机', uri, source_type: 'rtsp_stream', is_available: true, preheat_enabled: false, metadata: {} };
  globalThis.fetch = async (url, options = {}) => {
    assert.equal(new URL(String(url), browser.location.href).hostname, 'localhost');
    const path = new URL(String(url), browser.location.href).pathname, method = options.method || 'GET';
    const body = options.body ? JSON.parse(options.body) : null; calls.push({ path, method, body });
    return new Response(JSON.stringify(path === '/api/sources/' ? { sources: [source] } : path === '/api/folders/' ? { folders: [] }
      : { success: true, source: { ...source, uri: body?.url || body?.uri || uri } }), { headers: { 'Content-Type': 'application/json' } });
  };
  const host = browser.document.createElement('div'); browser.document.body.append(host);
  app = vue.createApp({ render: () => kind === 'input' ? vue.h(Component, { value: model.value, 'onUpdate:value': value => { model.value = value; }, active: active.value, label: '流地址' })
    : vue.h(kind === 'add' ? Add : Edit, { open: true, ...(kind === 'edit' ? { source } : {}) }) });
  app.use(pinia).use(i18n); app.mount(host); await flush();
}

for (const scheme of ['rtsp','rtmp','https','srt']) test(`${scheme}摘要隐藏凭据与敏感查询`, () => {
  const raw = uri.replace('rtsp:', scheme + ':'), shown = displayMediaAddress(raw);
  for (const secret of ['fixture-user', 'p%40ss%23word', 'fixture-token']) assert.equal(shown.includes(secret), false);
  assert.ok(shown.includes('192.0.2.188')); assert.ok(shown.includes('channel=5')); assert.equal(raw.includes('fixture-token'), true);
});
for (const raw of ['/media/测试.png', 'rtsp://192.0.2.1:554/live', 'udp://@239.1.2.3:1234', 'C:/media/test.mp4']) test('无秘密地址保持原文 '+raw, () => assert.equal(displayMediaAddress(raw), raw));
test('大小写/编码敏感键全部隐藏，重复键不漏出', () => {
  const shown = displayMediaAddress('srt://192.0.2.1/live?%74oken=one&TOKEN=two&passphrase=three&foo_secret=four');
  for (const secret of ['one','two','three','four']) assert.equal(shown.includes(secret), false);
});
test('真实Ant默认隐藏，明确查看后原值不变，离开即重新隐藏', async () => {
  await mount(); const input = browser.document.querySelector('input'); assert.equal(input.type, 'password'); assert.equal(input.value, uri);
  button('显示地址').click(); await flush(); assert.equal(input.type, 'text'); assert.equal(input.value, uri);
  active.value = false; await flush(); assert.equal(input.type, 'password'); assert.equal(model.value, uri); assert.deepEqual(calls, []);
});
test('编辑输入不替换为脱敏值', async () => {
  await mount(); const input = browser.document.querySelector('input'); const replacement = uri.replace('p%40ss%23word', 'changed%3Apass');
  input.value = replacement; input.dispatchEvent(new browser.Event('input', { bubbles: true })); await flush(); assert.equal(model.value, replacement);
});
test('新增抽屉提交完整带凭据地址', async () => {
  await mount('add'); const tab = [...browser.document.querySelectorAll('[role="tab"]')].find(node => visibleText(node.textContent) === '直播流');
  assert.ok(tab); tab.click(); await flush();
  const input = browser.document.querySelector('input[aria-label="流地址"]'); assert.equal(input.type, 'password'); input.value = uri;
  input.dispatchEvent(new browser.Event('input', { bubbles: true })); await flush(); button('添加直播源').click(); await flush(); await flush();
  assert.equal(calls.find(call => call.path === '/api/sources/streams/')?.body.url, uri);
});
test('编辑抽屉只把修改后的完整URI提交，不发送占位符', async () => {
  await mount('edit'); const input = browser.document.querySelector('input[aria-label="流地址"]'); assert.equal(input.type, 'password'); assert.equal(input.value, uri);
  const replacement = uri.replace('p%40ss%23word', 'changed%3Apass'); input.value = replacement; input.dispatchEvent(new browser.Event('input', { bubbles: true })); await flush();
  button('保存修改').click(); await flush(); await flush(); assert.equal(calls.find(call => call.method === 'PATCH')?.body.uri, replacement);
});
