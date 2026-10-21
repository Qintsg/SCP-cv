/**
 * 构建并核验 Electron 本机开发目录包。
 * @Project: SCP-cv
 * @File: build-electron.mjs
 * @Author: Qintsg
 * @Date: 2026-10-21
 */
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

// 使用 npm run 提供的实际入口，构建过程保持现有 PATH 和包管理器环境。
const npmCli = process.env.npm_execpath;
if (!npmCli || !isAbsolute(npmCli)) throw new Error('请通过 npm run build:electron 启动构建，以验证实际 npm 入口。');
const npmRoot = resolve(dirname(npmCli), '..');
const npmMetadata = JSON.parse(await readFile(join(npmRoot, 'package.json'), 'utf8'));
if (npmMetadata.name !== 'npm' || npmMetadata.version !== '11.12.1' || packageMetadata.packageManager !== 'npm@11.12.1')
  throw new Error('Electron 构建要求 npm 11.12.1，与 frontend/package.json 的 packageManager 一致。');
if (await realpath(npmCli) !== await realpath(join(npmRoot, 'bin/npm-cli.js')))
  throw new Error('npm_execpath 必须指向已核验 npm 包的 bin/npm-cli.js。');
// 页面依赖由Vite打包；主进程的纯JavaScript媒体依赖随asar发布，无需重建Node原生扩展。
const require = createRequire(import.meta.url);
const artifacts = await build({ projectDir: frontend, targets: Platform.WINDOWS.createTarget(['dir'], Arch.x64),
  publish: 'never', config: { electronDist, electronVersion: '44.2.0', npmRebuild: false,
    directories: { output } } });
const integrity = await verifyElectronPackage(frontend, assets, output);
console.log(JSON.stringify({ output, developmentOnly: true, artifacts, builderVersion: require('electron-builder/package.json').version,
  appCodeFiles: integrity.appCodeFiles, nativeAssetFiles: integrity.nativeAssetFiles, asarSha256: integrity.asarSha256 }));
