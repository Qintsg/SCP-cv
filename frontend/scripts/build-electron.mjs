/** 可重复Electron开发目录构建：固定运行时与锁定pnpm，只编包、不发布或改动全局工具。 */
import { readFile, stat, realpath } from 'node:fs/promises';
import { dirname, resolve, join, relative, isAbsolute } from 'node:path';
import { fileURLToPath } from 'node:url';
import { createRequire } from 'node:module';
import { createHash } from 'node:crypto';
import { build, Platform, Arch } from 'electron-builder';
import { verifyElectronPackage } from './electron-package-integrity.mjs';

const frontend = resolve(dirname(fileURLToPath(import.meta.url)), '..'), root = resolve(frontend, '..');
const argumentsMap = new Map();
for (let index = 2; index < process.argv.length; index += 2) argumentsMap.set(process.argv[index], process.argv[index + 1]);
if (argumentsMap.get('--development-only') !== 'true') throw new Error('仅本机开发目录包须显式--development-only true；不能借此入口公开分发。');
const assets = resolve(argumentsMap.get('--assets') || join(root, '.validation/client-preview-020/assets'));
const manifest = JSON.parse(await readFile(join(assets, 'media-assets.json'), 'utf8'));
if (manifest.development_only !== true) throw new Error('此入口只接本机development-only媒体资产，不代替公开源码合规审核。');
if (!/^[\da-f]{64}$/i.test(manifest.ffmpeg_sha256 || '') || createHash('sha256').update(await readFile(join(assets, 'ffmpeg.exe'))).digest('hex') !== manifest.ffmpeg_sha256.toLowerCase()) throw new Error('包内FFmpeg与准备时的已核摘要不一致。');
const packageMetadata = JSON.parse(await readFile(join(frontend, 'package.json'), 'utf8'));
const nativeRules = (packageMetadata.build?.extraResources || []).filter(item => typeof item === 'object' && item.to === 'client-preview');
if (nativeRules.length !== 1 || resolve(frontend, nativeRules[0].from) !== assets) throw new Error('媒体资产必须与仓库唯一client-preview打包规则一致；禁止叠加同目的地并发复制。');
const output = resolve(argumentsMap.get('--output') || join(root, '.validation/client-preview-020/package-local-20261021'));
const outputRelative = relative(join(root, '.validation/client-preview-020'), output);
if (!outputRelative || outputRelative.startsWith('..') || isAbsolute(outputRelative)) throw new Error('开发目录包必须在本轮独占.validation/client-preview-020子目录，不能清空仓库或用户目录。');
const electronDist = join(frontend, 'node_modules/electron/dist');
if ((await readFile(join(electronDist, 'version'), 'utf8')).trim() !== '44.2.0') throw new Error('只使用已安装且版本匹配的Electron44.2.0。');
for (const asset of ['electron.exe', 'resources/default_app.asar']) await stat(join(electronDist, asset));

// 明确调用当前仓库锁定版；PATH全局pnpm可能更新到其它版本而触发自动切换/网络等待。
let pinnedCli = argumentsMap.get('--pnpm-bin');
if (!pinnedCli) {
  const activeEntry = process.env.npm_execpath;
  if (!activeEntry || !isAbsolute(activeEntry)) throw new Error('直接Node构建需显式--pnpm-bin；pnpm run构建自动验证当前npm_execpath的锁定版本。');
  const activePackageRoot = resolve(dirname(activeEntry), '..');
  const activeManifest = JSON.parse(await readFile(join(activePackageRoot, 'package.json'), 'utf8'));
  if (activeManifest.name !== 'pnpm' || activeManifest.version !== '11.22.0') throw new Error('当前npm_execpath不是锁定pnpm11.22.0；拒绝静默使用其它版本，请从锁定版运行或显式--pnpm-bin。');
  pinnedCli = resolve(activePackageRoot, '../../bin');
  const managedEntry = join(pinnedCli, '../node_modules/pnpm/bin/pnpm.mjs');
  if (await realpath(managedEntry) !== await realpath(activeEntry)) throw new Error('无法从当前pnpm入口核对专有bin，请显式--pnpm-bin。');
}
if (!isAbsolute(pinnedCli)) throw new Error('--pnpm-bin必须是明确绝对目录，不改变全局PATH。');
await stat(join(pinnedCli, 'pnpm.CMD'));
const pinnedManifest = JSON.parse(await readFile(join(pinnedCli, '../node_modules/pnpm/package.json'), 'utf8'));
if (pinnedManifest.name !== 'pnpm' || pinnedManifest.version !== '11.22.0') throw new Error('包管理器目录不是锁定pnpm11.22.0。');
process.env.PATH = `${pinnedCli};${process.env.PATH || ''}`;
process.env.npm_config_user_agent = 'pnpm/11.22.0 npm/? node/' + process.versions.node;
process.env.npm_config_verify_deps_before_run = 'false';
process.env.pnpm_config_verify_deps_before_run = 'warn';
// 应用代码/依赖均已由Vite打包，生产dependencies为空；没有需要重建的Node原生扩展。
const require = createRequire(import.meta.url);
const artifacts = await build({ projectDir: frontend, targets: Platform.WINDOWS.createTarget(['dir'], Arch.x64),
  publish: 'never', config: { electronDist, electronVersion: '44.2.0', npmRebuild: false,
    directories: { output } } });
const integrity = await verifyElectronPackage(frontend, assets, output);
console.log(JSON.stringify({ output, developmentOnly: true, artifacts, builderVersion: require('electron-builder/package.json').version,
  appCodeFiles: integrity.appCodeFiles, nativeAssetFiles: integrity.nativeAssetFiles, asarSha256: integrity.asarSha256 }));
