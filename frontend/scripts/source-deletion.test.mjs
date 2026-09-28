/* 删除失败必须同步真实媒体库，同时保留可读的原始错误。 */
import assert from 'node:assert/strict';
import test from 'node:test';
import { runSourceDeletion } from '../src/stores/sourceDeletion.ts';

test('删除成功保持既有本地移除路径，不额外刷新', async () => {
  let refreshes = 0;
  await runSourceDeletion(async () => {}, async () => { refreshes += 1; });
  assert.equal(refreshes, 0);
});

test('隔离清理错误后刷新真实列表再抛出原错误', async () => {
  const error = new Error('源记录已删除，但隔离原件清理未完成');
  let refreshed = false;
  await assert.rejects(runSourceDeletion(async () => { throw error; }, async () => { refreshed = true; }),
    (actual) => actual === error);
  assert.equal(refreshed, true);
});

test('刷新失败不掩盖删除失败原因', async () => {
  const error = new Error('媒体原件被占用，删除未生效');
  await assert.rejects(runSourceDeletion(async () => { throw error; }, async () => { throw new Error('断网'); }),
    (actual) => actual === error);
});
