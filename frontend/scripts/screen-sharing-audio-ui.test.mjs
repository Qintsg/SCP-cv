/* 真实Vue/Naive UI音频操作与实际/设置分离；不模拟声音或原生采集。SCP-cv 2026-10-11。 */
import assert from 'node:assert/strict';
import test, { before, after, afterEach } from 'node:test';
import { setImmediate as nextTurn } from 'node:timers/promises';
import { Window } from 'happy-dom';
import { createDomTestServer } from './dom-test-server.mjs';

const browser = new Window({ url: 'http://localhost:5173/' });
const names = ['window', 'document', 'navigator', 'HTMLElement', 'HTMLInputElement', 'Element', 'Node', 'SVGElement', 'Document', 'ShadowRoot',
  'MutationObserver', 'ResizeObserver', 'Image', 'getComputedStyle', 'requestAnimationFrame', 'cancelAnimationFrame'];
const previous = new Map(names.map((name) => [name, Object.getOwnPropertyDescriptor(globalThis, name)]));
for (const name of names) Object.defineProperty(globalThis, name, { configurable: true, writable: true,
  value: typeof browser[name] === 'function' && /^[a-z]/.test(name) ? browser[name].bind(browser) : browser[name] });
let server, vue, Panel, i18n, app;
before(async () => {
  server = await createDomTestServer(); vue = await import('vue');
  ({ default: Panel } = await server.ssrLoadModule('/src/features/screen-share/ScreenShareAudioPanel.vue'));
  ({ i18n } = await server.ssrLoadModule('/src/locales/index.ts'));
});
afterEach(async () => { app?.unmount(); app = undefined; browser.document.body.replaceChildren(); await browser.happyDOM.abort(); });
after(async () => {
  await server?.close(); await browser.happyDOM.close();
  for (const [name, descriptor] of previous) { if (descriptor) Object.defineProperty(globalThis, name, descriptor); else delete globalThis[name]; }
});
/** :returns: 当前真实组件更新已完成。 */
async function flush() { await vue.nextTick(); await nextTurn(); await vue.nextTick(); }
/** :param name: 实际按钮名称。:returns: 已渲染按钮，不依赖内部组件实例。 */
function button(name) { const found = [...document.querySelectorAll('button')].find((node) => node.textContent.trim() === name); assert.ok(found, name); return found; }
/** :param patch: 外部实际状态。:returns: 可修改props及收到的明确用户意图。 */
async function mount(patch = {}) {
  const actions = [];
  const props = vue.reactive({ sharing: true, localHasAudio: true, audio: { phase: 'idle', errorCode: '' },
    snapshot: { id: '11111111-1111-4111-8111-111111111111', state: 'receiving', actual_frames: 10,
      receiver_has_audio: true, actual_volume: 100, actual_muted: true }, ...patch });
  const root = document.createElement('div'); document.body.append(root);
  app = vue.createApp({ render: () => vue.h(Panel, { ...props, onSetAudio: (volume, muted) => actions.push([volume, muted]) }) });
  app.use(i18n); app.mount(root); await flush(); return { props, actions };
}

test('设置音量不乐观覆盖实际值，明确应用才发一次动作', async () => {
  const { actions } = await mount(); const input = document.querySelector('input[type=number]');
  assert.equal(input.getAttribute('min'), '0'); assert.equal(input.getAttribute('max'), '100'); assert.ok(input.getAttribute('aria-label'));
  input.value = '29'; input.dispatchEvent(new browser.Event('input', { bubbles: true })); await flush();
  assert.deepEqual(actions, []); assert.match(document.querySelector('[role=status]').textContent, /实际音量 100/);
  button('应用音量').click(); await flush(); assert.deepEqual(actions, [[29, true]]);
  assert.match(document.querySelector('[role=status]').textContent, /实际音量 100/);
});
test('实际静音已知才提供开启声音；不使用尚未应用的音量草稿', async () => {
  const { actions } = await mount(); const input = document.querySelector('input'); input.value = '20';
  input.dispatchEvent(new browser.Event('input', { bubbles: true })); await flush(); button('开启声音').click(); await flush();
  assert.deepEqual(actions, [[100, false]]);
});
test('未知状态不猜成已开启，只允许先请求静音', async () => {
  const { props, actions } = await mount({ snapshot: { id: 'a', state: 'receiving', actual_frames: 10, receiver_has_audio: true, actual_volume: null, actual_muted: null } });
  assert.match(document.querySelector('[role=status]').textContent, /实际音量\/静音尚未确认/);
  button('先设为静音').click(); await flush(); assert.deepEqual(actions, [[100, true]]);
  props.snapshot = { ...props.snapshot, actual_volume: 0, actual_muted: false }; await flush();
  assert.match(document.querySelector('[role=status]').textContent, /实际音量 0/); button('静音').click(); await flush();
  assert.deepEqual(actions.at(-1), [0, true]);
});
test('等待命令与实际确认分开，等待期间全部音频操作禁用', async () => {
  const { props, actions } = await mount({ audio: { phase: 'pending', errorCode: '' } });
  assert.ok(document.querySelector('input').disabled, document.body.innerHTML); assert.ok(button('应用音量').disabled); assert.ok(button('开启声音').disabled);
  button('应用音量').click(); assert.deepEqual(actions, []); assert.match(document.body.textContent, /等待播放器应用并回读实际属性/);
  props.audio = { phase: 'confirmed', errorCode: '' }; await flush(); assert.match(document.body.textContent, /播放器已确认本次音频设置/);
});
for (const patch of [{ localHasAudio: false }, { snapshot: { id: 'a', receiver_has_audio: false } }, { sharing: false }]) {
  test(`无音轨/未出画禁用${JSON.stringify(patch)}并说明原因`, async () => {
    await mount(patch); assert.ok(document.querySelector('input').disabled); assert.ok(button('应用音量').disabled);
    assert.match(document.body.textContent, /未提供内容音轨|接收端尚无内容音轨|实际接收画面后/);
  });
}
test('非法/空/小数音量不能应用，输入草稿不被周期回读覆盖', async () => {
  const { props, actions } = await mount(); const input = document.querySelector('input');
  for (const value of ['', '-1', '101', '29.5']) {
    input.value = value; input.dispatchEvent(new browser.Event('input', { bubbles: true })); await flush();
    assert.ok(button('应用音量').disabled); button('应用音量').click(); assert.deepEqual(actions, []);
  }
  input.value = '29'; input.dispatchEvent(new browser.Event('input', { bubbles: true })); await flush();
  props.snapshot = { ...props.snapshot, actual_volume: 80 }; await flush(); assert.equal(input.value, '29');
  assert.match(document.querySelector('[role=status]').textContent, /实际音量 80/);
});
test('未确认/错误明确提示不重试；新分享重置旧音量草稿', async () => {
  const { props, actions } = await mount({ audio: { phase: 'unconfirmed', errorCode: 'share_audio_unconfirmed' } });
  assert.match(document.body.textContent, /未确认本次音频设置|不会自动重发/); assert.deepEqual(actions, []);
  props.audio = { phase: 'failed', errorCode: 'share_audio_identity_invalid' }; await flush();
  assert.match(document.body.textContent, /身份不匹配/);
  const input = document.querySelector('input'); input.value = '29'; input.dispatchEvent(new browser.Event('input', { bubbles: true })); await flush();
  props.snapshot = { ...props.snapshot, id: 'new', actual_volume: 80 }; await flush(); assert.equal(input.value, '80');
});
