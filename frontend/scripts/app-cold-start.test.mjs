/** 封装端空配置冷启动：真实app模式/API/Auth/Runtime，仅替换网络边界，不连接设备。 */
import assert from 'node:assert/strict';
import test, { afterEach } from 'node:test';
import { createPinia, setActivePinia, disposePinia } from 'pinia';
import { Window } from 'happy-dom';
import { createDomTestServer } from './dom-test-server.mjs';

const names = ['window', 'document', 'navigator', 'HTMLElement', 'Element', 'Node', 'SVGElement', 'EventSource', 'XMLHttpRequest'];
const globals = new Map(names.map(name => [name, Object.getOwnPropertyDescriptor(globalThis, name)]));
const originalFetch = globalThis.fetch;
let browser, server, pinia, api, auth, runtime, client, requests, xhrCount, eventCount;

/** :param mode: 构建模式。:param options: 原生页面/编译默认/开发模式。:returns: 未替换内部依赖的正式模块。 */
async function setup(mode = 'app', options = {}) {
  browser = new Window({ url: options.url || 'app://scp-cv/#/connect' });
  for (const name of names.slice(0, 7)) Object.defineProperty(globalThis, name, { configurable: true, writable: true, value: browser[name] });
  requests = []; xhrCount = 0; eventCount = 0;
  globalThis.fetch = async (url, init = {}) => {
    requests.push({ url: String(url), method: init.method || 'GET', credentials: init.credentials });
    const path = new URL(String(url), 'https://memory.invalid').pathname;
    const data = path === '/api/auth/csrf/' ? { csrfToken: 'memory-token' }
      : path === '/api/auth/status/' ? { authenticated: false, user: null }
        : path === '/api/auth/login/' ? { user: { id: 1, username: 'memory-user' } } : { success: true };
    return new Response(JSON.stringify(data), { headers: { 'Content-Type': 'application/json' } });
  };
  globalThis.XMLHttpRequest = class { constructor() { xhrCount++; throw new Error('空配置不得创建XHR'); } };
  globalThis.EventSource = class {
    constructor(url, options) { eventCount++; this.url = url; this.options = options; }
    close() {}
    addEventListener() {}
  };
  server = await createDomTestServer();
  Object.assign(server.config.env, { DEV: options.dev ?? false, MODE: mode, VITE_BACKEND_TARGET: options.target || '' });
  const platform = await server.ssrLoadModule('/src/platform/index.ts');
  assert.equal(platform.getClientBuildTarget(), mode);
  api = await server.ssrLoadModule('/src/services/api.ts');
  ({ clientConnection: client } = await server.ssrLoadModule('/src/platform/connection.ts'));
  const { useAuthStore } = await server.ssrLoadModule('/src/stores/auth.ts');
  const { useRuntimeStore } = await server.ssrLoadModule('/src/stores/runtime.ts');
  pinia = createPinia(); setActivePinia(pinia); auth = useAuthStore(pinia); runtime = useRuntimeStore(pinia);
}

afterEach(async () => {
  runtime?.disconnectEvents();
  if (pinia) disposePinia(pinia);
  await server?.close(); await browser?.happyDOM.close();
  globalThis.fetch = originalFetch;
  for (const [name, descriptor] of globals) {
    if (descriptor) Object.defineProperty(globalThis, name, descriptor); else delete globalThis[name];
  }
});

for (const url of ['app://scp-cv/#/connect', 'https://localhost/#/connect']) {
  test(`空配置${url}初始化保持未知身份、零网络`, async () => {
    await setup('app', { url }); await Promise.all([auth.ensureInitialized(), auth.ensureInitialized()]);
    assert.deepEqual(requests, []); assert.equal(auth.initialized, false); assert.equal(auth.user, null);
  });
}
for (const target of ['', 'https://build-default.invalid']) {
  test(`编译默认${target || '为空'}不能替代用户选主机`, async () => {
    await setup('app', { target });
    for (const path of ['/api/runtime/', 'https://absolute.invalid/api/runtime/']) {
      assert.throws(() => api.buildBackendUrl(path), { name: 'ServerProfileRequiredError' });
      await assert.rejects(api.requestJson(path), { name: 'ServerProfileRequiredError' });
    }
    assert.deepEqual(requests, []); assert.equal(api.getConnectionPorts().backendPort, '');
  });
}
for (const action of ['probe', 'login', 'logout', 'upload', 'events']) {
  test(`空配置${action}不派发、不中途初始化身份`, async () => {
    await setup();
    if (action === 'events') { runtime.connectEvents(); assert.equal(runtime.sseStatus, 'closed'); }
    else {
      const invoke = action === 'probe' ? () => auth.probeCurrentServer()
        : action === 'login' ? () => auth.login('memory', 'not-real')
          : action === 'logout' ? () => auth.logout() : () => api.api.uploadSource(new FormData());
      await assert.rejects(invoke(), { name: 'ServerProfileRequiredError' });
    }
    assert.deepEqual(requests, []); assert.equal(xhrCount, 0); assert.equal(eventCount, 0);
    assert.equal(auth.initialized, false); assert.equal(auth.user, null);
  });
}
test('明确选择HTTPS主机后正常探测、登录及带凭据SSE', async () => {
  await setup('app', { target: 'https://ignored.invalid' }); await auth.ensureInitialized();
  assert.deepEqual(requests, []);
  client.restoreProfile({ origin: 'https://selected.invalid:24443', displayName: '内存主机' });
  await auth.ensureInitialized(); assert.equal(auth.initialized, true); assert.equal(auth.user, null);
  await auth.login('memory', 'not-real'); assert.equal(auth.user.id, 1);
  runtime.connectEvents(); assert.equal(eventCount, 1);
  assert.equal(runtime._eventSource.url, 'https://selected.invalid:24443/api/events/');
  assert.equal(runtime._eventSource.options.withCredentials, true);
  assert.equal(requests.length, 4);
  assert.ok(requests.every(item => item.url.startsWith('https://selected.invalid:24443/api/') && item.credentials === 'include'));
});
test('明确选中的回环HTTP开发主机仍可使用', async () => {
  await setup(); client.restoreProfile({ origin: 'http://127.0.0.1:18000', displayName: '受控开发', allowInsecureDevelopment: true });
  await auth.ensureInitialized(); assert.equal(auth.initialized, true);
  assert.ok(requests.every(item => item.url.startsWith('http://127.0.0.1:18000/api/')));
});
test('开发app模式也不能跳过明确选主机', async () => {
  await setup('app', { dev: true, target: 'http://127.0.0.1:18000' });
  await auth.ensureInitialized(); assert.deepEqual(requests, []); assert.equal(auth.initialized, false);
});
for (const dev of [false, true]) test(`网页${dev ? '开发' : '生产'}保持同源初始化`, async () => {
  await setup('web', { dev, url: 'https://web.invalid:5212/', target: 'https://upstream.invalid' });
  await auth.ensureInitialized(); assert.equal(auth.initialized, true);
  assert.deepEqual(requests.map(item => item.url), ['/api/auth/csrf/', '/api/auth/status/']);
});
