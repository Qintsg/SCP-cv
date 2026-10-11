/** 正式网页无显式后端配置时走同源代理；保留显式主机与封装端配置。SCP-cv 2026-10-11。 */
import assert from 'node:assert/strict';
import test, { afterEach } from 'node:test';
import { Window } from 'happy-dom';
import { createDomTestServer } from './dom-test-server.mjs';

const originalWindow = Object.getOwnPropertyDescriptor(globalThis, 'window');
const originalDocument = Object.getOwnPropertyDescriptor(globalThis, 'document');
const originalFetch = globalThis.fetch;
let browser, server;

afterEach(async () => {
  await server?.close();
  await browser?.happyDOM.close();
  globalThis.fetch = originalFetch;
  for (const [name, descriptor] of [['window', originalWindow], ['document', originalDocument]]) {
    if (descriptor) Object.defineProperty(globalThis, name, descriptor);
    else delete globalThis[name];
  }
});

/** :param mode: 真实构建模式。:param target: 可选显式构建地址。:returns: 未替换内部依赖的正式API模块。 */
async function loadApi(mode = 'web', target = '') {
  browser = new Window({ url: 'https://qa.example.test:5212/display/big-left' });
  globalThis.window = browser;
  globalThis.document = browser.document;
  server = await createDomTestServer();
  server.config.env.DEV = false;
  server.config.env.MODE = mode;
  server.config.env.VITE_BACKEND_TARGET = target;
  return server.ssrLoadModule('/src/services/api.ts');
}

test('正式网页无显式后端地址时登录和REST使用同源路径而非默认8000端口', async () => {
  const module = await loadApi();
  assert.equal(module.buildBackendUrl('/api/auth/login/'), '/api/auth/login/');
  assert.equal(module.buildBackendUrl('/api/runtime/'), '/api/runtime/');
  assert.deepEqual(module.getConnectionPorts(), { backendPort: '5212', frontendPort: '5212' });
  let requested;
  globalThis.fetch = async (url, init) => {
    requested = { url, credentials: init.credentials };
    return new Response(JSON.stringify({ success: true, runtime: {} }), { headers: { 'Content-Type': 'application/json' } });
  };
  await module.api.getRuntime();
  assert.deepEqual(requested, { url: '/api/runtime/', credentials: 'include' });
});

test('网页显式构建后端地址仍优先，HTTPS默认端口摘要正确', async () => {
  const module = await loadApi('web', 'https://configured.example.test');
  assert.equal(module.buildBackendUrl('/api/runtime/'), 'https://configured.example.test/api/runtime/');
  assert.deepEqual(module.getConnectionPorts(), { backendPort: '443', frontendPort: '5212' });
});

test('已选择的播放主机仍覆盖网页构建地址，不被同源默认回退覆盖', async () => {
  const module = await loadApi('web', 'https://configured.example.test:18443');
  module.configureApiServer({ origin: 'https://selected.example.test:24443', displayName: '测试主机' });
  assert.equal(module.buildBackendUrl('/api/runtime/'), 'https://selected.example.test:24443/api/runtime/');
  assert.deepEqual(module.getConnectionPorts(), { backendPort: '24443', frontendPort: '5212' });
});

test('未配置的封装端保留显式后端端口回退，不能误向本地资源包发REST', async () => {
  const module = await loadApi('app');
  assert.equal(module.buildBackendUrl('/api/runtime/'), 'https://qa.example.test:8000/api/runtime/');
  assert.deepEqual(module.getConnectionPorts(), { backendPort: '8000', frontendPort: '5212' });
});
