/* 显式TLS配置的失败门禁；公开测试证书没有私钥，正常可信连接另由真实浏览器验。SCP-cv 2026-10-11。 */
import assert from 'node:assert/strict';
import test, { after, before } from 'node:test';
import { generateKeyPairSync } from 'node:crypto';
import { mkdtempSync, writeFileSync, rmSync, mkdirSync, symlinkSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { loadFrontendHttps } from '../config/https.ts';
import { publicCertificate } from './https-test-certificate.mjs';

let directory;
let frontendDirectory;
let fixtureDirectory;
const now = Date.parse('2026-10-12T00:00:00Z');
const env = { SCP_CV_HTTPS_CERT_FILE: '../cert.pem', SCP_CV_HTTPS_KEY_FILE: '../key.pem', SCP_CV_HTTPS_HOSTS: 'scp-cv-test.invalid,127.0.0.1' };
before(() => {
  fixtureDirectory = mkdtempSync(join(tmpdir(), 'scp-cv-https-test-'));
  directory = join(fixtureDirectory, 'frontend');
  frontendDirectory = directory;
  mkdirSync(frontendDirectory);
  mkdirSync(join(frontendDirectory, 'public'));
  writeFileSync(join(fixtureDirectory, 'cert.pem'), publicCertificate);
  writeFileSync(join(fixtureDirectory, 'bad.pem'), '不是PEM');
  const { privateKey } = generateKeyPairSync('rsa', { modulusLength: 2048 });
  writeFileSync(join(fixtureDirectory, 'key.pem'), privateKey.export({ type: 'pkcs8', format: 'pem' }));
  writeFileSync(join(fixtureDirectory, 'large.pem'), 'x'.repeat(256 * 1024 + 1));
  mkdirSync(join(fixtureDirectory, 'not-file.pem'));
  writeFileSync(join(frontendDirectory, 'public/key.pem'), '仅用于路径拒绝，不是私钥');
  writeFileSync(join(fixtureDirectory, 'unsafe.txt'), '仅用于扩展名拒绝，不是私钥');
  symlinkSync(fixtureDirectory, join(frontendDirectory, 'link-to-private'), 'junction');
  symlinkSync(frontendDirectory, join(fixtureDirectory, 'link-to-frontend'), 'junction');
});
after(() => { if (fixtureDirectory) rmSync(fixtureDirectory, { recursive: true, force: true }); });

test('未配置保持HTTP，不能自动生成证书或读取默认密钥', () => {
  assert.equal(loadFrontendHttps({}, directory, now), undefined);
  assert.equal(loadFrontendHttps({ SCP_CV_HTTPS_CERT_FILE: ' ', SCP_CV_HTTPS_KEY_FILE: '' }, directory, now), undefined);
});
for (const keys of [['SCP_CV_HTTPS_CERT_FILE'], ['SCP_CV_HTTPS_KEY_FILE'], ['SCP_CV_HTTPS_HOSTS'],
  ['SCP_CV_HTTPS_CERT_FILE', 'SCP_CV_HTTPS_KEY_FILE'], ['SCP_CV_HTTPS_CERT_FILE', 'SCP_CV_HTTPS_HOSTS'], ['SCP_CV_HTTPS_KEY_FILE', 'SCP_CV_HTTPS_HOSTS']]) {
  test(`部分TLS配置拒绝启动：${keys.join('+')}`, () => {
    assert.throws(() => loadFrontendHttps(Object.fromEntries(keys.map(k => [k, env[k]])), directory, now), /必须同时提供/);
  });
}
for (const hosts of ['https://localhost', 'localhost:5173', 'example.invalid/path', '*.invalid', '0.0.0.0', '::', 'bad_name.invalid', 'localhost,,127.0.0.1']) {
  test(`访问主机列表拒绝URL/通配/非法名称：${hosts}`, () => {
    assert.throws(() => loadFrontendHttps({ ...env, SCP_CV_HTTPS_HOSTS: hosts }, directory, now), /访问主机/);
  });
}
for (const filename of ['missing.pem', 'not-file.pem', 'large.pem']) {
  test(`TLS文件读取有界且错误不回显文件内容：${filename}`, () => {
    assert.throws(() => loadFrontendHttps({ ...env, SCP_CV_HTTPS_CERT_FILE: `../${filename}` }, directory, now), /无法读取证书/);
  });
}
test('畸形证书拒绝，不退回HTTP', () => {
  assert.throws(() => loadFrontendHttps({ ...env, SCP_CV_HTTPS_CERT_FILE: '../bad.pem' }, directory, now), /证书无效/);
});
test('畸形私钥拒绝且错误不包含密钥', () => {
  assert.throws(() => loadFrontendHttps({ ...env, SCP_CV_HTTPS_KEY_FILE: '../bad.pem' }, directory, now), /私钥无效/);
});
test('证书与有效但不匹配私钥拒绝', () => {
  assert.throws(() => loadFrontendHttps(env, directory, now), /私钥不匹配/);
});
test('未生效及过期证书拒绝', () => {
  assert.throws(() => loadFrontendHttps(env, directory, Date.parse('2020-01-01')), /有效期/);
  assert.throws(() => loadFrontendHttps(env, directory, Date.parse('2200-01-01')), /有效期/);
});
for (const hosts of ['localhost', '127.0.0.2']) {
  test(`DNS/IP必须与SAN匹配：${hosts}`, () => {
    assert.throws(() => loadFrontendHttps({ ...env, SCP_CV_HTTPS_HOSTS: hosts }, directory, now), /SAN/);
  });
}
test('相对路径以frontend目录解析，显式DNS/IP可通过SAN检查', () => {
  assert.throws(() => loadFrontendHttps({ ...env, SCP_CV_HTTPS_HOSTS: ' scp-cv-test.invalid , 127.0.0.1 ' }, frontendDirectory, now), /私钥不匹配/);
});
test('拒绝frontend/public中的TLS私钥，不能依赖该目录不适用的fs.deny', () => {
  assert.throws(() => loadFrontendHttps({ ...env, SCP_CV_HTTPS_KEY_FILE: 'public/key.pem' }, frontendDirectory, now), /frontend目录外.*\.pem/);
});
test('拒绝任意扩展名的TLS私钥，避免失去默认敏感文件阻断', () => {
  assert.throws(() => loadFrontendHttps({ ...env, SCP_CV_HTTPS_KEY_FILE: '../unsafe.txt' }, frontendDirectory, now), /frontend目录外.*\.pem/);
});
test('TLS公开证书也不得放在frontend中', () => {
  assert.throws(() => loadFrontendHttps({ ...env, SCP_CV_HTTPS_CERT_FILE: 'public/key.pem' }, directory, now), /frontend目录外.*\.pem/);
});
test('frontend中的链接即使指向外部私钥，也不能成为TLS配置入口', () => {
  assert.throws(() => loadFrontendHttps({ ...env, SCP_CV_HTTPS_KEY_FILE: 'link-to-private/key.pem' }, directory, now), /frontend目录外.*\.pem/);
});
test('外部路径经链接指向frontend实体文件时拒绝', () => {
  assert.throws(() => loadFrontendHttps({ ...env, SCP_CV_HTTPS_KEY_FILE: '../link-to-frontend/public/key.pem' }, directory, now), /frontend目录外.*\.pem/);
});
test('frontend自身由目录链接访问时仍拒绝其中指向私钥的链接', () => {
  assert.throws(() => loadFrontendHttps({ ...env, SCP_CV_HTTPS_KEY_FILE: 'link-to-private/key.pem' }, join(fixtureDirectory, 'link-to-frontend'), now), /frontend目录外.*\.pem/);
});
