/** 首页真实Ant音量滑杆归属：延迟HTTP，不调用组件内部方法，不操作声卡或设备。 */
import assert from 'node:assert/strict';
import test, { before, after, afterEach } from 'node:test';
import { setImmediate as nextTurn } from 'node:timers/promises';
import { Window } from 'happy-dom';
import { createDomTestServer, prepareSliderGeometry, browserKeyCode } from './dom-test-server.mjs';

const browser = new Window({ url: 'http://localhost:5173/dashboard' });
const names = ['window', 'document', 'navigator', 'history', 'location', 'HTMLElement', 'HTMLInputElement', 'Element', 'Node', 'SVGElement', 'Document', 'MutationObserver', 'ResizeObserver', 'Image', 'getComputedStyle', 'requestAnimationFrame', 'cancelAnimationFrame'];
const globals = new Map(names.map(name => [name, Object.getOwnPropertyDescriptor(globalThis, name)]));
for (const name of names) Object.defineProperty(globalThis, name, { configurable: true, writable: true, value: typeof browser[name] === 'function' && /^[a-z]/.test(name) ? browser[name].bind(browser) : browser[name] });
const originalFetch = globalThis.fetch;
let server, vue, createPinia, disposePinia, createRouter, createMemoryHistory, RouterView, i18n, Dashboard, useRuntime, useAuth, useToast, client;
let app, pinia, router, auth, runtime, requests, pending, respond;
/** :param value: 假HTTP数据。:param status: HTTP状态。:returns: 正式解析器使用的Response。 */
function json(value, status = 200) { return new Response(JSON.stringify(value), { status, headers: { 'Content-Type': 'application/json' } }); }
/** :returns: 本轮结束必释放的外部响应。 */
function gate() { let resolve; const promise = new Promise(done => { resolve = done; }); const result = { promise, resolve }; pending.push(result); return result; }
/** :param level: 返回音量。:returns: 无硬件确认的测试数据。 */
function volume(level) { return { volume: { level, muted: false, system_synced: false, backend: 'simulation' } }; }
/** :returns: 等待真实Vue及已完成HTTP的微任务，不代替在途完成证明。 */
async function flush() { await vue.nextTick(); await nextTurn(); await vue.nextTick(); }
before(async () => {
  [vue, { createPinia, disposePinia }, { createRouter, createMemoryHistory, RouterView }, server] = await Promise.all([import('vue'), import('pinia'), import('vue-router'), createDomTestServer()]);
  ({ i18n } = await server.ssrLoadModule('/src/locales/index.ts'));
  ({ default: Dashboard } = await server.ssrLoadModule('/src/features/dashboard/DashboardView.vue'));
  ({ useRuntimeStore: useRuntime } = await server.ssrLoadModule('/src/stores/runtime.ts'));
  ({ useAuthStore: useAuth } = await server.ssrLoadModule('/src/stores/auth.ts'));
  ({ useToastStore: useToast } = await server.ssrLoadModule('/src/composables/useToast.ts'));
  ({ clientConnection: client } = await server.ssrLoadModule('/src/platform/connection.ts'));
});
afterEach(async () => {
  app?.unmount(); app = undefined;
  for (const hold of pending || []) hold.resolve(json(volume(50)));
  await flush();
  if (pinia) disposePinia(pinia);
  browser.document.body.replaceChildren(); await browser.happyDOM.abort(); globalThis.fetch = originalFetch;
});
after(async () => { await server.close(); await browser.happyDOM.close(); for (const [name, descriptor] of globals) { if (descriptor) Object.defineProperty(globalThis, name, descriptor); else delete globalThis[name]; } });

/** :returns: 正式Dashboard及完整Store/API；所有网络在fetch边界截断。 */
async function mountDashboard() {
  requests = []; pending = [];
  client.restoreProfile({ origin: browser.location.origin, displayName: '本机内存', allowInsecureDevelopment: true });
  pinia = createPinia(); auth = useAuth(pinia); auth.user = { id: 1, username: 'memory', is_staff: true, is_superuser: true }; auth.initialized = true;
  runtime = useRuntime(pinia); runtime.systemVolume.level = 50;
  respond = async (path, options) => json(path === '/api/video-wall/layout/' ? { layout: { draft: null } }
    : path === '/api/auth/csrf/' ? { csrfToken: 'memory-token' }
      : path === '/api/auth/status/' ? { authenticated: true, user: { id: 1, username: 'memory' } }
        : path === '/api/volume/' ? volume(JSON.parse(options.body || '{"level":50}').level) : { success: true });
  globalThis.fetch = (url, options = {}) => {
    const path = new URL(String(url), browser.location.href).pathname;
    requests.push({ url: String(url), path, method: options.method || 'GET', body: options.body });
    return respond(path, options);
  };
  router = createRouter({ history: createMemoryHistory(), routes: [{ path: '/dashboard', component: Dashboard }, { path: '/other', component: { render: () => vue.h('p', '其它页') } }, { path: '/display/:id', component: { render: () => vue.h('p', '测试输出页') } }] });
  await router.push('/dashboard');
  const root = browser.document.createElement('div'); browser.document.body.append(root);
  app = vue.createApp({ render: () => vue.h(RouterView) }); app.use(pinia).use(i18n).use(router); app.mount(root);
  await flush(); useToast(pinia).clear();
}
/** :returns: 真实滑杆原生左方向键及收尾事件。 */
async function decrease() {
  const handle = browser.document.querySelector('[role="slider"][aria-label="系统音量"]');
  assert.ok(handle, '正式首页必须存在系统音量'); prepareSliderGeometry(handle); handle.focus();
  for (const type of ['keydown', 'keyup']) {
    handle.dispatchEvent(new browser.KeyboardEvent(type, { key: 'ArrowLeft', keyCode: browserKeyCode('ArrowLeft'), which: browserKeyCode('ArrowLeft'), bubbles: true, cancelable: true })); await flush();
  }
}
/** :returns: 仅实际音量写请求，初始化读取不算写入。 */
function writes() { return requests.filter(item => item.path === '/api/volume/' && item.method === 'PATCH'); }
/** :param status: 首次响应状态。:returns: 已挂起第一笔并缓冲第二笔的真实UI。 */
async function startBuffered(status = 200) {
  await mountDashboard(); const hold = gate(), fallback = respond;
  respond = (path, options) => path === '/api/volume/' && options.method === 'PATCH' && writes().length === 1 ? hold.promise : fallback(path, options);
  await decrease(); await decrease(); assert.equal(writes().length, 1);
  return async () => { hold.resolve(json(status === 200 ? volume(49) : { detail: '原音量写入失败' }, status)); await flush(); await flush(); };
}
for (const change of ['host', 'projection', 'account', 'relogin', 'probe', 'passwordRevision', 'unmount', 'route']) {
  for (const status of [200, 503]) test(`首页音量${change}后旧${status}不续发缓冲或提示`, async () => {
    const finish = await startBuffered(status);
    if (change === 'host') client.restoreProfile({ origin: 'https://new-memory.invalid', displayName: '新主机' });
    else if (change === 'projection') runtime.connectionGeneration++;
    else if (change === 'account' || change === 'relogin') auth.user = { id: change === 'account' ? 2 : 1, username: 'new-session' };
    else if (change === 'probe') await auth.probeCurrentServer();
    else if (change === 'passwordRevision') await auth.changePassword('not-real-old', 'not-real-new');
    else if (change === 'route') await router.push('/other');
    else { app.unmount(); app = undefined; }
    useToast(pinia).clear(); await finish();
    assert.equal(writes().length, 1, '未发送的旧音量不得投向新主机/会话'); assert.deepEqual(useToast(pinia).items, []);
  });
}
test('首页当前连续输入保留最终值，普通回写不丢缓冲', async () => {
  const finish = await startBuffered(); runtime.systemVolume.level = 49; await finish();
  assert.deepEqual(writes().map(item => JSON.parse(item.body).level), [49, 48]);
});
test('首页当前失败保留原说明，不把合法最终输入静默丢弃', async () => {
  const finish = await startBuffered(503); await finish();
  assert.deepEqual(writes().map(item => JSON.parse(item.body).level), [49, 48]);
  assert.match(useToast(pinia).items.at(-1)?.description || '', /原音量写入失败/);
});
test('真实切换在途不接新输入，结束后新输入可以发送', async () => {
  await mountDashboard(); const hold = gate();
  const switching = client.switchServer({ origin: 'https://next-memory.invalid', displayName: '新主机' }, { clearSession: () => hold.promise });
  await flush(); await decrease(); assert.equal(writes().length, 0);
  hold.resolve(); await switching; runtime.connectionGeneration = client.captureGeneration(); await decrease();
  assert.equal(writes().length, 1); assert.ok(writes()[0].url.startsWith('https://next-memory.invalid/'));
});
test('切到不同后端后新输入可立即发送，旧finally不解锁或覆盖新缓冲', async () => {
  const finish = await startBuffered(); client.restoreProfile({ origin: 'https://new-memory.invalid', displayName: '新主机' }); await decrease();
  const before = writes().length; await finish(); assert.equal(before, 2); assert.equal(writes().length, 2);
});
