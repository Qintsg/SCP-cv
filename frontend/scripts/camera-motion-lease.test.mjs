/* 运动租约实际代码的迟到响应回归；不使用网络或物理设备。 */
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { setImmediate as nextTurn } from 'node:timers/promises';
import test from 'node:test';
import ts from 'typescript';
const source = readFileSync(new URL('../src/features/devices/motionLease.ts', import.meta.url), 'utf8');
const compiled = ts.transpileModule(source, { compilerOptions: { target: ts.ScriptTarget.ESNext, module: ts.ModuleKind.ESNext } }).outputText;
const { CameraMotionLease } = await import(`data:text/javascript;base64,${Buffer.from(compiled).toString('base64')}`);

test('开始尚未回应时松手，迟到成功不能恢复心跳', async context => {
  context.mock.timers.enable({ apis: ['setInterval'] });
  const requests = []; let acknowledge;
  const lease = new CameraMotionLease(request => { requests.push(request); return request.phase === 'start' ? new Promise(resolve => { acknowledge = resolve; }) : Promise.resolve(); },
    () => {}, () => {}, () => 123456, () => 'test-motion');
  lease.start('left'); lease.stop(); acknowledge(); await nextTurn(); context.mock.timers.tick(1200); await nextTurn();
  assert.deepEqual(requests.map(request => request.phase), ['start', 'stop']);
  assert.ok(requests.every(request => request.motion_id === 'test-motion'));
});

test('按住仅续租，松手停止且不再续租', async context => {
  context.mock.timers.enable({ apis: ['setInterval'] }); const requests = [];
  const lease = new CameraMotionLease(async request => { requests.push(request); }, () => {}, () => {}, () => 1000, () => 'hold-motion');
  lease.start('zoom_in'); await nextTurn(); context.mock.timers.tick(300); await nextTurn();
  context.mock.timers.tick(300); await nextTurn(); lease.stop(); context.mock.timers.tick(1500); await nextTurn();
  assert.deepEqual(requests.map(request => request.phase), ['start', 'hold', 'hold', 'stop']);
});

test('续租失败释放，不重新发按下且报告错误', async context => {
  context.mock.timers.enable({ apis: ['setInterval'] }); const requests = []; const errors = [];
  const lease = new CameraMotionLease(async request => { requests.push(request); if (request.phase === 'hold') throw new Error('network'); }, error => errors.push(error), () => {}, () => 1000, () => 'failed-hold');
  lease.start('up'); await nextTurn(); context.mock.timers.tick(300); await nextTurn(); context.mock.timers.tick(3000); await nextTurn();
  assert.deepEqual(requests.map(request => request.phase), ['start', 'hold', 'stop']); assert.equal(errors.length, 1);
});
