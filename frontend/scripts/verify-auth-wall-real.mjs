// 文件描述：使用真实 ControlHost 验证认证恢复和大屏布局校验。
// 项目：SCP-cv；文件：verify-auth-wall-real.mjs；作者：Qintsg；日期：2026-10-27。
import assert from 'node:assert/strict';
import { spawn } from 'node:child_process';
import { randomBytes } from 'node:crypto';
import { mkdir } from 'node:fs/promises';
import { createServer as createNetServer } from 'node:net';
import { setTimeout as delay } from 'node:timers/promises';
import { fileURLToPath } from 'node:url';
import { createServer } from 'vite';

const frontend = fileURLToPath(new URL('../', import.meta.url));
const socket = createNetServer();
await new Promise((resolve, reject) => socket.once('error', reject).listen(0, '127.0.0.1', resolve));
const port = socket.address().port;
await new Promise((resolve, reject) => socket.close(error => error ? reject(error) : resolve()));
const origin = `http://127.0.0.1:${port}`;
const vite = await createServer({ configFile: false, root: frontend, mode: 'web', appType: 'custom',
  resolve: { alias: { '@': fileURLToPath(new URL('../src/', import.meta.url)) } },
  server: { middlewareMode: true }, logLevel: 'error' });
let host, hostExit;
try {
  const { validateBlocks } = await vite.ssrLoadModule('/src/features/runtime/wall/layoutModel.ts');
  const blocks = validateBlocks([{ region: 'left', input: { kind: 'window_1', ip_address: '  ' } },
    { region: 'right', input: { kind: 'window_2', ip_address: '' } }]);
  assert.equal(blocks.length, 2);
  assert.equal(blocks[0].input.ip_address, '');
  assert.throws(() => validateBlocks([{ region: 'fullscreen', input: { kind: 'window_1', ip_address: '192.168.5.63' } }]));
  assert.throws(() => validateBlocks([{ region: 'fullscreen', input: { kind: 'ip_stream', ip_address: '999.1.1.1' } }]));

  const { createPinia, setActivePinia } = await vite.ssrLoadModule('pinia');
  const { clientConnection } = await vite.ssrLoadModule('/src/platform/connection.ts');
  const { useAuthStore } = await vite.ssrLoadModule('/src/stores/auth.ts');
  setActivePinia(createPinia());
  clientConnection.restoreProfile({ origin, displayName: '本机验证', allowInsecureDevelopment: true });
  const auth = useAuthStore();
  await auth.ensureInitialized();
  assert.equal(auth.initialized, true);
  assert.equal(auth.isAuthenticated, false);
  assert.ok(auth.initializationError);

  const dataRoot = fileURLToPath(new URL(`../../.validation/auth-wall-${randomBytes(8).toString('hex')}/`, import.meta.url));
  await mkdir(dataRoot, { recursive: false });
  const assembly = fileURLToPath(new URL('../../runtime-dotnet/src/ScpCv.ControlHost/bin/Release/net10.0/ScpCv.ControlHost.dll', import.meta.url));
  host = spawn(process.env.SCP_CV_TEST_DOTNET || 'dotnet', [assembly, `--urls=${origin}`, '--SafetyMode=Hardware', `--DataRoot=${dataRoot}`],
    { cwd: fileURLToPath(new URL('../../runtime-dotnet/src/ScpCv.ControlHost/', import.meta.url)), stdio: 'inherit', env: process.env });
  hostExit = new Promise(resolve => host.once('exit', (code, signal) => resolve({ code, signal })));
  let ready = false;
  for (let attempt = 0; attempt < 60; attempt++) {
    if (host.exitCode !== null) throw new Error(`ControlHost 提前退出：${host.exitCode}`);
    try { ready = (await fetch(`${origin}/health/ready`)).ok; } catch { /* 等待真实服务启动。 */ }
    if (ready) break;
    await delay(500);
  }
  assert.equal(ready, true, 'ControlHost 未就绪');
  await auth.retryInitialization();
  assert.equal(auth.initialized, true);
  assert.equal(auth.isAuthenticated, false);
  assert.equal(auth.initializationError, '');
  console.log('真实 ControlHost：离线错误、恢复后未登录状态及布局地址校验通过。');
} finally {
  await vite.close();
  if (host && host.exitCode === null) {
    host.kill();
    const result = await Promise.race([hostExit, delay(10000).then(() => { throw new Error('ControlHost 未退出'); })]);
    console.log(`ControlHost 已退出：${result.code ?? result.signal}`);
  }
}
