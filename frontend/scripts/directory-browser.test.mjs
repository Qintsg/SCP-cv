/** 目录父链、递归范围与公开路径纯逻辑回归。SCP-cv 2026-10-12。 */
import assert from 'node:assert/strict';
import test, { before, after } from 'node:test';
import { createDomTestServer } from './dom-test-server.mjs';
let server, paths;
before(async () => { server = await createDomTestServer(); paths = await server.ssrLoadModule('/src/features/directories/directoryPath.ts'); });
after(async () => { await server?.close(); });
const folders = [{ id: 1, parent_id: null, name: 'examplefolder' }, { id: 2, parent_id: 1, name: 'child' }];
test('根与嵌套目录从/开始且循环不会无限遍历', () => {
  assert.equal(paths.directoryPath(null, folders), '/');
  assert.equal(paths.directoryPath(2, folders), '/examplefolder/child');
  assert.equal(paths.directoryPath(3, [{ id: 3, parent_id: 3, name: 'cycle' }]), '/cycle');
});
test('默认当前目录；仅明确递归搜索才包含子目录', () => {
  assert.equal(paths.inDirectory(2, 1, folders, false), false);
  assert.equal(paths.inDirectory(2, 1, folders, true), true);
  assert.equal(paths.inDirectory(1, 2, folders, true), false);
  assert.equal(paths.inDirectory(null, 1, folders, true), false);
});
test('优先后端真实path，不把原始文件名覆盖实际重名保存路径', () => {
  assert.equal(paths.sourcePath({ path: '/examplefolder/a (2).png', name: 'display', folder_id: 1, original_filename: 'a.png' }, folders), '/examplefolder/a (2).png');
});
