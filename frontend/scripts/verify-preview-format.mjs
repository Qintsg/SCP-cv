/** 文件描述：真实媒体格式组件与损坏输入验收；项目：SCP-cv；文件：verify-preview-format.mjs；作者：Qintsg；日期：2026-10-21。 */
import { spawn } from 'node:child_process';
import { mkdir, writeFile } from 'node:fs/promises';
import { resolve, dirname, join, relative } from 'node:path';
import { fileURLToPath } from 'node:url';
import { randomUUID, createHash } from 'node:crypto';
import assert from 'node:assert/strict';
import { NativeFlvLibrary } from '../electron-dist/preview/flvLibrary.js';

const parameters = new Map();
for (let index = 2; index < process.argv.length; index += 2) parameters.set(process.argv[index], process.argv[index + 1]);
const root = resolve(dirname(fileURLToPath(import.meta.url)), '../..');
const executable = parameters.get('--ffmpeg'), decoderPath = parameters.get('--ffprobe');
if (!executable || !decoderPath) throw new Error('需要显式--ffmpeg和--ffprobe绝对路径');
const outputRoot = resolve(parameters.get('--output') || join(root, '.validation/preview-format-022'));
const relativeOutput = relative(join(root, '.validation'), outputRoot);
if (!relativeOutput || relativeOutput.startsWith('..')) throw new Error('实际验收输出必须位于仓库.validation独立子目录');
const directory = join(outputRoot, randomUUID()); await mkdir(directory, { recursive: true });

/** :param executable: 显式工具路径。:param argumentsList: 有界实际媒体参数。:returns: 确认退出后的原始输出，任何工具错误都终止验收。 */
async function run(executable, argumentsList) {
  const child = spawn(executable, argumentsList, { windowsHide: true, shell: false, stdio: ['ignore', 'pipe', 'pipe'] });
  const output = [], errors = []; let size = 0;
  child.stdout.on('data', bytes => { size += bytes.length; if (size > 16 * 1024 * 1024) child.kill(); else output.push(bytes); });
  child.stderr.on('data', bytes => errors.push(bytes));
  const timeout = setTimeout(() => child.kill(), 15000);
  const code = await new Promise((resolveExit, reject) => { child.once('error', reject); child.once('exit', resolveExit); });
  clearTimeout(timeout);
  assert.equal(code, 0, '实际媒体工具必须成功退出'); assert.equal(Buffer.concat(errors).length, 0, '实际媒体工具不得存在错误');
  return { bytes: Buffer.concat(output), pid: child.pid };
}

/** :param bytes: 实际生成文件或其损坏副本。:param chunkSize: 传输边界。:returns: 正式成熟组件输出与分类，文件字段由库处理。 */
function processMedia(bytes, chunkSize) {
  const output = [], tags = [];
  const reader = new NativeFlvLibrary(header => output.push(Buffer.from(header)), tag => { output.push(Buffer.from(tag.bytes)); tags.push({ kind: tag.kind, timestamp: tag.timestamp }); }, error => { throw error; });
  try {
    for (let offset = 0; offset < bytes.length; offset += chunkSize) reader.accept(bytes.subarray(offset, offset + chunkSize));
    reader.finish(); return { bytes: Buffer.concat(output), tags };
  } finally { reader.dispose(); }
}

const generated = await run(resolve(executable), ['-nostdin', '-hide_banner', '-loglevel', 'error', '-f', 'lavfi', '-i', 'testsrc2=size=320x180:rate=25', '-t', '2', '-an', '-c:v', 'libx264', '-threads', '1', '-preset', 'ultrafast', '-tune', 'zerolatency', '-pix_fmt', 'yuv420p', '-bf', '0', '-g', '5', '-keyint_min', '5', '-sc_threshold', '0', '-f', 'flv', '-flvflags', 'no_duration_filesize', 'pipe:1']);
const complete = processMedia(generated.bytes, 1024), bytewise = processMedia(generated.bytes, 1);
assert.ok(complete.bytes.equals(generated.bytes)); assert.ok(bytewise.bytes.equals(generated.bytes));
const file = join(directory, 'actual-media.flv'); await writeFile(file, complete.bytes);
const decoded = await run(resolve(decoderPath), ['-v', 'error', '-select_streams', 'v:0', '-show_frames', '-show_entries', 'frame=width,height,key_frame,best_effort_timestamp_time', '-of', 'json', file]);
const frames = JSON.parse(decoded.bytes.toString()).frames;
assert.equal(frames.length, 50); assert.ok(frames.every(frame => frame.width === 320 && frame.height === 180));
const signature = Buffer.from(generated.bytes); signature[0] ^= 1;
const footer = Buffer.from(generated.bytes); footer[footer.length - 1] ^= 1;
const damage = {};
for (const [name, bytes] of [['signature', signature], ['footer', footer], ['truncated_footer', generated.bytes.subarray(0, generated.bytes.length - 1)], ['truncated_payload', generated.bytes.subarray(0, generated.bytes.length - 100)]]) {
  assert.throws(() => processMedia(bytes, 1024)); damage[name] = 'rejected';
}
const result = { boundary: '真实FFmpeg→正式NativeFlvLibrary→独立FFprobe，未替换网络或进程依赖', frames: frames.length,
  keys: frames.filter(frame => frame.key_frame).length, tags: complete.tags, damage, bytes: complete.bytes.length,
  sha256: createHash('sha256').update(complete.bytes).digest('hex'), publisher_pid: generated.pid, decoder_pid: decoded.pid };
await writeFile(join(directory, 'result.json'), JSON.stringify(result, null, 2));
console.log(JSON.stringify({ directory, frames: frames.length, keys: result.keys, damage }));
