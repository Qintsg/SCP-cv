/*
 * 大屏组合真实Vue/Naive UI DOM合同：能力、版本及发送边界。
 * @Project : SCP-cv
 * @File : video-wall-mapping.test.mjs
 * @Author : Qintsg
 * @Date : 2026-10-09
 */
import assert from 'node:assert/strict';
import { setImmediate as nextTurn } from 'node:timers/promises';
import test, { before, after, afterEach } from 'node:test';
import { Window } from 'happy-dom';
import { createDomTestServer } from './dom-test-server.mjs';

const browser = new Window({ url: 'http://localhost:5173/display/big-left' });
const names = ['window', 'document', 'navigator', 'HTMLElement', 'HTMLInputElement', 'HTMLTextAreaElement',
  'Element', 'Node', 'SVGElement', 'Document', 'MutationObserver', 'ResizeObserver', 'Image', 'InputEvent',
  'getComputedStyle', 'requestAnimationFrame', 'cancelAnimationFrame'];
const old = new Map(names.map((name) => [name, Object.getOwnPropertyDescriptor(globalThis, name)]));
for (const name of names) Object.defineProperty(globalThis, name, {
  value: typeof browser[name] === 'function' && /^[a-z]/.test(name) ? browser[name].bind(browser) : browser[name],
  configurable: true, writable: true,
});
const originalFetch = globalThis.fetch;
let server, vue, createPinia, i18n, Component, apiModule, app, requests;

before(async () => {
  [vue, { createPinia }, server] = await Promise.all([import('vue'), import('pinia'), createDomTestServer()]);
  ({ default: Component } = await server.ssrLoadModule('/src/features/runtime/VideoWallMappingPanel.vue'));
  ({ i18n } = await server.ssrLoadModule('/src/locales/index.ts'));
  apiModule = await server.ssrLoadModule('/src/services/api.ts');
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
  for (const [name, descriptor] of old) {
    if (descriptor) Object.defineProperty(globalThis, name, descriptor);
    else delete globalThis[name];
  }
});

/** :returns: 异步渲染及请求回调已刷新。 */
async function flush() { await vue.nextTick(); await nextTurn(); await vue.nextTick(); }
/** :param label: 按钮文字。 :returns: 真实渲染按钮。 */
function button(label) { return [...browser.document.querySelectorAll('button')].find((item) => item.textContent.trim() === label); }
/** :param draft: 服务器草稿。 :returns: 挂载完成的面板，HTTP边界被隔离。 */
async function mount(draft) {
  apiModule.clearApiSessionState();
  requests = [];
  let state = { draft, draft_revision: 7, active_preset: 'single', applied: null, applied_revision: 0,
    dispatch_status: 'none', dispatch_detail: '' };
  globalThis.fetch = async (url, options = {}) => {
    const path = new URL(String(url), browser.location.origin).pathname;
    const method = options.method ?? 'GET';
    const payload = options.body ? JSON.parse(options.body) : null;
    requests.push({ path, method, payload });
    const json = (body) => new Response(JSON.stringify(body), { headers: { 'Content-Type': 'application/json' } });
    if (path === '/api/auth/csrf/') return json({ csrfToken: 'qa-only' });
    if (path === '/api/video-wall/layout/apply/') {
      assert.equal(payload.expected_revision, 7);
      state = { ...state, applied: draft, applied_revision: 7, active_preset: 'custom',
        dispatch_status: 'simulated', dispatch_detail: '模拟应用；未向实体墙面发送命令。' };
      return json({ success: true, layout: state, runtime: { big_screen_mode: 'custom', muted_windows: [], volume_level: 100 }, sessions: [] });
    }
    assert.equal(path, '/api/video-wall/layout/');
    return json({ success: true, layout: state });
  };
  const host = browser.document.createElement('div');
  browser.document.body.append(host);
  app = vue.createApp(Component);
  app.use(createPinia());
  app.use(i18n);
  app.mount(host);
  await flush();
}

const kinds = ['window_1', 'window_2', 'laptop'];
const combinations = kinds.flatMap((left) => [[left], ...kinds.map((right) => [left, right])]);
for (const kinds of combinations) {
  test(`组合${kinds.join('|')}以服务端能力启用，提交保存版本并明确模拟结果`, async () => {
    const draft = { name: '组合', preset: '', can_apply: true, unavailable_reason: '', mappings: kinds.map((kind, index) => ({
      region: kinds.length === 1 ? 'fullscreen' : index === 0 ? 'left' : 'right', input: { kind, ip_address: '' },
    })) };
    await mount(draft);
    const apply = button('应用已保存布局');
    assert.ok(apply && !apply.disabled);
    if (kinds.includes('laptop')) assert.match(browser.document.querySelector('.wall-map').textContent, /IP81/);
    apply.click();
    await flush();
    assert.equal(requests.filter((item) => item.path.endsWith('/apply/')).length, 1);
    assert.match(browser.document.querySelector('.wall-map').textContent, /模拟.*未.*实体/);
  });
}

test('未登记IP流按服务器原因禁用，已保存表单修改后禁止应用旧布局', async () => {
  await mount({ name: '未登记', preset: '', mappings: [{ region: 'fullscreen', input: { kind: 'ip_stream', ip_address: '239.1.2.3' } }],
    can_apply: false, unavailable_reason: '缺少已确认的设备流描述' });
  assert.ok(button('应用已保存布局').disabled);
  assert.match(browser.document.querySelector('.wall-map').textContent, /缺少已确认/);
  assert.equal(requests.filter((item) => item.method === 'POST').length, 0);
});

test('编辑保存后的名称使应用禁用，不使用上次能力投影误发送', async () => {
  await mount({ name: '已保存', preset: '', mappings: [{ region: 'fullscreen', input: { kind: 'window_2' } }], can_apply: true, unavailable_reason: '' });
  const name = browser.document.querySelector('input');
  name.value = '未保存编辑';
  name.dispatchEvent(new browser.Event('input', { bubbles: true }));
  await flush();
  assert.ok(button('应用已保存布局').disabled);
});
