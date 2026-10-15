/* 真实应用外壳与显示页回归：两逻辑窗口不按墙面模式隐藏，路由选择没有物理写操作。SCP-cv 2026-10-11。 */
import assert from 'node:assert/strict';
import test, { before, after, afterEach } from 'node:test';
import { setImmediate as nextTurn } from 'node:timers/promises';
import { Window } from 'happy-dom';
import { createDomTestServer } from './dom-test-server.mjs';

const browser = new Window({ url: 'http://localhost:5173/display/big-right', width: 1440 });
const names = ['window', 'document', 'navigator', 'history', 'localStorage', 'HTMLElement', 'HTMLInputElement', 'HTMLTextAreaElement',
  'Element', 'Node', 'SVGElement', 'Document', 'MutationObserver', 'ResizeObserver', 'Image', 'InputEvent',
  'getComputedStyle', 'requestAnimationFrame', 'cancelAnimationFrame'];
const original = new Map(names.map((name) => [name, Object.getOwnPropertyDescriptor(globalThis, name)]));
for (const name of names) Object.defineProperty(globalThis, name, {
  value: typeof browser[name] === 'function' && /^[a-z]/.test(name) ? browser[name].bind(browser) : browser[name], configurable: true, writable: true,
});
const originalFetch = globalThis.fetch;
let server, vue, piniaModule, routerModule, i18n, Shell, Display, useSessionStore, useRuntimeStore, app;
before(async () => {
  [vue, piniaModule, routerModule, server] = await Promise.all([import('vue'), import('pinia'), import('vue-router'), createDomTestServer()]);
  ({ default: Shell } = await server.ssrLoadModule('/src/layouts/AppShell.vue'));
  ({ default: Display } = await server.ssrLoadModule('/src/features/display/DisplayControlView.vue'));
  ({ i18n } = await server.ssrLoadModule('/src/locales/index.ts'));
  ({ useSessionStore } = await server.ssrLoadModule('/src/stores/sessions.ts'));
  ({ useRuntimeStore } = await server.ssrLoadModule('/src/stores/runtime.ts'));
});
afterEach(async () => { app?.unmount(); app = undefined; browser.document.body.replaceChildren(); await browser.happyDOM.abort(); globalThis.fetch = originalFetch; });
after(async () => {
  await server?.close(); await browser.happyDOM.close();
  for (const [name, descriptor] of original) { if (descriptor) Object.defineProperty(globalThis, name, descriptor); else delete globalThis[name]; }
});
/** :returns: 真实Vue渲染和路由回调已刷新。 */
async function flush() { await vue.nextTick(); await nextTurn(); await vue.nextTick(); }

for (const mode of ['single', 'double', 'custom']) for (const width of [1440, 390]) {
  test(`${mode}/${width}墙面布局仍可导航两逻辑窗口，不发送布局或播放器控制`, async () => {
    browser.innerWidth = width;
    const requests = [];
    globalThis.fetch = async (url, options = {}) => { requests.push({ url: String(url), method: options.method ?? 'GET' }); throw new Error('路由导航不需要HTTP副作用'); };
    const pinia = piniaModule.createPinia();
    useRuntimeStore(pinia).runtime = { big_screen_mode: mode, muted_windows: [], volume_level: 100 };
    useSessionStore(pinia).applyRemoteSessions([1, 2].map((window_id) => ({ window_id, session_id: window_id,
      source_id: null, source_name: '无', source_type: '', source_uri: '', playback_mode: '', playback_state: 'idle', playback_state_label: '空闲',
      pending_command: '', player_online: true, error_message: '', current_slide: 0, total_slides: 0,
      volume: 100, is_muted: false, loop_enabled: false, position_ms: null, duration_ms: null, last_updated_at: '2026-10-11T00:00:00Z',
    })));
    const router = routerModule.createRouter({ history: routerModule.createMemoryHistory(), routes: [
      { path: '/display/:target', component: Display },
      { path: '/:pathMatch(.*)*', component: { render: () => null } },
    ] });
    app = vue.createApp(Shell); app.use(pinia); app.use(i18n); app.use(router);
    await router.push('/display/big-right'); await router.isReady();
    const host = browser.document.createElement('div'); browser.document.body.append(host); app.mount(host); await flush();
    if (width === 1440) {
      const nav = browser.document.querySelector('a[href="/display/big-right"]');
      assert.ok(nav, '窗口2导航必须存在'); assert.match(nav.textContent, /窗口 2/);
    } else {
      assert.ok(browser.document.querySelector('input[type="radio"][value="big-right"]'), '窄屏保留窗口2');
      const sourceTab = browser.document.querySelector('[role="tab"][aria-selected="true"]');
      assert.ok(sourceTab, '窄屏页签必须可被辅助技术识别和键盘操作');
      sourceTab.dispatchEvent(new browser.KeyboardEvent('keydown', { key: 'ArrowRight', bubbles: true })); await flush();
      assert.equal(browser.document.activeElement?.textContent.trim(), '播放控制');
      assert.equal(browser.document.activeElement?.getAttribute('aria-selected'), 'true');
    }
    assert.equal(browser.document.querySelectorAll('.display-view h1').length, 1);
    assert.match(browser.document.querySelector('.display-view h1')?.textContent ?? '', /窗口 2/);
    assert.match(browser.document.querySelector('.screen-share__target')?.textContent ?? '', /窗口 2/);
    assert.ok(browser.document.querySelector('.playback-control'), '窗口2控制区不能被单屏旧判断遮住');
    await router.push('/display/big-left'); await flush();
    assert.match(browser.document.querySelector('.screen-share__target')?.textContent ?? '', /窗口 1/);
    assert.deepEqual(requests, []);
  });
}
