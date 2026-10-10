/* 共享音频的独占寿命、受理/实际回读与迟到围栏；外部IO替身不证明声音。SCP-cv 2026-10-11。 */
import assert from 'node:assert/strict';
import test, { before, after, afterEach } from 'node:test';
import { setImmediate as nextTurn } from 'node:timers/promises';
import { createDomTestServer } from './dom-test-server.mjs';

let server, Publisher;
const owners = [];
const commandId = 'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa';
before(async () => {
  server = await createDomTestServer();
  ({ ScreenSharePublisher: Publisher } = await server.ssrLoadModule('/src/features/screen-share/ScreenSharePublisher.ts'));
});
afterEach(async () => { await Promise.all(owners.splice(0).map((owner) => owner.dispose())); });
after(async () => { await server?.close(); });

/** :returns: 测试独占的外部响应，可模拟无视AbortSignal的迟到IO。 */
function deferred() { let resolve, reject; const promise = new Promise((yes, no) => { resolve = yes; reject = no; }); return { promise, resolve, reject }; }
/** :param predicate: 实际条件。:returns: 不依靠固定睡眠的有界等待。 */
async function until(predicate) {
  const deadline = Date.now() + 3000;
  while (!predicate()) { assert.ok(Date.now() < deadline, '等待条件未达到'); await nextTurn(); }
}
/** :param windowId: 独立目标。:param hasAudio: 是否实际存在内容音轨。:returns: 正式Publisher与不含媒体的IO夹具。 */
function fixture(windowId = 1, hasAudio = true) {
  const calls = [];
  const tracks = (hasAudio ? ['video', 'audio'] : ['video']).map((kind) => Object.assign(new EventTarget(), {
    kind, readyState: 'live', stop() { this.readyState = 'ended'; },
  }));
  const media = { getTracks: () => tracks, getVideoTracks: () => tracks.filter((t) => t.kind === 'video'), getAudioTracks: () => tracks.filter((t) => t.kind === 'audio') };
  let current = true;
  let snapshot = { id: `${windowId}${'1'.repeat(7)}-1111-4111-8111-111111111111`, window_id: windowId,
    state: 'allocated', has_audio: hasAudio, receiver_has_audio: hasAudio, source_generation: null, actual_frames: 0,
    expires_at: new Date(Date.now() + 20000).toISOString(), last_frame_at: null, answer: null, stop_reason: '', actual_volume: null, actual_muted: null };
  const api = {
    async create() { return { ...snapshot }; },
    async offer() {
      snapshot = { ...snapshot, state: 'receiving', source_generation: 8, actual_frames: 10,
        actual_volume: 100, actual_muted: true, answer: { type: 'answer', sdp: 'v=0\r\n' } }; return { ...snapshot };
    },
    async get() { calls.push('get'); return { ...snapshot }; },
    async heartbeat() { return { ...snapshot }; },
    async stop() { calls.push('stop'); snapshot = { ...snapshot, state: 'closed' }; return { ...snapshot }; },
    async setAudio(id, volume, muted, signal) {
      calls.push({ id, volume, muted, signal }); snapshot = { ...snapshot, actual_volume: volume, actual_muted: muted };
      return receipt();
    },
    async getAudioCommand(window, id) { calls.push({ command: id, window }); return command(); },
  };
  const owner = new Publisher({ windowId: () => windowId, availability: () => '', isCurrent: () => current,
    capture: async () => media, peer: () => ({ offer: async () => ({ type: 'offer', sdp: 'v=0\r\n' }), answer: async () => {}, close() {} }),
    api, changed() {} }); owners.push(owner);
  const receipt = (patch = {}) => ({ submitted_command_id: commandId, window_id: windowId, share_id: snapshot.id, source_generation: 8, ...patch });
  const command = (patch = {}) => ({ command_id: commandId, window_id: windowId, command: 'SCREEN_SHARE_AUDIO', source_generation: 8,
    status: 'completed', result_code: 'ok', completed_at: '2026-10-11T00:00:00Z', ...patch });
  return { owner, api, calls, tracks, receipt, command, snapshot: () => ({ ...snapshot }), patch: (value) => { snapshot = { ...snapshot, ...value }; }, loseContext: () => { current = false; } };
}
/** :param value: 独占夹具。:returns: 已有实际后帧、与音频受理无关的分享。 */
async function sharing(value) { await value.owner.start(true); await until(() => value.owner.view.phase === 'sharing'); }

test('202只有等待；精确命令和实际属性都匹配后才确认', async () => {
  const value = fixture(); await sharing(value);
  const pending = deferred();
  value.api.setAudio = async (id, volume, muted, signal) => { value.calls.push({ id, volume, muted, signal }); return value.receipt(); };
  value.api.getAudioCommand = async () => { value.calls.push('query'); return pending.promise; };
  const applying = value.owner.setAudio(29, false); await until(() => value.calls.includes('query'));
  assert.equal(value.owner.view.audio.phase, 'pending');
  assert.equal(value.owner.view.snapshot.actual_volume, 100); assert.equal(value.owner.view.snapshot.actual_muted, true);
  value.patch({ actual_volume: 29, actual_muted: false }); pending.resolve(value.command()); await applying;
  assert.equal(value.owner.view.audio.phase, 'confirmed');
  assert.equal(value.owner.view.snapshot.actual_volume, 29); assert.equal(value.owner.view.snapshot.actual_muted, false);
  assert.equal(value.owner.view.phase, 'sharing');
});

test('completed/ok但实际值未知或不匹配，仍不得报成功', async (t) => {
  t.mock.timers.enable({ apis: ['setTimeout'] });
  const value = fixture(); await sharing(value);
  value.api.setAudio = async () => value.receipt();
  value.patch({ actual_volume: null, actual_muted: null });
  const applying = value.owner.setAudio(29, false); await until(() => value.calls.some((c) => c.command));
  await nextTurn(); assert.equal(value.owner.view.audio.phase, 'pending');
  t.mock.timers.tick(8000); await applying;
  assert.equal(value.owner.view.audio.phase, 'unconfirmed'); assert.equal(value.owner.view.phase, 'sharing');
});

test('音频请求不返回也在八秒内结束，不重发PATCH、不停止视频', async (t) => {
  t.mock.timers.enable({ apis: ['setTimeout'] });
  const value = fixture(); await sharing(value); const pending = deferred();
  value.api.setAudio = async () => { value.calls.push('audio'); return pending.promise; };
  const applying = value.owner.setAudio(40, true); t.mock.timers.tick(8000); await applying;
  assert.equal(value.owner.view.audio.phase, 'unconfirmed'); assert.equal(value.owner.view.captureActive, true);
  assert.equal(value.calls.filter((c) => c === 'audio').length, 1);
  pending.resolve(value.receipt()); await nextTurn(); assert.equal(value.owner.view.audio.phase, 'unconfirmed');
});

test('处理中重复点击只发一次控制', async () => {
  const value = fixture(); await sharing(value); const pending = deferred();
  value.api.getAudioCommand = async () => pending.promise;
  const applying = value.owner.setAudio(40, true); await nextTurn(); await value.owner.setAudio(50, false);
  assert.equal(value.calls.filter((c) => c.id).length, 1); pending.resolve(value.command()); await applying;
});

for (const patch of [{ window_id: 2 }, { share_id: 'ffffffff-ffff-4fff-8fff-ffffffffffff' }, { source_generation: 9 }, { submitted_command_id: '' }]) {
  test(`错误提交身份${JSON.stringify(patch)}不查询、不污染分享`, async () => {
    const value = fixture(); await sharing(value); value.api.setAudio = async () => value.receipt(patch);
    await value.owner.setAudio(30, true); assert.equal(value.owner.view.audio.phase, 'failed');
    assert.equal(value.calls.filter((c) => c.command).length, 0); assert.equal(value.owner.view.phase, 'sharing');
  });
}
for (const patch of [{ command_id: 'ffffffff-ffff-4fff-8fff-ffffffffffff' }, { window_id: 2 }, { source_generation: 9 },
  { command: 'SET_VOLUME' }, { completed_at: null }, { completed_at: 'not-a-date' }, { status: 'unknown' }]) {
  test(`错误命令回读${JSON.stringify(patch)}不假报成功`, async () => {
    const value = fixture(); await sharing(value); value.api.getAudioCommand = async () => value.command(patch);
    await value.owner.setAudio(30, true); assert.equal(value.owner.view.audio.phase, 'failed');
    assert.equal(value.owner.view.phase, 'sharing');
  });
}
for (const status of ['failed', 'superseded', 'uncertain']) {
  test(`命令终态${status}不报实际应用成功，不自动重试`, async () => {
    const value = fixture(); await sharing(value); value.api.getAudioCommand = async () => value.command({ status, result_code: 'rejected' });
    await value.owner.setAudio(30, true);
    assert.ok(['failed', 'unconfirmed'].includes(value.owner.view.audio.phase)); assert.equal(value.calls.filter((c) => c.id).length, 1);
  });
}

test('无本地/接收音轨不下发控制', async () => {
  const a = fixture(1, false); await sharing(a); await a.owner.setAudio(30, false); assert.equal(a.calls.filter((c) => c.id).length, 0);
  const b = fixture(2); b.patch({ receiver_has_audio: false }); await sharing(b); await b.owner.setAudio(40, true);
  assert.equal(b.calls.filter((c) => c.id).length, 0);
});
test('未收到后帧及连接阶段不能下发音频控制', async () => {
  const value = fixture(); value.api.offer = async () => ({ ...value.snapshot(), state: 'negotiating', source_generation: 8 });
  await value.owner.start(true); await value.owner.setAudio(30, false); assert.equal(value.calls.filter((c) => c.id).length, 0);
});
for (const volume of [-1, 101, 29.5, Number.NaN, '30']) {
  test(`非法音量${String(volume)}零副作用`, async () => {
    const value = fixture(); await sharing(value); await value.owner.setAudio(volume, false);
    assert.equal(value.calls.filter((c) => c.id).length, 0); assert.equal(value.owner.view.audio.phase, 'failed');
  });
}
for (const ending of ['stop', 'dispose', 'context']) {
  test(`${ending}立即取消音频寿命，迟到受理不能更新已结束视图`, async () => {
    const value = fixture(); await sharing(value); const pending = deferred(); let signal;
    value.api.setAudio = async (_id, _volume, _muted, received) => { signal = received; return pending.promise; };
    const applying = value.owner.setAudio(40, false); await until(() => Boolean(signal));
    if (ending === 'context') { value.loseContext(); await value.owner.dispose(); } else await value.owner[ending]();
    assert.equal(signal.aborted, true); await applying;
    const phase = value.owner.view.phase; pending.resolve(value.receipt()); await nextTurn();
    assert.equal(value.owner.view.phase, phase); assert.equal(value.owner.view.audio.phase, 'idle');
    assert.ok(value.tracks.every((track) => track.readyState === 'ended'));
  });
}

test('两个目标音频隔离，控制和停止一窗不改另一窗', async () => {
  const a = fixture(1), b = fixture(2); await Promise.all([sharing(a), sharing(b)]);
  await a.owner.setAudio(29, false); assert.equal(b.owner.view.snapshot.actual_volume, 100); assert.equal(b.owner.view.snapshot.actual_muted, true);
  assert.equal(b.calls.filter((c) => c.id).length, 0); await a.owner.stop(); assert.equal(b.owner.view.phase, 'sharing');
});

test('普通GET迟到不能覆盖音频控制后的新实际属性', async (t) => {
  t.mock.timers.enable({ apis: ['setTimeout'] });
  const value = fixture(); await sharing(value); const older = value.snapshot(), pending = deferred(); let reads = 0;
  value.api.get = async () => { if (++reads === 1) return pending.promise; return value.snapshot(); };
  t.mock.timers.tick(1000); await until(() => reads === 1);
  await value.owner.setAudio(29, false); assert.equal(value.owner.view.audio.phase, 'confirmed');
  pending.resolve(older); await nextTurn(); await nextTurn();
  assert.equal(value.owner.view.snapshot.actual_volume, 29); assert.equal(value.owner.view.snapshot.actual_muted, false);
});
test('较早普通GET迟到失败不能结束刚确认音频的有效视频', async (t) => {
  t.mock.timers.enable({ apis: ['setTimeout'] });
  const value = fixture(); await sharing(value); const pending = deferred(); let reads = 0;
  value.api.get = async () => { if (++reads === 1) return pending.promise; return value.snapshot(); };
  t.mock.timers.tick(1000); await until(() => reads === 1); await value.owner.setAudio(29, false);
  pending.reject(new Error('private stale network failure')); await nextTurn(); await nextTurn();
  assert.equal(value.owner.view.phase, 'sharing'); assert.equal(value.owner.view.audio.phase, 'confirmed');
  assert.equal(value.owner.view.snapshot.actual_volume, 29); assert.equal(value.owner.view.captureActive, true);
});
