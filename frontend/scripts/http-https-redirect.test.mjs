/** HTTP默认升级：真实HTTP监听核跳转与零业务调用；HTTPS分支另核，真实TLS由部署验证。 */
import assert from 'node:assert/strict';
import test from 'node:test';
import { createServer, request } from 'node:http';
import { once } from 'node:events';
import * as access from '../config/dualProtocol.ts';

/** :returns: 临时端口探测完成，不保留其它服务或全局配置。 */
async function unusedPort() {
  const probe = createServer(); probe.listen(0, '127.0.0.1'); await once(probe, 'listening');
  const port = probe.address().port; await new Promise(resolve => probe.close(resolve)); return port;
}

/** :param t: 测试清理范围。:param tls: 是否模拟已校验TLS配置。:returns: 真实HTTP入口及分支计数。 */
async function fixture(t, tls = true, preview = true) {
  const middlewares = []; let served = 0;
  const dispatch = (req, res) => {
    let index = 0;
    const next = () => {
      if (index < middlewares.length) return middlewares[index++](req, res, next);
      served++; res.end('content');
    };
    next();
  };
  dispatch.use = handler => middlewares.push(handler);
  const http = createServer(dispatch);
  const httpsPort = await unusedPort();
  const plugin = access.dualProtocolPlugin(tls ? {} : undefined, httpsPort);
  const config = { server: { port: 0, host: '127.0.0.1' }, preview: { port: 0, host: '127.0.0.1' }, logger: { info() {} } };
  await plugin[preview ? 'configurePreviewServer' : 'configureServer']({ config, httpServer: http, middlewares: dispatch });
  http.listen(0, '127.0.0.1'); await once(http, 'listening');
  t.after(async () => { await new Promise(resolve => http.close(resolve)); await plugin.closeBundle(); });
  return { port: http.address().port, httpsPort, middlewares, served: () => served };
}

/** :param fixture: 仅本机的HTTP监听。:param options: 请求方法与路径。:returns: 不自动跟随的响应。 */
function send(fixture, { path = '/', method = 'GET', host = '192.168.5.194:80' } = {}) {
  return new Promise((resolve, reject) => {
    const req = request({ host: '127.0.0.1', port: fixture.port, path, method, headers: { Host: host } }, res => {
      let body = ''; res.setEncoding('utf8'); res.on('data', chunk => { body += chunk; });
      res.on('end', () => resolve({ status: res.statusCode, headers: res.headers, body }));
    });
    req.setTimeout(3000, () => req.destroy(new Error('入口请求超时'))); req.on('error', reject);
    req.end(method === 'POST' ? 'local-fixture-body' : undefined);
  });
}

for (const method of ['GET', 'HEAD', 'POST']) test(`HTTP ${method}在业务前307跳转，保留路径/查询且不永久缓存`, async t => {
  const service = await fixture(t);
  const response = await send(service, { method, path: '/display/big-left?tab=source&name=%E6%96%87%E7%A8%BF' });
  assert.equal(response.status, 307);
  assert.equal(response.headers.location, `https://192.168.5.194:${service.httpsPort}/display/big-left?tab=source&name=%E6%96%87%E7%A8%BF`);
  assert.equal(response.headers['cache-control'], 'no-store');
  assert.equal(response.body, ''); assert.equal(service.served(), 0);
});

test('开发和preview使用同一默认跳转，发现接口也进入HTTPS', async t => {
  const service = await fixture(t, true, false);
  const response = await send(service, { path: access.ACCESS_PATH });
  assert.equal(response.status, 307);
  assert.equal(response.headers.location, `https://192.168.5.194:${service.httpsPort}${access.ACCESS_PATH}`);
  assert.equal(service.served(), 0);
});

test('未配置TLS仍提供HTTP开发页和空安全端口发现', async t => {
  const service = await fixture(t, false);
  assert.equal((await send(service)).body, 'content');
  const discovery = await send(service, { path: access.ACCESS_PATH });
  assert.equal(discovery.status, 200);
  assert.deepEqual(JSON.parse(discovery.body), { version: 1, httpsPort: null });
});

test('HTTPS分支不循环跳转，仍提供入口发现', async t => {
  const service = await fixture(t);
  let next = false; const headers = {};
  const response = { setHeader(name, value) { headers[name] = value; }, end(value) { this.body = value; } };
  service.middlewares[0]({ socket: { encrypted: true }, headers: { host: '192.168.5.194' }, method: 'GET', url: '/' }, response, () => { next = true; });
  assert.equal(next, true); assert.equal(headers.Location, undefined);
  service.middlewares[0]({ socket: { encrypted: true }, headers: { host: '192.168.5.194' }, method: 'GET', url: access.ACCESS_PATH }, response, () => assert.fail('发现不应进业务'));
  assert.deepEqual(JSON.parse(response.body), { version: 1, httpsPort: service.httpsPort });
});

test('标准443省略端口，IPv6和自定义安全端口正确，双斜线仍属同主机路径', () => {
  assert.equal(access.httpsRedirectTarget('192.168.5.194:80', '/login?redirect=%2Fsources', 443), 'https://192.168.5.194/login?redirect=%2Fsources');
  assert.equal(access.httpsRedirectTarget('[::1]:5173', '/sources', 5174), 'https://[::1]:5174/sources');
  assert.equal(access.httpsRedirectTarget('localhost:5173', '//example.invalid/path', 5174), 'https://localhost:5174//example.invalid/path');
});

test('畸形Host或绝对请求目标拒绝，不构造外部跳转', async t => {
  for (const host of [undefined, '', 'user:secret@example.invalid', 'example.invalid/path', 'example.invalid#x', 'example.invalid:99999'])
    assert.equal(access.httpsRedirectTarget(host, '/', 443), null);
  for (const path of ['http://example.invalid/', 'path', '/bad\r\nLocation:x', '/has space'])
    assert.equal(access.httpsRedirectTarget('localhost', path, 443), null);
  const service = await fixture(t);
  const response = await send(service, { host: 'user:secret@example.invalid' });
  assert.equal(response.status, 400); assert.equal(response.headers.location, undefined); assert.equal(service.served(), 0);
});
