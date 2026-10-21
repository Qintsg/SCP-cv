/** 显式合成媒体/Electron验证编排；全部随机127.0.0.1端口，不接D4/摄像机或模拟输入。 */
import { spawn, spawnSync } from 'node:child_process';
import { createServer, connect } from 'node:net';
import { mkdir, writeFile, readFile } from 'node:fs/promises';
import { resolve, join, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';

const root = resolve(dirname(fileURLToPath(import.meta.url)), '../..'), assets = resolve(process.argv[3] || '.validation/client-preview-020/assets');
if (process.argv[2] !== '--assets') throw new Error('必须显式指定--assets；默认测试不启动原生程序。');
const output = join(root, '.validation/client-preview-020'); await mkdir(output, { recursive: true });
const children = [];
/** :param executable: 显式固定测试路径。:param args: 测试自有参数。:param extra: 子进程专有选项。:returns: 已追踪进程。 */
function launch(executable, args, extra = {}) {
  const child = spawn(executable, args, { cwd: root, windowsHide: true, shell: false, stdio: ['ignore', 'pipe', 'pipe'], ...extra });
  child.syntheticDiagnostic = ''; child.stderr.on('data', bytes => { child.syntheticDiagnostic += bytes.toString().replace(/rtsp:\/\/\S+/g, '[synthetic-loopback]').slice(0, 3000); });
  child.stdout.resume(); child.stderr.resume(); children.push(child); return child;
}
/** :returns: 未占用随机回环端口。 */
async function port() { const server = createServer(); await new Promise(resolveListen => server.listen(0, '127.0.0.1', resolveListen)); const value = server.address().port; await new Promise(resolveClose => server.close(resolveClose)); return value; }
/** :param milliseconds: 短轮询间隔。:returns: 不阻断超过60秒的等待。 */
const delay = milliseconds => new Promise(resolveDelay => setTimeout(resolveDelay, milliseconds));
/** :param value: 本用例端口。:returns: 仅TCP监听就绪，不伪装出画证据。 */
async function ready(value) {
  for (let index = 0; index < 60; index++) {
    const ok = await new Promise(resolveProbe => { const socket = connect(value, '127.0.0.1'); socket.once('connect', () => { socket.destroy(); resolveProbe(true); }); socket.once('error', () => resolveProbe(false)); });
    if (ok) return; await delay(100);
  }
  throw new Error('合成MediaMTX未监听');
}
try {
  const rtsp = await port(), rtmp = await port(), config = join(output, 'synthetic-mediamtx.yml');
  const inputUri = process.argv.includes('--rtmp') ? `rtmp://127.0.0.1:${rtmp}/synthetic` : `rtsp://127.0.0.1:${rtsp}/synthetic`;
  await writeFile(config, `logLevel: error\nrtsp: yes\nrtspAddress: 127.0.0.1:${rtsp}\nrtspTransports: [tcp]\nrtmp: yes\nrtmpAddress: 127.0.0.1:${rtmp}\nhls: no\nwebrtc: no\nsrt: no\napi: no\npaths:\n  all_others:\n    source: publisher\n`, 'utf8');
  launch(join(root, 'tools/third_party/mediamtx/mediamtx.exe'), [config]); await ready(rtsp);
  const h265 = process.argv.includes('--source-h265');
  launch(join(assets, 'ffmpeg.exe'), ['-nostdin', '-hide_banner', '-loglevel', 'error', '-re', '-f', 'lavfi', '-i', 'testsrc2=size=1280x720:rate=25',
    '-c:v', h265 ? 'libx265' : 'libx264', '-threads', '1', ...(h265 ? ['-x265-params', 'pools=1:frame-threads=1:log-level=error'] : []), '-preset', 'ultrafast', '-tune', 'zerolatency', '-pix_fmt', 'yuv420p', '-g', '12', '-an', '-f', 'rtsp', '-rtsp_transport', 'tcp', `rtsp://127.0.0.1:${rtsp}/synthetic`]);
  await delay(1000);
  if (process.argv.includes('--media-only')) {
    const { NativePreviewRegistry } = await import('../electron-dist/preview/registry.js');
    const { resolvePreviewAssets, startPreviewHelper } = await import('../electron-dist/preview/helperProcess.js');
    const registry = new NativePreviewRegistry({ owner: async () => 'synthetic-owner',
      resolve: async request => ({ uri: inputUri, sharing_key: 'f'.repeat(64), source_id: 1,
        source_revision: 1, quality: request.quality, expires_at: new Date(Date.now() + 30000).toISOString() }),
      produce: (uri, mode) => startPreviewHelper(resolvePreviewAssets(assets), uri, mode),
    });
    try {
      const lease = await registry.acquire({ source_id: 1, quality: 'high' });
      if (h265) {
        const first = await registry.stream(new Request(lease.streamUrl)), firstReader = first.body.getReader();
        await firstReader.read(); const rejected = await firstReader.read();
        if (!rejected.done) throw new Error('H265原码不能伪装为已验证H264');
        await registry.fallback(lease.id);
      }
      const abort = new AbortController(), started = performance.now(), response = await registry.stream(new Request(lease.streamUrl, { signal: abort.signal }));
      const reader = response.body.getReader(), bytes = []; let size = 0, firstBytes = 0;
      const timeout = setTimeout(() => abort.abort(), 5000);
      try {
        while (performance.now() - started < 2200 || size < 100000) {
          const next = await reader.read(); if (next.done) break;
          if (!firstBytes && next.value.length > 13) firstBytes = performance.now() - started;
          bytes.push(Buffer.from(next.value)); size += next.value.length;
        }
      } finally { clearTimeout(timeout); await reader.cancel(); await registry.release(lease.id); }
      const file = join(output, 'native-h264-copy.flv'); await writeFile(file, Buffer.concat(bytes));
      const probeIndex = process.argv.indexOf('--ffprobe'); if (probeIndex < 0) throw new Error('media-only需显式--ffprobe，测试工具不查询PATH。');
      const inspected = spawnSync(resolve(process.argv[probeIndex + 1]), ['-v', 'error', '-select_streams', 'v:0', '-count_frames', '-show_entries', 'stream=codec_name,width,height,nb_read_frames', '-of', 'json', file], { encoding: 'utf8', windowsHide: true, shell: false });
      const stream = JSON.parse(inspected.stdout).streams?.[0];
      if (inspected.status !== 0 || stream?.width !== 1280 || stream.height !== 720 || Number(stream.nb_read_frames) < 12) {
        // 只对本用例合成回环复现固定参数；原生生产仍不记录可能含凭据的运行时stderr。
        const direct = spawnSync(join(assets, 'ffmpeg.exe'), ['-nostdin', '-hide_banner', '-loglevel', 'error', '-max_alloc', '67108864', '-protocol_whitelist', 'rtmp,rtsp,tcp,udp,rtp', '-threads', '1', ...(process.argv.includes('--rtmp') ? ['-rw_timeout', '8000000'] : ['-rtsp_transport', 'tcp', '-timeout', '8000000']), '-i', inputUri, '-map', '0:v:0', '-an', '-sn', '-dn', '-c:v', 'copy', '-t', '1', '-f', 'flv', '-flvflags', 'no_duration_filesize', '-flush_packets', '1', 'pipe:1'], { encoding: 'utf8', timeout: 12000, windowsHide: true, shell: false });
        console.log(JSON.stringify({ services: children.map(child => ({ exit: child.exitCode, error: child.syntheticDiagnostic })), directError: direct.stderr, directStatus: direct.status }));
        throw new Error('真实原码FLV解码/尺寸检查失败');
      }
      const result = { boundary: '真实合成流→自包含helper→共享注册表FLV→ffprobe，非Electron app协议或D4', protocol: process.argv.includes('--rtmp') ? 'rtmp' : 'rtsp', mode: h265 ? 'h265-to-compatible-h264' : 'h264-copy', firstBytesMilliseconds: Math.round(firstBytes), bytes: size, stream };
      await writeFile(join(output, `native-media-${h265 ? 'compatible' : process.argv.includes('--rtmp') ? 'rtmp' : 'copy'}.json`), `${JSON.stringify(result, null, 2)}\n`, 'utf8'); console.log(JSON.stringify(result));
    } finally { await registry.dispose(); }
  } else {
  const environment = { ...process.env, SCP_CV_CLIENT_PREVIEW_DEV_ASSETS: assets }; delete environment.ELECTRON_RUN_AS_NODE; delete environment.NODE_OPTIONS;
  const report = join(output, `electron-app-protocol-${process.argv.includes('--rtmp') ? 'rtmp' : 'rtsp'}.json`);
  const certIndex = process.argv.indexOf('--cert'), keyIndex = process.argv.indexOf('--key');
  if (certIndex < 0 || keyIndex < 0) throw new Error('真实Electron验收须显式--cert和--key，使用正常受信TLS，不忽略证书错误。');
  environment.SCP_CV_NATIVE_PREVIEW_TEST_CERT = resolve(process.argv[certIndex + 1]);
  environment.SCP_CV_NATIVE_PREVIEW_TEST_KEY = resolve(process.argv[keyIndex + 1]);
  environment.SCP_CV_NATIVE_PREVIEW_TEST_INPUT = inputUri;
  environment.SCP_CV_NATIVE_PREVIEW_TEST_REPORT = report;
  const electron = launch(join(root, 'frontend/node_modules/electron/dist/electron.exe'), [join(root, 'frontend/scripts/native-preview-electron'), ...(process.argv.includes('--headless') ? ['--headless'] : []), '--enable-logging=stderr', '--v=1'], { env: environment });
  let diagnostic = ''; electron.stderr.on('data', bytes => { diagnostic += bytes.toString().replace(/rtsp:\/\/\S+/g, '[synthetic-loopback]'); });
  const exit = await new Promise((resolveExit, reject) => { const timeout = setTimeout(() => reject(new Error('Electron探针未在25秒完成')), 25000); electron.once('exit', code => { clearTimeout(timeout); resolveExit(code); }); electron.once('error', reject); });
  await writeFile(join(output, 'electron-diagnostic.log'), `exit=${exit}\n${diagnostic}`, 'utf8');
  if (exit !== 0) {
    console.log(JSON.stringify({ exit, diagnostic: diagnostic.slice(-4000) }));
    // 启动阶段退出不能用旧报告冒充本次真实出画；保留原始退出码与未验证状态。
    if (!diagnostic) throw new Error(`Electron在应用入口前退出（${exit}），本轮app协议真实帧尚未验证。`);
  }
  const result = JSON.parse(await readFile(report, 'utf8')); console.log(JSON.stringify(result));
  if (exit !== 0 || !result.success || result.nodeExposed || result.errors.length || result.failures.length) throw new Error('真实Electron预览验收失败');
  }
} finally {
  for (const child of children.reverse()) if (child.exitCode === null && child.signalCode === null) child.kill();
  await Promise.all(children.map(child => child.exitCode !== null || child.signalCode !== null ? undefined : new Promise(resolveExit => child.once('exit', resolveExit))));
}
