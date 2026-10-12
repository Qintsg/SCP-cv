/** 双网页入口端口/上游合同；实际socket与认证联调另验。SCP-cv 2026-10-12。 */
import assert from 'node:assert/strict';
import test from 'node:test';
import { accessPort, backendTargets } from '../config/dualProtocol.ts';

test('网页端口严格解析，不能静默换端口或接受部分数字', () => {
  assert.equal(accessPort(undefined, 5173), 5173);
  assert.equal(accessPort(' 5210 ', 5173), 5210);
  for (const value of ['0', '65536', '5173abc', '-1', '1.5', 'NaN'])
    assert.throws(() => accessPort(value, 5173));
});
test('每入口只使用协议匹配的上游，明确地址无协议默认HTTP', () => {
  assert.deepEqual(backendTargets({}), { http: 'http://127.0.0.1:18444', https: 'https://127.0.0.1:18443' });
  assert.equal(backendTargets({ VITE_BACKEND_TARGET: '127.0.0.1:18000' }).http, 'http://127.0.0.1:18000');
  assert.equal(backendTargets({ VITE_BACKEND_TARGET: 'https://host.test:443' }).https, 'https://host.test');
  for (const env of [{ VITE_BACKEND_HTTP_TARGET: 'https://host.test' }, { VITE_BACKEND_HTTPS_TARGET: 'http://host.test' },
    { VITE_BACKEND_TARGET: 'ftp://host.test' }, { VITE_BACKEND_HTTP_TARGET: 'http://u:p@host.test' },
    { VITE_BACKEND_TARGET: 'host.test/api' }]) assert.throws(() => backendTargets(env));
});
