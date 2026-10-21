/** 构建客户端专有媒体资产；只接受显式文件和摘要，公开分发另有匹配源码/许可证门禁。 */
import { spawnSync } from 'node:child_process';
import { createHash } from 'node:crypto';
import { copyFile, mkdir, readFile, writeFile, stat } from 'node:fs/promises';
import { resolve, join, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';

const root = resolve(dirname(fileURLToPath(import.meta.url)), '../..');
const args = new Map();
for (let index = 2; index < process.argv.length; index += 2) args.set(process.argv[index], process.argv[index + 1]);
const developmentOnly = args.get('--development-only') === 'true';
const ffmpeg = resolve(args.get('--ffmpeg') || ''), license = resolve(args.get('--license') || '');
const expected = args.get('--sha256') || '', origin = args.get('--origin') || '';
const sourceArchive = args.get('--source-archive');
const output = resolve(args.get('--output') || join(root, '.validation/client-preview-020/assets'));
if (!/^[\da-f]{64}$/i.test(expected) || !origin.startsWith('https://') || !args.has('--ffmpeg') || !args.has('--license')) throw new Error('必须显式提供--ffmpeg、--sha256、--license、--origin；不查询PATH。');
if (!developmentOnly && !sourceArchive) throw new Error('公开分发门禁：缺少与二进制匹配的--source-archive；本机验证可显式--development-only true。');
const bytes = await readFile(ffmpeg), digest = createHash('sha256').update(bytes).digest('hex');
if (digest !== expected.toLowerCase()) throw new Error('FFmpeg摘要不匹配，未生成媒体资产。');
if (!(await stat(license)).isFile()) throw new Error('许可证不是明确文件。');
const configuration = spawnSync(ffmpeg, ['-version'], { encoding: 'utf8', windowsHide: true, shell: false });
if (configuration.status !== 0 || !configuration.stdout.includes('ffmpeg version')) throw new Error('FFmpeg版本检查失败。');
await mkdir(output, { recursive: true });
const publish = spawnSync('dotnet', ['publish', join(root, 'runtime-dotnet/src/ScpCv.ClientPreviewHost/ScpCv.ClientPreviewHost.csproj'), '-c', 'Release', '-r', 'win-x64', '--self-contained', 'true', '--nologo', '-o', output],
  { cwd: root, stdio: 'inherit', windowsHide: true, shell: false });
if (publish.status !== 0) throw new Error('自包含宿主发布失败，不可将该目录称为完整客户端资产。');
await copyFile(ffmpeg, join(output, 'ffmpeg.exe'));
await copyFile(license, join(output, 'FFmpeg-LICENSE.txt'));
if (sourceArchive) await copyFile(resolve(sourceArchive), join(output, 'FFmpeg-corresponding-source.zip'));
await writeFile(join(output, 'media-assets.json'), `${JSON.stringify({ platform: 'win-x64', ffmpeg_sha256: digest, origin,
  version_configuration: configuration.stdout.trim(), development_only: developmentOnly, corresponding_source_included: Boolean(sourceArchive),
  note: '含GPL组件的构建不能标为纯LGPL。单个LICENSE或非商业用途不免除匹配源码及第三方依赖义务；未审计完整源码匹配不得公开分发。' }, null, 2)}\n`, 'utf8');
console.log(JSON.stringify({ output, developmentOnly, ffmpegSha256: digest }));
