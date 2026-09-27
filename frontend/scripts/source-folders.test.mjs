/*
 * 实体媒体目录前端合同：保留全量源索引，同时按当前位置渲染并提供目录移动。
 */
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import test from 'node:test';

test('媒体库保留跨目录选源索引，列表只展示当前位置', async () => {
  const store = await readFile(new URL('../src/stores/sources.ts', import.meta.url), 'utf8');
  assert.match(store, /api\.listSources\(\)/);
  assert.match(store, /source\.folder_id !== state\.currentFolderId/);
  assert.match(store, /async moveFolder\(folderId: number, parentId: number \| null\)/);
  assert.match(store, /await api\.updateFolder\(folderId, \{ parent_id: parentId \}\)/);
});

test('源与目录移动都提供可读目标路径，排除自身及子孙目录', async () => {
  const folders = await readFile(new URL('../src/features/sources/useSourceFolders.ts', import.meta.url), 'utf8');
  assert.match(folders, /function descendantsOf\(folderId: number\)/);
  assert.match(folders, /!excluded\.has\(candidate\.id\)/);
  assert.match(folders, /candidate\.relative_path \|\| candidate\.name/);
  assert.match(folders, /folder\.relative_path \|\| folder\.name/);
});
