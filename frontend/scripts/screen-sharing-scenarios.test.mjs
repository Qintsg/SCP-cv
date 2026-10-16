/* 正式预案页DOM回归：临时共享不能经“从当前状态生成”静默保存，普通新建仍可用。 */
import { visibleText } from './dom-test-server.mjs';

import assert from 'node:assert/strict';
import test, { before, after, afterEach } from 'node:test';
import { setImmediate as nextTurn } from 'node:timers/promises';
import { Window } from 'happy-dom';
import { createDomTestServer } from './dom-test-server.mjs';

const browser = new Window({ url: 'http://localhost:5173/scenarios', width: 1440 });
const names = ['window', 'document', 'navigator', 'history', 'localStorage', 'HTMLElement', 'HTMLInputElement', 'HTMLTextAreaElement',
  'Element', 'Node', 'SVGElement', 'Document', 'MutationObserver', 'ResizeObserver', 'Image', 'InputEvent',
  'getComputedStyle', 'requestAnimationFrame', 'cancelAnimationFrame'];
const original = new Map(names.map((name) => [name, Object.getOwnPropertyDescriptor(globalThis, name)]));
for (const name of names) Object.defineProperty(globalThis, name, {
  value: typeof browser[name] === 'function' && /^[a-z]/.test(name) ? browser[name].bind(browser) : browser[name], configurable: true, writable: true,
});
const originalFetch = globalThis.fetch;
let server, vue, piniaModule, i18n, View, useSessionStore, useRuntimeStore, useToastStore, app, toast;
before(async () => {
  [vue, piniaModule, server] = await Promise.all([import('vue'), import('pinia'), createDomTestServer()]);
  ({ default: View } = await server.ssrLoadModule('/src/features/scenarios/ScenariosView.vue'));
  ({ i18n } = await server.ssrLoadModule('/src/locales/index.ts'));
  ({ useSessionStore } = await server.ssrLoadModule('/src/stores/sessions.ts'));
  ({ useRuntimeStore } = await server.ssrLoadModule('/src/stores/runtime.ts'));
  ({ useToastStore } = await server.ssrLoadModule('/src/composables/useToast.ts'));
});
afterEach(async () => {
  toast?.clear(); app?.unmount(); app = undefined; browser.document.body.replaceChildren();
  await browser.happyDOM.abort(); globalThis.fetch = originalFetch;
});
after(async () => {
  await server?.close(); await browser.happyDOM.close();
  for (const [name, descriptor] of original) { if (descriptor) Object.defineProperty(globalThis, name, descriptor); else delete globalThis[name]; }
});
/** :returns: 真实Vue渲染和按钮回调已刷新。 */
async function flush() { await vue.nextTick(); await nextTurn(); await vue.nextTick(); }

for (const windowId of [1, 2]) for (const field of ['share_id', 'actual_share_id', 'actual_adapter_kind']) {
  test(`窗口${windowId}/${field}临时共享拒绝捕获，零HTTP/永久源/编辑抽屉`, async () => {
    const requests = [];
    globalThis.fetch = async (url, options = {}) => { requests.push({ url: String(url), method: options.method ?? 'GET' }); throw new Error('被拒绝的捕获不应有HTTP'); };
    const pinia = piniaModule.createPinia();
    useRuntimeStore(pinia).runtime = { big_screen_mode: 'custom', muted_windows: [], volume_level: 100 };
    useSessionStore(pinia).applyRemoteSessions([1, 2].map((window_id) => ({ window_id, session_id: window_id,
      source_id: null, source_name: '无', source_type: '', source_uri: '', playback_mode: '', playback_state: 'idle', playback_state_label: '空闲',
      pending_command: '', player_online: true, error_message: '', current_slide: 0, total_slides: 0,
      volume: 100, is_muted: false, loop_enabled: false, position_ms: null, duration_ms: null, last_updated_at: '2026-10-11T00:00:00Z',
      ...(window_id === windowId ? { [field]: field === 'actual_adapter_kind' ? 'screen_share' : '0eb4ca16-54ef-49e9-889c-016b0a29aa61' } : {}),
    })));
    toast = useToastStore(pinia);
    app = vue.createApp(View); app.use(pinia); app.use(i18n);
    const host = browser.document.createElement('div'); browser.document.body.append(host); app.mount(host); await flush();
    const button = [...browser.document.querySelectorAll('button')].find((item) => visibleText(item.textContent) === visibleText('从当前状态生成'));
    assert.ok(button); button.click(); await flush();
    assert.equal(toast.items.length, 1);
    assert.equal(toast.items[0].level, 'warning');
    assert.match(toast.items[0].description, new RegExp(`窗口 ?${windowId}.*屏幕共享`));
    assert.equal(browser.document.querySelector('.ant-drawer'), null);
    assert.deepEqual(requests, []);
  });
}
