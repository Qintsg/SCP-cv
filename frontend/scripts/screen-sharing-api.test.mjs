/* 共享API客户端边界：认证、防伪、固定路由、取消与主机代次；不计作实际HTTP服务器验收。SCP-cv 2026-10-11。 */
import assert from 'node:assert/strict';
import test, { before, after, afterEach } from 'node:test';
import { Window } from 'happy-dom';
import { createDomTestServer } from './dom-test-server.mjs';

const originals = new Map(['window', 'document', 'fetch'].map((name) => [name, Object.getOwnPropertyDescriptor(globalThis, name)]));
const shareId = '12345678-1234-4234-8234-123456789abc';
const browser = new Window({ url: 'https://qa.invalid/' });
let server, api, requests, connection, sharing;
before(async () => {
  globalThis.window = browser;
  globalThis.document = browser.document;
  browser.document.cookie = 'csrftoken=qa-cookie-token';
  server = await createDomTestServer();
  api = await server.ssrLoadModule('/src/services/api.ts');
  ({ clientConnection: connection } = await server.ssrLoadModule('/src/platform/connection.ts'));
  ({ screenSharingApi: sharing } = await server.ssrLoadModule('/src/services/screenSharing.ts'));
  api.configureApiServer({ origin: 'https://qa.invalid', displayName: '隔离测试' });
});
afterEach(() => { api.registerUnauthorizedHandler(null); api.clearApiSessionState(); });
after(async () => {
  await server?.close();
  await browser.happyDOM.close();
  for (const [name, descriptor] of originals) {
    if (descriptor) Object.defineProperty(globalThis, name, descriptor); else delete globalThis[name];
  }
});
/** :param status: 预期HTTP状态。:param body: 公开结果。:returns: 仅fetch被替换的正式请求封装。 */
function respond(status = 200, body = { success: true, share: { id: shareId } }) {
  requests = [];
  globalThis.fetch = async (url, init) => {
    requests.push({ url, init });
    return new Response(JSON.stringify(body), { status, headers: { 'Content-Type': 'application/json' } });
  };
}

test('共享预约沿用cookie凭据/CSRF、实际音轨参数和no-store', async () => {
  respond();
  assert.deepEqual(await sharing.create(2, false), { id: shareId });
  assert.equal(requests.length, 1);
  const { url, init } = requests[0];
  assert.equal(url, 'https://qa.invalid/api/screen-shares/');
  assert.equal(init.method, 'POST'); assert.equal(init.cache, 'no-store'); assert.equal(init.credentials, 'include');
  assert.equal(init.headers['X-CSRFToken'], 'qa-cookie-token');
  assert.deepEqual(JSON.parse(init.body), { window_id: 2, has_audio: false });
});
test('offer/GET/心跳/停止保持固定会话路由与方法，不接受外部URL身份', async () => {
  respond();
  await sharing.offer(shareId, { type: 'offer', sdp: 'v=0\r\n' });
  await sharing.get(shareId); await sharing.heartbeat(shareId); await sharing.stop(shareId);
  assert.deepEqual(requests.map(({ url, init }) => [url.replace('https://qa.invalid', ''), init.method || 'GET']), [
    [`/api/screen-shares/${shareId}/offer/`, 'POST'], [`/api/screen-shares/${shareId}/`, 'GET'],
    [`/api/screen-shares/${shareId}/heartbeat/`, 'POST'], [`/api/screen-shares/${shareId}/`, 'DELETE'],
  ]);
  assert.ok(requests.every(({ init }) => init.cache === 'no-store'));
  assert.throws(() => sharing.stop('https://outside.invalid/'), /share_identity_invalid/);
  assert.equal(requests.length, 4);
});
test('结构化409拒绝保留稳定错误码，既有公开message不变', async () => {
  respond(409, { detail: '此目标已占用', code: 'share_window_busy' });
  await assert.rejects(sharing.create(1, false), (error) => {
    assert.ok(error instanceof api.ApiRequestError);
    assert.equal(error.message, '此目标已占用'); assert.equal(error.code, 'share_window_busy'); assert.equal(error.status, 409);
    return true;
  });
});
test('401仍触发一次原认证清理，不能混成共享争抢错误', async () => {
  respond(401, { detail: '需要登录', code: 'share_window_busy' });
  let invalidated = 0; api.registerUnauthorizedHandler(() => { invalidated++; });
  await assert.rejects(sharing.get(shareId), api.UnauthorizedError);
  assert.equal(invalidated, 1);
});
test('调用方取消信号传到实际fetch，不被默认超时controller覆盖', async () => {
  const caller = new AbortController(); let transportSignal;
  globalThis.fetch = (_url, init) => {
    transportSignal = init.signal;
    return new Promise((_resolve, reject) => {
      init.signal.addEventListener('abort', () => reject(new DOMException('aborted', 'AbortError')), { once: true });
    });
  };
  const result = assert.rejects(sharing.get(shareId, caller.signal));
  assert.ok(transportSignal); caller.abort();
  await result; assert.equal(transportSignal.aborted, true);
});
test('切主机后的旧响应在共享客户端被丢弃，不写入新主机状态', async () => {
  let finish;
  globalThis.fetch = async () => new Promise((resolve) => { finish = resolve; });
  const result = assert.rejects(sharing.get(shareId), { name: 'StaleConnectionResponseError' });
  connection.restoreProfile({ origin: 'https://new-qa.invalid', displayName: '新主机' });
  finish(new Response(JSON.stringify({ success: true, share: { id: shareId } }), { headers: { 'Content-Type': 'application/json' } }));
  await result;
});
