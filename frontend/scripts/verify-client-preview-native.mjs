/** 显式本机原生验收：仅随机回环停数据socket，真实EOF/心跳/父死亡/Job回收，不连接现场。 */
import assert from 'node:assert/strict';
import { spawn } from 'node:child_process';
import { createServer } from 'node:net';
import { resolve, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { writeFile, mkdir } from 'node:fs/promises';

const args = process.argv.slice(2);
const assets = resolve(args[1] || '.validation/client-preview-020/assets');
const ownFile = fileURLToPath(import.meta.url);
/** :param milliseconds: 测试短轮询，不阻断用户沟通。:returns: 短期等待。 */
const delay = milliseconds => new Promise(resolveDelay => setTimeout(resolveDelay, milliseconds));
/** :param pid: 本用例真实句柄报告的PID。:returns: 只读存活事实。 */
function alive(pid) { try { process.kill(pid, 0); return true; } catch { return false; } }
/** :param condition: 真实退出条件。:param timeout: 有界预算。:returns: 实际状态出现的耗时。 */
async function until(condition, timeout = 4800) {
  const started = performance.now();
  while (!condition()) { if (performance.now() - started > timeout) throw new Error('自有进程未在预算内退出'); await delay(20); }
  return Math.round(performance.now() - started);
}
/** :param port: 测试专有回环socket。:returns: 无原地址日志的helper与真实FFmpeg PID。 */
async function launch(port) {
  const helper = spawn(join(assets, 'ScpCv.ClientPreviewHost.exe'), [], { cwd: assets, windowsHide: true, shell: false, stdio: ['pipe', 'pipe', 'pipe'] });
  helper.stdin.on('error', () => {}); helper.stdout.resume();
  let ffmpegPid, errors = '';
  helper.stderr.on('data', bytes => { errors += bytes; for (const line of errors.split('\n')) { try { const item = JSON.parse(line); if (item.event === 'started') ffmpegPid = item.pid; } catch {} } });
  helper.stdin.write(`${JSON.stringify({ uri: `rtsp://127.0.0.1:${port}/stalled`, mode: 'copy' })}\n`);
  await until(() => !!ffmpegPid || helper.exitCode !== null);
  assert.ok(ffmpegPid, '必须观察到真实自有FFmpeg启动，而非只测配置错误');
  assert.ok(alive(ffmpegPid));
  return { helper, ffmpegPid };
}
if (args[0] === '--child-parent') {
  const own = await launch(Number(args[2]));
  process.stdout.write(`${JSON.stringify({ helperPid: own.helper.pid, ffmpegPid: own.ffmpegPid })}\n`);
  setInterval(() => { if (own.helper.stdin.writable) own.helper.stdin.write('heartbeat\n'); }, 500);
  await new Promise(() => {});
} else {
  assert.equal(args[0], '--assets', '显式指定--assets；默认测试不启动真实进程。');
  const sockets = new Set(), server = createServer(socket => { sockets.add(socket); socket.on('close', () => sockets.delete(socket)); socket.on('error', () => {}); });
  await new Promise(resolveListen => server.listen(0, '127.0.0.1', resolveListen));
  const port = server.address().port, results = [];
  try {
    const eof = await launch(port), started = performance.now(); eof.helper.stdin.end();
    await until(() => eof.helper.exitCode !== null || eof.helper.signalCode !== null);
    await until(() => !alive(eof.ffmpegPid));
    results.push({ name: 'stdin-eof-stalled-upstream', milliseconds: Math.round(performance.now() - started), residual: false });

    const heartbeat = await launch(port), heartbeatStarted = performance.now();
    await until(() => heartbeat.helper.exitCode !== null || heartbeat.helper.signalCode !== null, 4800);
    await until(() => !alive(heartbeat.ffmpegPid));
    results.push({ name: 'heartbeat-timeout-stalled-upstream', milliseconds: Math.round(performance.now() - heartbeatStarted), residual: false });

    const crashed = await launch(port), crashStarted = performance.now(); crashed.helper.kill('SIGKILL');
    await until(() => crashed.helper.exitCode !== null || crashed.helper.signalCode !== null);
    await until(() => !alive(crashed.ffmpegPid));
    results.push({ name: 'kill-helper-job', milliseconds: Math.round(performance.now() - crashStarted), residual: false });

    const parent = spawn(process.execPath, [ownFile, '--child-parent', assets, String(port)], { windowsHide: true, shell: false, stdio: ['ignore', 'pipe', 'pipe'] });
    let identity, text = ''; parent.stderr.resume();
    parent.stdout.on('data', bytes => { text += bytes; try { identity = JSON.parse(text.trim()); } catch {} });
    await until(() => !!identity); assert.ok(alive(identity.helperPid)); assert.ok(alive(identity.ffmpegPid));
    const parentStarted = performance.now(); parent.kill('SIGKILL');
    await until(() => !alive(identity.helperPid) && !alive(identity.ffmpegPid));
    results.push({ name: 'kill-node-parent-stalled-upstream', milliseconds: Math.round(performance.now() - parentStarted), residual: false });
    for (const result of results) assert.ok(result.milliseconds < 5000);
    const output = resolve('.validation/client-preview-020'); await mkdir(output, { recursive: true });
    await writeFile(join(output, 'native-lifecycle.json'), `${JSON.stringify({ assets, boundary: '真实自包含helper/FFmpeg，只连接127.0.0.1且对端不返回数据；不是D4或实体出画', results }, null, 2)}\n`, 'utf8');
    console.log(JSON.stringify(results));
  } finally { for (const socket of sockets) socket.destroy(); await new Promise(resolveClose => server.close(resolveClose)); }
}
