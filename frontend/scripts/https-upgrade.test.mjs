/** HTTP共享升级纯合同；不伪造采集或浏览器证书信任。SCP-cv 2026-10-12。 */
import assert from 'node:assert/strict';
import test from 'node:test';
import { buildHttpsPage, discoverHttpsPage } from '../src/features/screen-share/httpsUpgrade.ts';

test('HTTPS跳转使用配置端口与原主机，保留目标/合法位置而不传秘密', () => {
  const next = buildHttpsPage('http://192.168.5.194:5173/display/big-right?tab=media&token=secret&password=x#overview', { version: 1, httpsPort: 5174 });
  assert.equal(next, 'https://192.168.5.194:5174/display/big-right?tab=media#overview');
  assert.equal(buildHttpsPage('http://[::1]:5210/display/big-left', { version: 1, httpsPort: 443 }), 'https://[::1]/display/big-left');
  assert.equal(buildHttpsPage('http://host.test/?redirect=%2Fdisplay%2Fbig-left%3Fcap%3Dx#cap=x', { version: 1, httpsPort: 5443 }), 'https://host.test:5443/');
});
test('无入口/非法发现结果拒绝，不猜端口或使用服务器提供的外部主机', () => {
  for (const info of [{ version: 1, httpsPort: null }, { version: 1, httpsPort: 0 }, { version: 1, httpsPort: '5174' },
    { version: 2, httpsPort: 5174 }, { version: 1, httpsPort: 5174, host: 'evil.test' }, { version: 1, httpsPort: 65536 }])
    assert.throws(() => buildHttpsPage('http://host.test:5173/display/big-left', info));
  for (const href of ['https://host.test', 'ftp://host.test', 'http://u:p@host.test'])
    assert.throws(() => buildHttpsPage(href, { version: 1, httpsPort: 5174 }));
});
test('嵌套路由与片段中的各类认证秘密均不复制到HTTPS入口', () => {
  for (const key of ['token', 'password', 'passwd', 'secret', 'session', 'csrf', 'authorization', 'cap', 'credential']) {
    assert.equal(buildHttpsPage(`http://host.test/display/big-right?redirect=${encodeURIComponent(`/path?${key}=qa`)}#${key}=qa`,
      { version: 1, httpsPort: 5174 }), 'https://host.test:5174/display/big-right');
  }
});
test('发现仅同源GET且不跟随重定向、不附会话，明确请求失败', async () => {
  let seen;
  const next = await discoverHttpsPage('http://host.test:5173/display/big-left', new AbortController().signal, async (url, init) => {
    seen = { url, init }; return { ok: true, json: async () => ({ version: 1, httpsPort: 5443 }) };
  });
  assert.equal(next, 'https://host.test:5443/display/big-left');
  assert.equal(seen.url, 'http://host.test:5173/.well-known/scp-cv-access');
  assert.equal(seen.init.redirect, 'error'); assert.equal(seen.init.credentials, 'omit'); assert.equal(seen.init.cache, 'no-store');
  await assert.rejects(discoverHttpsPage('http://host.test', new AbortController().signal, async () => ({ ok: false })), /share_https_unavailable/);
});
