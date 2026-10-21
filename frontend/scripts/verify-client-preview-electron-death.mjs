/** 真Electron父死亡验证：私有HTTPS假认证+停数据回环源，强杀父后验证自有helper/FFmpeg退出。 */
import assert from 'node:assert/strict';
import { spawn, spawnSync } from 'node:child_process';
import { createServer } from 'node:net';
import { mkdir, readFile, writeFile } from 'node:fs/promises';
import { resolve, join, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';
import { randomUUID } from 'node:crypto';

const root = resolve(dirname(fileURLToPath(import.meta.url)), '../..'), args = new Map();
for (let index = 2; index < process.argv.length; index += 2) args.set(process.argv[index], process.argv[index + 1]);
if (!args.has('--assets') || !args.has('--cert') || !args.has('--key')) throw new Error('需显式--assets、--cert、--key；不能忽略正常TLS。');
const assets = resolve(args.get('--assets')), directory = join(root, '.validation/client-preview-020'); await mkdir(directory, { recursive: true });
const report = join(directory, `electron-hold-${randomUUID()}.json`), sockets = new Set();
const source = createServer(socket => { sockets.add(socket); socket.on('close', () => sockets.delete(socket)); socket.on('error', () => {}); });
await new Promise(resolveListen => source.listen(0, '127.0.0.1', resolveListen));
const environment = { ...process.env, SCP_CV_CLIENT_PREVIEW_DEV_ASSETS: assets,
  SCP_CV_NATIVE_PREVIEW_TEST_INPUT: `rtsp://127.0.0.1:${source.address().port}/stalled`, SCP_CV_NATIVE_PREVIEW_TEST_REPORT: report,
  SCP_CV_NATIVE_PREVIEW_TEST_CERT: resolve(args.get('--cert')), SCP_CV_NATIVE_PREVIEW_TEST_KEY: resolve(args.get('--key')), SCP_CV_NATIVE_PREVIEW_TEST_HOLD: 'true' };
delete environment.ELECTRON_RUN_AS_NODE; delete environment.NODE_OPTIONS;
const electron = spawn(join(root, 'frontend/node_modules/electron/dist/electron.exe'), [join(root, 'frontend/scripts/native-preview-electron')],
  { cwd: root, env: environment, windowsHide: true, shell: false, stdio: ['ignore', 'pipe', 'pipe'] });
electron.stdout.resume(); electron.stderr.resume();
/** :param milliseconds: 有界短轮询。:returns: 短暂等待。 */
const delay = milliseconds => new Promise(resolveDelay => setTimeout(resolveDelay, milliseconds));
/** :param pid: 本测试真实自有PID。:returns: 只读存活状态。 */
function alive(pid) { try { process.kill(pid, 0); return true; } catch { return false; } }
/** :param parent: 自有父PID。:param expectedName: 精确程序名。:returns: 只读发现本人子进程，不读取命令行/凭据。 */
function childIdentity(parent, expectedName) {
  assert.ok(Number.isInteger(parent)); assert.match(expectedName, /^[\w.]+$/);
  const result = spawnSync('powershell.exe', ['-NoProfile', '-Command', `Get-CimInstance Win32_Process -Filter "ParentProcessId=${parent} AND Name='${expectedName}'" | Select-Object ProcessId,ExecutablePath | ConvertTo-Json -Compress`], { encoding: 'utf8', windowsHide: true, shell: false });
  if (result.status !== 0 || !result.stdout.trim()) return null;
  const parsed = JSON.parse(result.stdout); return Array.isArray(parsed) ? parsed[0] : parsed;
}
try {
  let ready;
  for (let index = 0; index < 120; index++) {
    try { ready = JSON.parse(await readFile(report, 'utf8')); if (ready.ready) break; } catch {}
    if (electron.exitCode !== null || electron.signalCode !== null) throw new Error('Electron在持有测试前退出');
    await delay(100);
  }
  assert.equal(ready?.ready, true); assert.equal(ready.mainPid, electron.pid);
  const helper = childIdentity(electron.pid, 'ScpCv.ClientPreviewHost.exe');
  assert.ok(helper && resolve(helper.ExecutablePath) === join(assets, 'ScpCv.ClientPreviewHost.exe'));
  let ffmpeg;
  for (let index = 0; index < 20; index++) { ffmpeg = childIdentity(helper.ProcessId, 'ffmpeg.exe'); if (ffmpeg) break; await delay(50); }
  assert.ok(ffmpeg && resolve(ffmpeg.ExecutablePath) === join(assets, 'ffmpeg.exe'));
  assert.ok(alive(helper.ProcessId) && alive(ffmpeg.ProcessId));
  const started = performance.now(); electron.kill('SIGKILL');
  while (alive(helper.ProcessId) || alive(ffmpeg.ProcessId)) { if (performance.now() - started > 4800) throw new Error('Electron强杀后仍有自有媒体进程'); await delay(20); }
  const result = { electron: '44.2.0', method: 'strong-kill-actual-electron-parent', stalledUpstream: true,
    milliseconds: Math.round(performance.now() - started), residual: false, boundary: '真实Main/HTTPS假认证/私有app流，TCP上游不产数据；无D4/设备/GUI输入' };
  await writeFile(join(directory, 'electron-parent-death.json'), `${JSON.stringify(result, null, 2)}\n`, 'utf8'); console.log(JSON.stringify(result));
} finally {
  if (electron.exitCode === null && electron.signalCode === null) electron.kill();
  for (const socket of sockets) socket.destroy(); await new Promise(resolveClose => source.close(resolveClose));
}
