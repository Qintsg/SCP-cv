// 文件描述：独占目录中核验正式Electron的模块Worker、平台桥和iframe响应头。
// 项目：SCP-cv；文件：verify-electron-resources-031.mjs；作者：Qintsg；日期：2026-10-30。
import { spawn } from 'node:child_process';
import { copyFile, mkdir, mkdtemp, readFile, realpath, symlink, stat, writeFile } from 'node:fs/promises';
import { resolve, join, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';
import assert from 'node:assert/strict';

const root = resolve(dirname(fileURLToPath(import.meta.url)), '../..');
const stage = join(root, '.validation/navigation-031');
const fixture = join(stage, 'electron-resources-fixture');
const frontend = join(root, 'frontend'), built = join(frontend, 'dist-app');
await mkdir(fixture, { recursive: true }); await stat(join(built, 'index.html'));
const fixturePackage = join(fixture, 'package.json'), packageSource = join(frontend, 'scripts/electron-resources-031/package.json');
try { assert.equal(await readFile(fixturePackage, 'utf8'), await readFile(packageSource, 'utf8')); }
catch (error) { if (error.code !== 'ENOENT') throw error; await copyFile(packageSource, fixturePackage); }
const linked = join(fixture, 'dist-app');
try { await stat(linked); }
catch (error) { if (error.code !== 'ENOENT') throw error; await symlink(built, linked, 'junction'); }
assert.equal(await realpath(linked), await realpath(built));
const run = await mkdtemp(join(stage, 'electron-resources-'));
const environment = { ...process.env, SCP_CV_ELECTRON_RESOURCES_RUN: run };
for (const name of ['ELECTRON_RUN_AS_NODE', 'NODE_OPTIONS', 'SCP_CV_ELECTRON_DEV_URL']) delete environment[name];
const child = spawn(join(frontend, 'node_modules/electron/dist/electron.exe'), [fixture], {
  cwd: root, env: environment, windowsHide: true, shell: false, stdio: ['ignore', 'pipe', 'pipe'],
});
let standardOutput = '', standardError = '';
child.stdout.on('data', bytes => { standardOutput += bytes; }); child.stderr.on('data', bytes => { standardError += bytes; });
const timeout = setTimeout(() => child.kill(), 40000);
const code = await new Promise((resolveExit, reject) => { child.once('error', reject); child.once('exit', resolveExit); });
clearTimeout(timeout);
await writeFile(join(run, 'process.json'), JSON.stringify({ pid: child.pid, code, standardOutput, standardError }, null, 2) + '\n');
const report = JSON.parse(await readFile(join(run, 'report.json'), 'utf8'));
assert.equal(code, 0); assert.equal(report.success, true, JSON.stringify(report));
console.log(JSON.stringify({ run, success: true, electron: report.electron, worker: report.resources.worker, frame: report.resources.frame,
  sidebarBoundary: report.sidebarBoundary }));
