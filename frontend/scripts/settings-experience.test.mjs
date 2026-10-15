/** 设置真实组件的延迟请求、固定目标和密码表单回归；所有网络均为内存替身。 */
import assert from 'node:assert/strict';
import test, { before, after, afterEach } from 'node:test';
import { setImmediate as nextTurn } from 'node:timers/promises';
import { Window } from 'happy-dom';
import { createDomTestServer } from './dom-test-server.mjs';

const browser = new Window({ url: 'http://localhost:5173/settings' });
const names = ['window', 'document', 'navigator', 'history', 'location', 'HTMLElement', 'HTMLInputElement',
  'HTMLTextAreaElement', 'Element', 'Node', 'SVGElement', 'Document', 'MutationObserver', 'ResizeObserver',
  'Image', 'InputEvent', 'KeyboardEvent', 'PointerEvent', 'getComputedStyle', 'requestAnimationFrame', 'cancelAnimationFrame'];
const previous = new Map(names.map(name => [name, Object.getOwnPropertyDescriptor(globalThis, name)]));
for (const name of names) Object.defineProperty(globalThis, name, { configurable: true, writable: true,
  value: typeof browser[name] === 'function' && /^[a-z]/.test(name) ? browser[name].bind(browser) : browser[name] });
const originalFetch = globalThis.fetch;
let server, vue, createPinia, routers, i18n, DisplaySettings, AccountSettings;
let useDisplays, useRuntime, useAuth, useToasts, clientConnection, app;
let requests = [];

before(async () => {
  [vue, { createPinia }, routers, server] = await Promise.all([
    import('vue'), import('pinia'), import('vue-router'), createDomTestServer(),
  ]);
  ({ i18n } = await server.ssrLoadModule('/src/locales/index.ts'));
  ({ default: DisplaySettings } = await server.ssrLoadModule('/src/features/settings/tabs/DisplaySettingsTab.vue'));
  ({ default: AccountSettings } = await server.ssrLoadModule('/src/features/settings/tabs/AccountSettingsTab.vue'));
  ({ useDisplayStore: useDisplays } = await server.ssrLoadModule('/src/stores/displays.ts'));
  ({ useRuntimeStore: useRuntime } = await server.ssrLoadModule('/src/stores/runtime.ts'));
  ({ useAuthStore: useAuth } = await server.ssrLoadModule('/src/stores/auth.ts'));
  ({ useToastStore: useToasts } = await server.ssrLoadModule('/src/composables/useToast.ts'));
  ({ clientConnection } = await server.ssrLoadModule('/src/platform/connection.ts'));
});
/** :returns: Promise及Vue响应式更新完成。 */
async function flush() { await nextTurn(); await vue.nextTick(); await nextTurn(); await vue.nextTick(); }
afterEach(async () => {
  app?.unmount(); app = undefined;
  for (const request of requests) request.resolve(response(request.path));
  await flush(); browser.document.body.replaceChildren(); await browser.happyDOM.abort(); globalThis.fetch = originalFetch;
});
after(async () => {
  await server?.close(); await browser.happyDOM.close();
  for (const [name, descriptor] of previous) {
    if (descriptor) Object.defineProperty(globalThis, name, descriptor); else delete globalThis[name];
  }
});
/** :param path: 被延迟的API路径。:returns: 只用于隔离测试的成功回执。 */
function response(path) {
  return new Response(JSON.stringify(path.includes('displays') ? { success: true, sessions: [] } : { detail: '已保存' }),
    { status: 200, headers: { 'content-type': 'application/json' } });
}
/** :param account: 是否挂账户表单。:returns: 原组件、store和内存路由。 */
async function mount(account = false) {
  requests = [];
  globalThis.fetch = (input, options = {}) => new Promise(resolve => {
    const path = new URL(String(input), browser.location.href).pathname;
    assert.ok(['/api/displays/select/', '/api/auth/change-password/'].includes(path), path);
    requests.push({ path, body: options.body && JSON.parse(options.body), resolve });
  });
  const pinia = createPinia();
  const auth = useAuth(pinia); auth.user = { id: 7, username: 'fixture', permissions: [] };
  const displays = useDisplays(pinia);
  displays.targets = [{ index: 0, name: 'Output-A', width: 1920, height: 1080, x: 1920, y: 0, is_playback_target: true, playback_role: 'big_left' },
    { index: 1, name: 'Output-B', width: 1920, height: 1080, x: 3840, y: 0, is_playback_target: true, playback_role: 'big_right' }];
  const router = routers.createRouter({ history: routers.createMemoryHistory(), routes: [{ path: '/settings', component: account ? AccountSettings : DisplaySettings },
    { path: '/login', component: { render: () => null } }, { path: '/:pathMatch(.*)*', component: { render: () => null } }] });
  app = vue.createApp(account ? AccountSettings : DisplaySettings); app.use(pinia); app.use(i18n); app.use(router);
  await router.push('/settings'); await router.isReady();
  const root = browser.document.createElement('div'); browser.document.body.append(root); app.mount(root); await flush();
  return { root, auth, runtime: useRuntime(pinia), toasts: useToasts(pinia) };
}
/** :param root: 组件容器。:returns: 显示器应用入口。 */
function applyButton(root) { return [...root.querySelectorAll('button')].find(button => /应用到窗口/.test(button.textContent)); }
/** :param root: 账户容器。:param confirmation: 确认密码。:returns: 原生输入更新完成。 */
async function fillPasswords(root, confirmation = 'NewFixture-123') {
  const values = ['OldFixture-123', 'NewFixture-123', confirmation];
  [...root.querySelectorAll('input')].forEach((input, index) => {
    input.value = values[index]; input.dispatchEvent(new browser.Event('input', { bubbles: true }));
  });
  await flush();
}
/** :param root: 账户容器。:returns: 真实表单提交事件，覆盖回车默认提交路径。 */
function submit(root) {
  const form = root.querySelector('form'); assert.ok(form, '密码修改应为原生表单');
  form.dispatchEvent(new browser.Event('submit', { bubbles: true, cancelable: true }));
}

test('显示器应用同一渲染轮重复点击只提交一次，目标选择在途锁定', async () => {
  const { root, toasts } = await mount(); root.querySelector('.settings-view__display-tile').click(); await flush();
  const apply = applyButton(root); apply.click(); apply.click(); await flush();
  assert.equal(requests.length, 1);
  assert.deepEqual(requests[0].body, { window_id: 1, display_mode: 'single', target_label: 'Output-A' });
  assert.ok([...root.querySelectorAll('input[type="radio"]')].every(input => input.disabled));
  assert.ok(root.querySelector('.settings-view__display-tile').disabled); assert.ok(applyButton(root).disabled);
  root.querySelector('input[value="2"]').click(); await flush();
  requests[0].resolve(response(requests[0].path)); await flush();
  assert.match(toasts.items.at(-1)?.message ?? '', /窗口 1/); assert.equal(applyButton(root).disabled, false);
});

test('显示器绑定失败后保留选择并恢复显式重试，没有自动重发', async () => {
  const { root, toasts } = await mount(); root.querySelector('.settings-view__display-tile').click(); await flush();
  applyButton(root).click(); await flush();
  requests[0].resolve(new Response(JSON.stringify({ detail: '测试显示器暂不可用' }), { status: 400 })); await flush();
  assert.equal(requests.length, 1); assert.equal(applyButton(root).disabled, false);
  assert.match(toasts.items.at(-1)?.description ?? '', /测试显示器暂不可用/);
  applyButton(root).click(); await flush();
  assert.equal(requests.length, 2); assert.deepEqual(requests[1].body, requests[0].body);
});

for (const invalidation of ['host', 'account', 'unmount']) {
  test(`显示器请求${invalidation}后迟到反馈不进入当前界面`, async () => {
    const { root, runtime, auth, toasts } = await mount(); root.querySelector('.settings-view__display-tile').click(); await flush();
    applyButton(root).click(); await flush();
    if (invalidation === 'host') runtime.connectionGeneration++;
    if (invalidation === 'account') { auth.user = null; auth.user = { id: 7, username: 'fixture', permissions: [] }; }
    if (invalidation === 'unmount') { app.unmount(); app = undefined; }
    await flush(); requests[0].resolve(response(requests[0].path)); await flush();
    assert.equal(toasts.items.length, 0);
    if (invalidation !== 'unmount') assert.ok(applyButton(root).disabled, '旧显示器选择必须失效');
  });
}

for (const account of [false, true]) {
  test(`${account ? '密码' : '显示器'}在真实主机代次已变但清会话未结束时也忽略旧响应`, async () => {
    const { root, runtime, toasts } = await mount(account);
    if (account) { await fillPasswords(root); submit(root); }
    else { root.querySelector('.settings-view__display-tile').click(); await flush(); applyButton(root).click(); }
    await flush();
    const oldRuntimeGeneration = runtime.connectionGeneration;
    let finishCleanup;
    const switching = clientConnection.switchServer({ origin: 'http://localhost:5173', displayName: '隔离测试' }, {
      clearSession: () => new Promise(resolve => { finishCleanup = resolve; }),
    });
    try {
      assert.equal(runtime.connectionGeneration, oldRuntimeGeneration);
      assert.notEqual(clientConnection.captureGeneration(), oldRuntimeGeneration);
      requests[0].resolve(response(requests[0].path)); await flush();
      assert.equal(toasts.items.length, 0);
      if (account) assert.ok([...root.querySelectorAll('input')].every(input => input.value === ''));
      else assert.ok(applyButton(root).disabled);
    } finally { finishCleanup(); await switching; }
  });
}

test('密码原生输入均有名称；不一致错误关联到确认输入且零写入', async () => {
  const { root } = await mount(true);
  assert.deepEqual([...root.querySelectorAll('input')].map(input => input.getAttribute('aria-label')), ['当前密码', '新密码', '确认新密码']);
  await fillPasswords(root, 'MismatchFixture'); submit(root); await flush();
  const confirmation = root.querySelector('input[aria-label="确认新密码"]');
  assert.equal(confirmation.getAttribute('aria-invalid'), 'true');
  const description = confirmation.getAttribute('aria-describedby'); assert.ok(description);
  assert.match(browser.document.getElementById(description)?.textContent ?? '', /两次输入的新密码不一致/);
  assert.equal(requests.length, 0);
});

test('密码表单连续回车只提交一次，完成后清空敏感字段并可继续使用', async () => {
  const { root, toasts } = await mount(true); await fillPasswords(root); submit(root); submit(root); await flush();
  assert.equal(requests.length, 1);
  assert.deepEqual(requests[0].body, { current_password: 'OldFixture-123', new_password: 'NewFixture-123' });
  assert.ok([...root.querySelectorAll('input')].every(input => input.disabled));
  requests[0].resolve(response(requests[0].path)); await flush();
  assert.ok([...root.querySelectorAll('input')].every(input => input.value === '' && !input.disabled));
  assert.match(toasts.items.at(-1)?.message ?? '', /密码已修改/);
});

test('密码失败信息可由原生输入找到，恢复编辑且不会自动重试', async () => {
  const { root } = await mount(true); await fillPasswords(root); submit(root); await flush();
  requests[0].resolve(new Response(JSON.stringify({ detail: '测试当前密码不正确' }), { status: 400 })); await flush();
  const current = root.querySelector('input[aria-label="当前密码"]');
  assert.equal(current.disabled, false); assert.equal(current.value, 'OldFixture-123');
  assert.match(browser.document.getElementById(current.getAttribute('aria-describedby'))?.textContent ?? '', /测试当前密码不正确/);
  assert.equal(requests.length, 1);
});

for (const invalidation of ['host', 'account', 'unmount']) {
  test(`密码请求${invalidation}后清除旧表单并忽略迟到反馈`, async () => {
    const { root, runtime, auth, toasts } = await mount(true); await fillPasswords(root); submit(root); await flush();
    if (invalidation === 'host') runtime.connectionGeneration++;
    if (invalidation === 'account') auth.user = { id: 8, username: 'other', permissions: [] };
    if (invalidation === 'unmount') { app.unmount(); app = undefined; }
    await flush(); requests[0].resolve(response(requests[0].path)); await flush();
    assert.equal(toasts.items.length, 0);
    if (invalidation !== 'unmount') assert.ok([...root.querySelectorAll('input')].every(input => input.value === ''));
  });
}
