/* 实际Vue共享面板的许可/反馈DOM合同；不含实际采集或媒体传输。SCP-cv 2026-10-11。 */
import assert from 'node:assert/strict';
import test, { before, after, afterEach } from 'node:test';
import { setImmediate as nextTurn } from 'node:timers/promises';
import { Window } from 'happy-dom';
import { createDomTestServer } from './dom-test-server.mjs';

const browser = new Window({ url: 'http://localhost:5173/display/big-left' });
const names = ['window', 'document', 'navigator', 'HTMLElement', 'HTMLInputElement', 'Element', 'Node', 'SVGElement',
  'Document', 'MutationObserver', 'ResizeObserver', 'Image', 'getComputedStyle', 'requestAnimationFrame', 'cancelAnimationFrame', 'RTCPeerConnection'];
const original = new Map(names.map((name) => [name, Object.getOwnPropertyDescriptor(globalThis, name)]));
for (const name of names.filter((name) => name !== 'RTCPeerConnection')) Object.defineProperty(globalThis, name, {
  value: typeof browser[name] === 'function' && /^[a-z]/.test(name) ? browser[name].bind(browser) : browser[name], configurable: true, writable: true,
});
Object.defineProperty(globalThis, 'RTCPeerConnection', { value: function PeerAvailabilityOnly() {}, configurable: true });
Object.defineProperty(browser, 'isSecureContext', { value: true, configurable: true });
let server, vue, createPinia, i18n, Component, useAuthStore, clientConnection, app, calls, rejectCapture, resolveCapture, auth;
const originalFetch = globalThis.fetch;
before(async () => {
  [vue, { createPinia }, server] = await Promise.all([import('vue'), import('pinia'), createDomTestServer()]);
  ({ default: Component } = await server.ssrLoadModule('/src/features/screen-share/ScreenSharePanel.vue'));
  ({ i18n } = await server.ssrLoadModule('/src/locales/index.ts'));
  ({ useAuthStore } = await server.ssrLoadModule('/src/stores/auth.ts'));
  ({ clientConnection } = await server.ssrLoadModule('/src/platform/connection.ts'));
});
afterEach(async () => {
  app?.unmount(); app = undefined;
  rejectCapture?.(Object.assign(new Error('cancelled'), { name: 'NotAllowedError' })); rejectCapture = undefined;
  await flush(); browser.document.body.replaceChildren(); await browser.happyDOM.abort(); globalThis.fetch = originalFetch;
});
after(async () => {
  await server?.close(); await browser.happyDOM.close();
  for (const [name, descriptor] of original) { if (descriptor) Object.defineProperty(globalThis, name, descriptor); else delete globalThis[name]; }
});
/** :returns: 刷新实际Vue DOM和异步失败反馈。 */
async function flush() { await vue?.nextTick(); await nextTurn(); await vue?.nextTick(); }
/** :param windowId: 当前目标。:param online: 实际在线标识。:returns: 仅HTTP/原生许可边界被隔离的正式面板。 */
async function mount(windowId = 1, online = true) {
  calls = [];
  Object.defineProperty(browser.navigator, 'mediaDevices', { value: { getDisplayMedia: () => {
    calls.push('capture'); return new Promise((resolve, reject) => { resolveCapture = resolve; rejectCapture = reject; });
  } }, configurable: true });
  globalThis.fetch = async () => { calls.push('api'); throw new Error('不应发出HTTP'); };
  const pinia = createPinia(); auth = useAuthStore(pinia); auth.user = { id: 1, username: 'qa', is_staff: true, is_superuser: true };
  const host = browser.document.createElement('div'); browser.document.body.append(host);
  app = vue.createApp(Component, { windowId, playerOnline: online }); app.use(pinia); app.use(i18n); app.mount(host); await flush();
}
/** :param label: 稳定按钮文案。:returns: 真实渲染的按钮。 */
function button(label) { return [...browser.document.querySelectorAll('button')].find((item) => item.textContent.trim() === label); }
for (const windowId of [1, 2]) test(`面板目标${windowId}点击先打开选择器，支持停止选择且不会OPEN`, async () => {
  await mount(windowId); assert.match(browser.document.body.textContent, new RegExp(`窗口 ${windowId}`));
  assert.ok(button('选择共享内容')); button('选择共享内容').click();
  assert.deepEqual(calls, ['capture'], '原点击栈应已调用原生许可'); await flush();
  assert.match(browser.document.querySelector('[role="status"]').textContent, /选择器/);
  assert.equal(button('选择共享内容').disabled, true);
  button('停止共享').click(); await flush(); assert.deepEqual(calls, ['capture']);
});
test('许可拒绝文案明确，状态不冒充已经出画', async () => {
  await mount(); button('选择共享内容').click(); await flush();
  rejectCapture(Object.assign(new Error('private'), { name: 'NotAllowedError' })); await flush();
  assert.match(browser.document.body.textContent, /取消|拒绝/);
  assert.doesNotMatch(browser.document.querySelector('[role="status"]').textContent, /已收到画面/);
  assert.deepEqual(calls, ['capture']);
});
test('播放器离线禁用采集入口并给出明确提示', async () => {
  await mount(2, false); assert.equal(button('选择共享内容').disabled, true);
  assert.match(browser.document.body.textContent, /播放器.*离线/); assert.deepEqual(calls, []);
});

for (const reason of ['pagehide', 'logout', 'host']) test(`${reason}结束面板寿命；迟到原生许可释放轨道，不向新账号/主机创建预约`, async () => {
  await mount(); button('选择共享内容').click(); await flush();
  if (reason === 'pagehide') browser.dispatchEvent(new browser.Event('pagehide'));
  else if (reason === 'logout') auth.user = null;
  else clientConnection.restoreProfile({ origin: 'https://qa-new-host.invalid', displayName: '本机测试替身' });
  await flush();
  assert.doesNotMatch(browser.document.querySelector('[role="status"]').textContent, /选择器/, '主机/页面/账号切换不能继续等待许可');
  const video = { readyState: 'live', stop() { this.readyState = 'ended'; } };
  resolveCapture({ getTracks: () => [video], getVideoTracks: () => [video] }); await flush();
  assert.equal(video.readyState, 'ended'); assert.deepEqual(calls, ['capture']);
});
