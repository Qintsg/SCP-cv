/** 首页布局归属回归：正式Vue/Ant/API，仅内存DOM与fetch，不产生系统输入或访问现场。 */
import assert from 'node:assert/strict';
import test, { before, after, afterEach } from 'node:test';
import { setImmediate as nextTurn } from 'node:timers/promises';
import { Window } from 'happy-dom';
import { createDomTestServer, visibleText } from './dom-test-server.mjs';

const browser = new Window({ url: 'http://localhost:5173/dashboard' });
const names = ['window', 'document', 'navigator', 'HTMLElement', 'HTMLInputElement', 'HTMLTextAreaElement',
  'Element', 'Node', 'SVGElement', 'Document', 'MutationObserver', 'ResizeObserver', 'Image', 'InputEvent',
  'getComputedStyle', 'requestAnimationFrame', 'cancelAnimationFrame'];
const globals = new Map(names.map(name => [name, Object.getOwnPropertyDescriptor(globalThis, name)]));
for (const name of names) Object.defineProperty(globalThis, name, { configurable: true, writable: true,
  value: typeof browser[name] === 'function' && /^[a-z]/.test(name) ? browser[name].bind(browser) : browser[name] });
const originalFetch = globalThis.fetch;
let server, vue, createPinia, disposePinia, i18n, Component, apiModule, client, useRuntime, useAuth, useToast;
let app, pinia, auth, runtime, requests, holds, respond;

/** :param value: 仅测试HTTP。:param status: 响应码。:returns: 正式解析器消费的Response。 */
function json(value, status = 200) { return new Response(JSON.stringify(value), { status, headers: { 'Content-Type': 'application/json' } }); }
/** :param name: 可区分的现场投影。:returns: 合法可用布局，不冒充物理确认。 */
function layout(name = '原主机布局') { return { success: true, layout: { draft_revision: 7,
  draft: { name, can_apply: true, unavailable_reason: '', mappings: [{ region: 'fullscreen', input: { kind: 'window_1', ip_address: '' } }] } } }; }
/** :returns: 最终必释放的外部HTTP等待。 */
function gate() { let resolve; const promise = new Promise(done => { resolve = done; }); const value = { promise, resolve }; holds.push(value); return value; }
/** :returns: Vue和已完成HTTP的微任务收尾，不代替原请求终端。 */
async function flush() { await vue.nextTick(); await nextTurn(); await vue.nextTick(); }
/** :param label: 公开按钮文字。:returns: 真实Ant按钮。 */
function button(label = '应用布局') { return [...browser.document.querySelectorAll('button')].find(node => visibleText(node.textContent) === visibleText(label)); }
/** :returns: 预检与实体应用HTTP计数，不包含只读GET。 */
function writes() { return requests.filter(item => item.method !== 'GET' && item.path.startsWith('/api/video-wall/')); }
/** :returns: 正常认证与布局HTTP替身，所有业务模块仍为正式实现。 */
function normal(path, options) {
  if (path === '/api/auth/csrf/') return Promise.resolve(json({ csrfToken: 'memory-only' }));
  if (path === '/api/auth/status/') return Promise.resolve(json({ authenticated: true, user: { id: 1, username: 'memory' } }));
  if (path === '/api/video-wall/layout/') return Promise.resolve(json(layout(options.method === 'PUT' ? JSON.parse(options.body).name : '原主机布局')));
  return Promise.resolve(json({ success: true, layout: layout().layout, runtime: { big_screen_mode: 'custom' }, sessions: [] }));
}

before(async () => {
  [vue, { createPinia, disposePinia }, server] = await Promise.all([import('vue'), import('pinia'), createDomTestServer()]);
  ({ default: Component } = await server.ssrLoadModule('/src/features/runtime/VideoWallMappingPanel.vue'));
  ({ i18n } = await server.ssrLoadModule('/src/locales/index.ts'));
  apiModule = await server.ssrLoadModule('/src/services/api.ts');
  ({ useRuntimeStore: useRuntime } = await server.ssrLoadModule('/src/stores/runtime.ts'));
  ({ useAuthStore: useAuth } = await server.ssrLoadModule('/src/stores/auth.ts'));
  ({ useToastStore: useToast } = await server.ssrLoadModule('/src/composables/useToast.ts'));
  ({ clientConnection: client } = await server.ssrLoadModule('/src/platform/connection.ts'));
});
afterEach(async () => {
  app?.unmount(); app = undefined;
  for (const hold of holds || []) hold.resolve(json(layout('已结束的旧请求')));
  await flush(); if (pinia) disposePinia(pinia);
  browser.document.body.replaceChildren(); await browser.happyDOM.abort(); globalThis.fetch = originalFetch;
});
after(async () => { await server.close(); await browser.happyDOM.close(); for (const [name, descriptor] of globals) {
  if (descriptor) Object.defineProperty(globalThis, name, descriptor); else delete globalThis[name];
} });

/** :returns: 已登录的正式首页布局块；网络只允许测试域，不能漏向D4。 */
async function mount() {
  requests = []; holds = []; respond = normal;
  client.restoreProfile({ origin: browser.location.origin, displayName: '本机内存', allowInsecureDevelopment: true });
  apiModule.clearApiSessionState(); apiModule.registerUnauthorizedHandler(null);
  pinia = createPinia(); auth = useAuth(pinia); auth.user = { id: 1, username: 'memory' }; auth.initialized = true;
  runtime = useRuntime(pinia);
  globalThis.fetch = (url, options = {}) => {
    const address = new URL(String(url), browser.location.href);
    assert.ok(['localhost', 'new-wall.invalid'].includes(address.hostname), '禁止访问任何真实设备');
    const request = { url: address.href, path: address.pathname, method: options.method || 'GET', body: options.body };
    requests.push(request); return respond(request.path, options);
  };
  const host = browser.document.createElement('div'); browser.document.body.append(host);
  app = vue.createApp(Component); app.use(pinia).use(i18n); app.mount(host); await flush();
}

test('真实连接先于runtime投影变化时立即撤销旧布局就绪，新主机仅独立读取', async () => {
  await mount(); assert.equal(button().disabled, false);
  const hold = gate(); respond = (path, options) => path === '/api/video-wall/layout/' ? hold.promise : normal(path, options);
  const projection = runtime.connectionGeneration;
  client.restoreProfile({ origin: 'https://new-wall.invalid', displayName: '新主机' }); await flush();
  assert.equal(runtime.connectionGeneration, projection, '覆盖真实连接先失效、投影尚未更新的窗口');
  assert.notEqual(button()?.disabled, false, '读取骨架或禁用按钮均不能应用旧主机布局');
  assert.equal([...browser.document.querySelectorAll('input')].some(node => node.value.includes('原主机布局')), false);
  assert.equal(requests.filter(item => item.url.startsWith('https://new-wall.invalid/') && item.path === '/api/video-wall/layout/').length, 1);
  assert.deepEqual(writes(), [], '恢复不能自发预检或切墙');
  hold.resolve(json(layout('新主机布局'))); await flush();
  assert.equal(button().disabled, false);
  assert.equal(browser.document.querySelector('input[aria-label="布局名称"]').value, '新主机布局');
});

/** :param change: 正式认证/连接或页面寿命边界。:returns: 不调用布局模块私有方法。 */
async function invalidate(change) {
  if (change === 'host') client.restoreProfile({ origin: 'https://new-wall.invalid', displayName: '新主机' });
  else if (change === 'projection') runtime.connectionGeneration++;
  else if (change === 'relogin') auth.user = { id: 1, username: 'new-memory' };
  else if (change === 'passwordRevision') await auth.changePassword('fake-old', 'fake-new');
  else if (change === 'logout') auth.clearLocal();
  else if (change === 'unmount') { app.unmount(); app = undefined; }
  await flush();
}

for (const change of ['host', 'projection', 'relogin', 'passwordRevision', 'logout', 'unmount']) {
  for (const status of [200, 503]) test(`预检在途${change}后旧${status}不续发或串反馈`, async () => {
    await mount(); const hold = gate();
    respond = (path, options) => path === '/api/video-wall/layout/' && options.method === 'PUT' ? hold.promise : normal(path, options);
    button().click(); await flush(); assert.equal(writes().length, 1);
    await invalidate(change); useToast(pinia).clear();
    hold.resolve(json(status === 200 ? layout('旧预检布局') : { detail: '旧预检故障' }, status)); await flush(); await flush();
    assert.equal(writes().length, 1, '失效的原意图不能继续POST或自动重放');
    assert.doesNotMatch(browser.document.body.textContent, /旧预检故障|旧响应已丢弃/);
    assert.deepEqual(useToast(pinia).items, []);
  });
}

test('新同ID登录独立加载，旧GET的finally不能解锁新GET', async () => {
  await mount(); const first = gate(), latest = gate(); let reads = 0;
  respond = (path, options) => path === '/api/video-wall/layout/' ? (++reads === 1 ? first.promise : latest.promise) : normal(path, options);
  await invalidate('projection'); assert.equal(reads, 1);
  await invalidate('relogin'); assert.equal(reads, 2);
  first.resolve(json(layout('旧读取布局'))); await flush();
  assert.notEqual(button()?.disabled, false); assert.equal(browser.document.body.textContent.includes('旧读取布局'), false);
  latest.resolve(json(layout('新登录布局'))); await flush();
  assert.equal(button().disabled, false); assert.equal(browser.document.querySelector('input[aria-label="布局名称"]').value, '新登录布局');
  assert.deepEqual(writes(), []);
});

test('不同主机的新应用不被旧POST的finally解锁或提示', async () => {
  await mount(); const first = gate(), latest = gate(); let applications = 0;
  respond = (path, options) => path.endsWith('/apply/') ? (++applications === 1 ? first.promise : latest.promise) : normal(path, options);
  button().click(); await flush(); assert.equal(applications, 1);
  await invalidate('host'); button().click(); await flush(); assert.equal(applications, 2);
  useToast(pinia).clear(); first.resolve(json({ ...layout(), runtime: { big_screen_mode: 'single' }, sessions: [] })); await flush();
  assert.equal(button().disabled, true); assert.deepEqual(useToast(pinia).items, []);
  latest.resolve(json({ ...layout(), runtime: { big_screen_mode: 'double' }, sessions: [] })); await flush();
  assert.equal(button().disabled, false); assert.equal(useToast(pinia).items.length, 1); assert.equal(runtime.runtime.big_screen_mode, 'double');
});

test('认证失效清除原声音阶段反馈，不把旧警告带入新会话', async () => {
  await mount();
  respond = (path, options) => path.endsWith('/apply/') ? Promise.resolve(json({ ...layout(), runtime: {}, sessions: [],
    audio: { status: 'uncertain', detail: '旧会话声音未确认' } })) : normal(path, options);
  button().click(); await flush(); assert.match(browser.document.body.textContent, /旧会话声音未确认/);
  await invalidate('relogin'); assert.doesNotMatch(browser.document.body.textContent, /旧会话声音未确认/);
});

test('当前预检401仍退出，旧finally不向匿名环境恢复读取', async () => {
  await mount(); let unauthorized = 0;
  apiModule.registerUnauthorizedHandler(() => { unauthorized++; auth.clearLocal(); });
  respond = (path, options) => options.method === 'PUT' ? Promise.resolve(json({ detail: '当前登录失效' }, 401)) : normal(path, options);
  const reads = requests.filter(item => item.path === '/api/video-wall/layout/' && item.method === 'GET').length;
  button().click(); await flush(); await flush();
  assert.equal(unauthorized, 1); assert.equal(auth.user, null); assert.equal(writes().length, 1);
  assert.equal(requests.filter(item => item.path === '/api/video-wall/layout/' && item.method === 'GET').length, reads);
  assert.equal(button().disabled, true); assert.deepEqual(useToast(pinia).items, []);
});

test('当前布局读取失败保留说明，可以手动重试但不会自动切墙', async () => {
  await mount(); let reject = true;
  respond = (path, options) => path === '/api/video-wall/layout/' && reject
    ? Promise.resolve(json({ detail: '当前布局服务原始故障' }, 503)) : normal(path, options);
  await invalidate('relogin'); assert.match(browser.document.body.textContent, /当前布局服务原始故障/);
  assert.equal(button().disabled, true); reject = false; button('请稍后重试').click(); await flush();
  assert.equal(button().disabled, false); assert.doesNotMatch(browser.document.body.textContent, /当前布局服务原始故障/); assert.deepEqual(writes(), []);
});

test('真实连接切换清理在途时不读取中间目标，也不接受应用', async () => {
  await mount(); const hold = gate();
  const switching = client.switchServer({ origin: 'https://new-wall.invalid', displayName: '新主机' }, { clearSession: () => hold.promise });
  await flush(); assert.equal(client.isSwitching, true); button().click(); button('请稍后重试')?.click(); await flush();
  assert.equal(requests.filter(item => item.path === '/api/video-wall/layout/').length, 1); assert.deepEqual(writes(), []);
  hold.resolve(); await switching;
  // 正式切换会重新认证；这里只在真实新认证到达时恢复，不用旧finally触发。
  await invalidate('relogin'); assert.ok(requests.some(item => item.url.startsWith('https://new-wall.invalid/')));
  assert.equal(button().disabled, false); assert.deepEqual(writes(), []);
});
