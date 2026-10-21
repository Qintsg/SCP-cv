/** 目录包制品门禁：比对当前Web/App主进程源码与包内资产，不能把旧release目录当本次成功。 */
import { createRequire } from 'node:module';
import { createHash } from 'node:crypto';
import { readFile, readdir, writeFile } from 'node:fs/promises';
import { join } from 'node:path';

/** :param bytes: 本次文件。:returns: 公开完整性摘要，不输出文件内容或凭据。 */
function digest(bytes) { return createHash('sha256').update(bytes).digest('hex'); }
/** :param root: 已明确的自有资产目录。:param prefix: 相对子目录。:returns: 不跟随symlink的文件列表。 */
async function files(root, prefix = '') {
  const result = [];
  for (const entry of await readdir(join(root, prefix), { withFileTypes: true })) {
    if (entry.isSymbolicLink()) throw new Error('构建资产中不允许未验证的符号连接。');
    const path = prefix ? `${prefix}/${entry.name}` : entry.name;
    if (entry.isDirectory()) result.push(...await files(root, path)); else result.push(path);
  }
  return result;
}
/** :param frontend: 已构建项目。:param assets: 显式媒体资产。:param output: 本次目录包输出。:returns: 每文件摘要与关键Native引用检查结果。 */
export async function verifyElectronPackage(frontend, assets, output) {
  const require = createRequire(import.meta.url), builder = require.resolve('app-builder-lib', { paths: [require.resolve('electron-builder')] });
  const asar = createRequire(builder)('@electron/asar'), directory = join(output, 'win-unpacked'), archive = join(directory, 'resources/app.asar');
  const checks = [], assetChecks = [];
  for (const group of ['dist-app', 'electron-dist']) {
    for (const file of await files(join(frontend, group))) {
      const expected = digest(await readFile(join(frontend, group, file))), actual = digest(asar.extractFile(archive, join(group, file)));
      if (expected !== actual) throw new Error(`包内代码不属于当前构建：${group}/${file}`);
      checks.push({ path: `${group}/${file}`, sha256: actual });
    }
  }
  for (const file of await files(assets)) {
    const expected = digest(await readFile(join(assets, file))), actual = digest(await readFile(join(directory, 'resources/client-preview', file)));
    if (expected !== actual) throw new Error(`包内原生资产摘要不匹配：${file}`);
    assetChecks.push({ path: file, sha256: actual });
  }
  const main = asar.extractFile(archive, join('electron-dist', 'main.js')).toString(), csp = asar.extractFile(archive, join('electron-dist', 'security.js')).toString();
  const native = asar.extractFile(archive, join('electron-dist', 'preview', 'mainPreview.js')).toString();
  if (!main.includes('setupNativePreview') || !csp.includes('worker-src') || !native.includes('process.resourcesPath')) throw new Error('包内缺少正式Native/CSP/固定资产接线。');
  const result = { developmentOnly: true, output: directory, appCodeFiles: checks.length, nativeAssetFiles: assetChecks.length,
    asarSha256: digest(await readFile(archive)), checks, assetChecks };
  await writeFile(join(output, 'package-integrity.json'), `${JSON.stringify(result, null, 2)}\n`, 'utf8');
  return result;
}
