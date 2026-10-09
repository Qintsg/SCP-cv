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
      assert.equal(payload.expected_revision, state.draft_revision);
      state = { ...state, applied: state.draft, applied_revision: state.draft_revision, active_preset: 'custom',
        dispatch_status: 'simulated', dispatch_detail: '模拟应用；未向实体墙面发送命令。' };
      return json({ success: true, layout: state, runtime: { big_screen_mode: 'custom', muted_windows: [], volume_level: 100 }, sessions: [] });
    }
    assert.equal(path, '/api/video-wall/layout/');
    if (method === 'PUT') state = { ...state, draft_revision: state.draft_revision + 1,
      draft: { ...payload, can_apply: true, unavailable_reason: '' } };
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

test('草稿徽标只表示保存状态，不冒充实体画面确认', async () => {
  await mount({ name: '已保存', mappings: [{ region: 'fullscreen', input: { kind: 'window_1' } }], can_apply: true });
  const badge = browser.document.querySelector('[data-wall-draft]');
  assert.ok(badge, '应有独立的草稿状态徽标');
  assert.equal(badge.getAttribute('role'), 'status');
  assert.match(badge.textContent, /草稿已保存/);
  assert.match(browser.document.querySelector('[data-wall-dispatch]').textContent, /尚未发送/);
  assert.equal(requests.filter((item) => item.method !== 'GET').length, 0);
});

test('留空区域有明确样式，但选源保持可用', async () => {
  await mount({ name: '只映射右侧', mappings: [{ region: 'right', input: { kind: 'window_1' } }], can_apply: true });
  assert.equal(browser.document.querySelectorAll('.wall-map__region--empty').length, 1);
  await chooseInput('大屏左输入来源', '窗口 2');
  assert.equal(browser.document.querySelectorAll('.wall-map__region--empty').length, 0);
  assert.ok(button('应用已保存布局').disabled);
  assert.equal(requests.filter((item) => item.method !== 'GET').length, 0);
});
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

/** :param label: 可访问输入区域。:param choice: 菜单文案。:returns: 用户选择完成。 */
async function chooseInput(label, choice) {
  const select = browser.document.querySelector(`.wall-map__screen .n-select[aria-label="${label}"] .n-base-selection`);
  assert.ok(select, `输入选择应位于屏幕区域内：${label}`);
  const choices = ['窗口 1', '窗口 2', 'IP81', '不映射', '自定义 IP 流'];
  const current = choices.findIndex((item) => select.textContent.includes(item));
  const target = choices.indexOf(choice);
  assert.ok(current >= 0 && target >= 0, '键盘选择使用明确的当前和目标输入');
  select.click();
  await flush();
  // Naive缓存离场菜单；不从全局DOM误点上一选择区的隐藏选项，按公开键盘规则选源。
  const key = target > current ? 'ArrowDown' : 'ArrowUp';
  for (let index = 0; index < Math.abs(target - current); index++) {
    select.dispatchEvent(new browser.KeyboardEvent('keydown', { key, bubbles: true }));
    await flush();
  }
  select.dispatchEvent(new browser.KeyboardEvent('keydown', { key: 'Enter', bubbles: true }));
  await flush();
  assert.ok(select.textContent.includes(choice));
}

/** :param label: 单屏或双屏。:returns: 布局已通过真实输入切换。 */
async function chooseLayout(label) {
  const radio = [...browser.document.querySelectorAll('.n-radio-button')]
    .find((item) => item.textContent.trim() === label);
  assert.ok(radio, '应有单屏/双屏分段选择');
  radio.click();
  await flush();
}

test('单屏只有一个区域内输入选择，双屏只有两个，不罗列组合按钮', async () => {
  await mount({ name: '已保存', mappings: [{ region: 'fullscreen', input: { kind: 'window_2' } }], can_apply: true });
  assert.equal(browser.document.querySelectorAll('.wall-map__screen .n-select').length, 1);
  assert.match(browser.document.querySelector('.wall-map__screen').textContent, /窗口 2/);
  assert.deepEqual([...browser.document.querySelectorAll('.n-radio-button')].map((item) => item.textContent.trim()), ['单屏', '双屏']);
  assert.equal(browser.document.querySelector('.wall-map__options').open, false);
  await chooseLayout('双屏');
  assert.equal(browser.document.querySelectorAll('.wall-map__screen .n-select').length, 2);
  assert.equal(requests.filter((item) => item.method !== 'GET').length, 0);
});

test('单双屏往返保留各区域选择，选择过程不保存或应用墙面', async () => {
  await mount({ name: '双屏', mappings: [
    { region: 'left', input: { kind: 'window_1' } }, { region: 'right', input: { kind: 'window_2' } },
  ], can_apply: true });
  await chooseInput('大屏左输入来源', '窗口 2');
  await chooseInput('大屏右输入来源', 'IP81');
  await chooseLayout('单屏');
  await chooseInput('全屏输入来源', '窗口 1');
  await chooseLayout('双屏');
  assert.match(browser.document.querySelector('[aria-label="大屏左输入来源"]').textContent, /窗口 2/);
  assert.match(browser.document.querySelector('[aria-label="大屏右输入来源"]').textContent, /IP81/);
  assert.ok(button('应用已保存布局').disabled);
  assert.equal(requests.filter((item) => item.method !== 'GET').length, 0);
});

test('区域内相同IP81可保存并带新修订应用，不增加播放器请求', async () => {
  await mount({ name: '双屏', mappings: [
    { region: 'left', input: { kind: 'window_1' } }, { region: 'right', input: { kind: 'window_2' } },
  ], can_apply: true });
  await chooseInput('大屏左输入来源', 'IP81');
  await chooseInput('大屏右输入来源', 'IP81');
  button('保存布局草稿').click();
  await flush();
  const saved = requests.find((item) => item.method === 'PUT');
  assert.deepEqual(saved.payload.mappings, [
    { region: 'left', input: { kind: 'laptop', ip_address: '' } },
    { region: 'right', input: { kind: 'laptop', ip_address: '' } },
  ]);
  assert.ok(!button('应用已保存布局').disabled);
  button('应用已保存布局').click();
  await flush();
  assert.equal(requests.find((item) => item.method === 'POST').payload.expected_revision, 8);
  assert.equal(requests.filter((item) => item.path.includes('/playback/')).length, 0);
});

test('编辑保存后的名称使应用禁用，不使用上次能力投影误发送', async () => {
  await mount({ name: '已保存', preset: '', mappings: [{ region: 'fullscreen', input: { kind: 'window_2' } }], can_apply: true, unavailable_reason: '' });
  const name = browser.document.querySelector('input[aria-label="布局名称"]');
  name.value = '未保存编辑';
  name.dispatchEvent(new browser.Event('input', { bubbles: true }));
  await flush();
  assert.ok(button('应用已保存布局').disabled);
});
