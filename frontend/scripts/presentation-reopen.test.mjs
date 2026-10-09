/*
 * 文稿重开真实DOM回归：HTTP/SSE实际观测决定重开的资源和文字布局。
 * HTTP只在fetch边界替换，不启动原生引擎或控制现场设备。
 * @Project : SCP-cv
 * @File : presentation-reopen.test.mjs
 * @Author : Qintsg
 * @Date : 2026-10-02
 */
import assert from 'node:assert/strict';
import { setImmediate as nextTurn } from 'node:timers/promises';
import test, { after, afterEach, before } from 'node:test';
import { Window } from 'happy-dom';
import { createDomTestServer } from './dom-test-server.mjs';

const browser = new Window({ url: 'http://localhost:5173/display' });
const globalNames = ['window', 'document', 'navigator', 'history', 'HTMLElement', 'HTMLInputElement',
  'HTMLTextAreaElement', 'Element', 'Node', 'SVGElement', 'Document', 'MutationObserver',
  'ResizeObserver', 'Image', 'InputEvent', 'getComputedStyle', 'requestAnimationFrame', 'cancelAnimationFrame'];
const previousGlobals = new Map(globalNames.map((name) => [name, Object.getOwnPropertyDescriptor(globalThis, name)]));
for (const name of globalNames) {
  const value = typeof browser[name] === 'function' && /^[a-z]/.test(name)
    ? browser[name].bind(browser) : browser[name];
  Object.defineProperty(globalThis, name, { value, configurable: true, writable: true });
}

let server;
let vue;
let piniaModule;
let routerModule;
let PlaybackControl;
let useSessionStore;
let i18n;
let app;
let requests;
const originalFetch = globalThis.fetch;

before(async () => {
  [vue, piniaModule, routerModule, server] = await Promise.all([
    import('vue'), import('pinia'), import('vue-router'), createDomTestServer(),
  ]);
  ({ default: PlaybackControl } = await server.ssrLoadModule('/src/features/display/PlaybackControl.vue'));
  ({ useSessionStore } = await server.ssrLoadModule('/src/stores/sessions.ts'));
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

/** :returns: Vue及已受理HTTP回调已完成本轮渲染。 */
async function flushUi() {
  await vue.nextTick();
  await nextTurn();
  await vue.nextTick();
}

/**
 * 经公开store状态挂载播放控制，不替换组件内部逻辑。
 * :param observation: 当前服务端会话的实际观测。
 * :returns: 可用于模拟后续SSE更新的公开store。
 */
async function mountPlayback(observation) {
  const session = {
    window_id: 1, session_id: 1, source_id: 41, source_name: '请求文稿', source_type: 'ppt',
    source_uri: 'deck.pptx', source_type_label: 'PPT', playback_mode: 'libreoffice',
    playback_state: 'error', playback_state_label: '失败', error_message: '测试打开失败',
    current_slide: 1, total_slides: 9, position_ms: null, duration_ms: null,
    volume: 100, is_muted: false, loop_enabled: false, player_online: true,
    pending_command: '', last_updated_at: '2026-10-02T00:00:00Z', ...observation,
  };
  requests = [];
  globalThis.fetch = async (url, options = {}) => {
    requests.push({ url: String(url), method: options.method ?? 'GET', body: options.body });
    return new Response(JSON.stringify({ success: true, csrfToken: 'test-token', resources: [], sessions: [session] }),
      { headers: { 'Content-Type': 'application/json' } });
  };
  const pinia = piniaModule.createPinia();
  const store = useSessionStore(pinia);
  store.applyRemoteSessions([session]);
  app = vue.createApp({ setup: () => () => vue.h(PlaybackControl, { session: store.byWindowId(1) }) });
  app.use(pinia);
  app.use(i18n);
  const router = routerModule.createRouter({ history: routerModule.createMemoryHistory(), routes: [
    { path: '/', component: { render: () => null } },
    { path: '/display', component: { render: () => null } },
    { path: '/ppt-focus/:id', component: { render: () => null } },
  ] });
  app.use(router);
  await router.push('/display');
  const host = browser.document.createElement('div');
  browser.document.body.append(host);
  app.mount(host);
  await flushUi();
  return store;
}

/** :returns: 用户可见的重开按钮，不通过组件私有函数触发。 */
function reopenButton() {
  const button = [...browser.document.querySelectorAll('button')]
    .find((candidate) => /^(重新打开|再次打开)/.test(candidate.textContent.trim()));
  assert.ok(button, '错误反馈中应有显式重开动作');
  return button;
}

/** :returns: 当前测试在HTTP边界观察到的OPEN请求参数。 */
function openBodies() {
  return requests.filter((item) => item.url.endsWith('/playback/1/open/') && item.method === 'POST')
    .map((item) => JSON.parse(item.body));
}

test('重新打开当前画面源沿用实际适配布局，而不打开失败的期望新源', async () => {
  await mountPlayback({ source_id: 41, actual_source_id: 40, actual_adapter_kind: 'libreoffice', actual_fit_text: true });
  reopenButton().click();
  await flushUi();
  assert.deepEqual(openBodies(), [{ source_id: 40, autoplay: true, presentation_mode: 'libreoffice', fit_text: true }]);
  assert.equal(reopenButton().textContent.trim(), '重新打开当前画面源');
  assert.match(browser.document.body.textContent, /实际文字适配/);
});

test('明确没有实际源时禁用重开，不回退到期望源', async () => {
  await mountPlayback({ source_id: 41, actual_source_id: null, actual_adapter_kind: '', actual_fit_text: null });
  assert.equal(reopenButton().disabled, true);
  reopenButton().click();
  await flushUi();
  assert.deepEqual(openBodies(), []);
});

test('已确认原排版和静态适配器同样沿用实际资源，而不是期望模式', async () => {
  await mountPlayback({ source_id: 41, playback_mode: '', actual_source_id: 40,
    actual_adapter_kind: 'slide_images', actual_fit_text: false });
  reopenButton().click();
  await flushUi();
  assert.deepEqual(openBodies(), [{ source_id: 40, autoplay: true, presentation_mode: 'slide_images', fit_text: false }]);
  assert.match(browser.document.body.textContent, /实际原排版/);
});

test('旧会话完全缺少实际源字段时保持原source_id兼容，未知布局仍为默认原排版', async () => {
  await mountPlayback({});
  reopenButton().click();
  await flushUi();
  assert.deepEqual(openBodies(), [{ source_id: 41, autoplay: true, presentation_mode: 'libreoffice', fit_text: false }]);
});

test('较旧SSE不能覆盖当前实际文字布局，重开继续使用最新观测', async () => {
  const store = await mountPlayback({ actual_source_id: 40, actual_adapter_kind: 'libreoffice', actual_fit_text: true,
    last_updated_at: '2026-10-02T00:00:02Z' });
  store.applyRemoteSessions([{ ...store.byWindowId(1), actual_fit_text: false, last_updated_at: '2026-10-02T00:00:01Z' }]);
  await flushUi();
  reopenButton().click();
  await flushUi();
  assert.deepEqual(openBodies(), [{ source_id: 40, autoplay: true, presentation_mode: 'libreoffice', fit_text: true }]);
});
