/** 清晰共享参数、兼容回退和真实码率回归；替身不计作实机画质。SCP-cv 2026-10-12。 */
import assert from 'node:assert/strict';
import test, { before, after } from 'node:test';
import { createDomTestServer } from './dom-test-server.mjs';

let server, quality;
before(async () => {
  server = await createDomTestServer();
  quality = await server.ssrLoadModule('/src/features/screen-share/shareQuality.ts');
});
after(async () => { await server?.close(); });

test('采集没有1080P上限并请求完整像素/30fps', () => {
  assert.equal(quality.displayVideoConstraints.resizeMode, 'none');
  assert.equal(quality.displayVideoConstraints.width, undefined);
  assert.equal(quality.displayVideoConstraints.height, undefined);
  assert.deepEqual(quality.displayVideoConstraints.frameRate, { ideal: 30, max: 30 });
});
test('上限按像素量选取，不当作实际发送码率', () => {
  assert.equal(quality.videoBitrateLimit({ width: 1280, height: 720 }), 8_000_000);
  assert.equal(quality.videoBitrateLimit({ width: 1920, height: 1080 }), 8_000_000);
  assert.equal(quality.videoBitrateLimit({ width: 2560, height: 1440 }), 12_000_000);
  assert.equal(quality.videoBitrateLimit({ width: 3840, height: 2160 }), 20_000_000);
});
test('仅视频sender修改可写编码字段并保留RID和事务', async () => {
  let applied;
  const sender = { track: { kind: 'video', getSettings: () => ({ width: 1920, height: 1080 }) },
    getParameters: () => ({ transactionId: 'same', encodings: [{ rid: 'a', active: true }] }),
    async setParameters(params) { applied = params; } };
  assert.equal(await quality.configureVideoSender(sender, () => true), 'configured');
  assert.equal(applied.transactionId, 'same');
  assert.equal(applied.encodings[0].rid, 'a');
  assert.equal(applied.encodings[0].maxBitrate, 8_000_000);
  assert.equal(applied.encodings[0].scaleResolutionDownBy, 1);
  assert.equal(applied.degradationPreference, 'maintain-resolution');
  let audioCalls = 0;
  await quality.configureVideoSender({ track: { kind: 'audio' }, getParameters() { audioCalls++; } }, () => true);
  assert.equal(audioCalls, 0);
});
test('不支持参数有界回退，关闭后不继续尝试', async () => {
  let calls = 0;
  const sender = { track: { kind: 'video', getSettings: () => ({}) },
    getParameters: () => ({ encodings: [{}] }), async setParameters() {
      calls++; throw Object.assign(new Error(), { name: 'NotSupportedError' });
    } };
  assert.equal(await quality.configureVideoSender(sender, () => true), 'browser-default');
  assert.equal(calls, 3);
  calls = 0;
  await quality.configureVideoSender(sender, () => false);
  assert.equal(calls, 0);
});
test('码率只按同report的实际差分计算，首样本/重置为未知', () => {
  const first = { id: 'video', timestamp: 1000, bytesSent: 1000 };
  const next = { id: 'video', timestamp: 2000, bytesSent: 501000 };
  assert.equal(quality.actualBitrateMbps(undefined, first), null);
  assert.equal(quality.actualBitrateMbps(first, next), 4);
  assert.equal(quality.actualBitrateMbps(next, first), null);
  assert.equal(quality.actualBitrateMbps(first, { ...next, id: 'other' }), null);
});

test('兼容回退删除浏览器返回的旧限制字段，而非带着不支持的字段重试', async () => {
  const applied = [];
  const sender = { track: { kind: 'video', getSettings: () => ({}) },
    getParameters: () => ({ degradationPreference: 'maintain-framerate', encodings: [{ maxFramerate: 60, scaleResolutionDownBy: 2 }] }),
    async setParameters(params) {
      applied.push(params);
      if (applied.length < 3) throw Object.assign(new Error(), { name: 'InvalidModificationError' });
    } };
  assert.equal(await quality.configureVideoSender(sender, () => true), 'compatible');
  assert.equal(applied[1].degradationPreference, undefined);
  assert.equal(applied[2].encodings[0].maxFramerate, undefined);
  assert.equal(applied[2].encodings[0].scaleResolutionDownBy, undefined);
  assert.equal(applied[2].encodings[0].maxBitrate, 8_000_000);
});
