/* 网页共享授权顺序、两目标及资源回收回归；IO替身不计作真实采集。SCP-cv 2026-10-11。 */
import assert from 'node:assert/strict';
import test, { before, after, afterEach } from 'node:test';
import { setImmediate as nextTurn } from 'node:timers/promises';
import { createDomTestServer } from './dom-test-server.mjs';

let server, Publisher, availability;
const owners = [];
before(async () => {
  server = await createDomTestServer();
  ({ ScreenSharePublisher: Publisher } = await server.ssrLoadModule('/src/features/screen-share/ScreenSharePublisher.ts'));
  ({ screenShareAvailability: availability } = await server.ssrLoadModule('/src/features/screen-share/availability.ts'));
});
afterEach(async () => { await Promise.all(owners.splice(0).map((owner) => owner.dispose())); });
after(async () => { await server?.close(); });

/** :returns: 可显式释放或模拟浏览器ended的自有轨道替身。 */
function track(kind = 'video') {
  const value = new EventTarget();
  Object.assign(value, { kind, readyState: 'live', stops: 0, stop() { this.stops++; this.readyState = 'ended'; } });
  return value;
}
/** :param tracks: 自有媒体轨道。:returns: 不含实际屏幕内容的MediaStream接口替身。 */
function stream(tracks = [track()]) {
  return { getTracks: () => tracks, getVideoTracks: () => tracks.filter((t) => t.kind === 'video'), getAudioTracks: () => tracks.filter((t) => t.kind === 'audio') };
}
/** :returns: 由用例控制完成时机的异步结果。 */
function deferred() { let resolve, reject; const promise = new Promise((yes, no) => { resolve = yes; reject = no; }); return { promise, resolve, reject }; }
/** :param window: 目标。:param overrides: 外部IO行为。:returns: 正式发布者和可审查边界调用。 */
function fixture(window = 1, overrides = {}) {
  const calls = [], media = stream(), peer = {
    closes: 0,
    async offer() { calls.push('peer.offer'); return { type: 'offer', sdp: 'v=0\r\n' }; },
    async answer(value) { calls.push('peer.answer'); assert.equal(value.type, 'answer'); },
    close() { this.closes++; },
  };
  let snapshot = { id: window === 1 ? '11111111-1111-4111-8111-111111111111' : '22222222-2222-4222-8222-222222222222', window_id: window,
    state: 'allocated', has_audio: false, receiver_has_audio: false, source_generation: null, actual_frames: 0, last_frame_at: null,
    expires_at: new Date(Date.now() + 20000).toISOString(), answer: null, stop_reason: '' };
  const api = {
    async create(target, hasAudio) { calls.push(['create', target, hasAudio]); return { ...snapshot }; },
    async offer(id) { calls.push(['offer', id]); snapshot = { ...snapshot, state: 'negotiating', source_generation: 7 }; return { ...snapshot }; },
    async get(id) { calls.push(['get', id]); return { ...snapshot }; },
    async heartbeat(id) { calls.push(['heartbeat', id]); return { ...snapshot }; },
    async stop(id) { calls.push(['stop', id]); snapshot = { ...snapshot, state: 'stopping' }; return { ...snapshot }; },
    ...overrides.api,
  };
  const deps = { windowId: () => window, availability: () => '', isCurrent: () => true,
    capture: (options) => { calls.push(['capture', options]); return Promise.resolve(media); },
    peer: () => peer, api, changed: () => {}, ...overrides, api };
  const owner = new Publisher(deps); owners.push(owner);
  return { owner, calls, media, peer, api, getSnapshot: () => ({ ...snapshot }), setSnapshot: (patch) => { snapshot = { ...snapshot, ...patch }; } };
}
/** :param predicate: 可观测的异步条件。:returns: 仅等待实际条件，不使用任意固定睡眠。 */
async function until(predicate) {
  const deadline = Date.now() + 3000;
  while (!predicate()) { assert.ok(Date.now() < deadline, '异步边界未到达'); await nextTurn(); }
}

test('点击同步发起原生许可；选择器未完成时零API/OPEN', async () => {
  const capture = deferred(); let invoked = 0;
  const { owner, calls, media } = fixture(1, { capture: () => { invoked++; return capture.promise; } });
  const starting = owner.start(false);
  assert.equal(invoked, 1);
  assert.equal(owner.view.phase, 'selecting');
  assert.deepEqual(calls, []);
  await owner.stop(); capture.resolve(media); await starting;
  assert.ok(media.getTracks().every((t) => t.readyState === 'ended'));
  assert.deepEqual(calls, [], '选择器迟到确认不能创建或切换播放器');
});
for (const errorName of ['NotAllowedError', 'AbortError', 'NotReadableError']) {
  test(`原生许可${errorName}零API并明确反馈`, async () => {
    const { owner, calls } = fixture(1, { capture: () => Promise.reject(Object.assign(new Error('private browser detail'), { name: errorName })) });
    await owner.start(false);
    assert.equal(calls.length, 0);
    assert.notEqual(owner.view.phase, 'sharing');
    assert.ok(owner.view.errorCode);
    assert.doesNotMatch(owner.view.errorCode, /private/);
  });
}
for (const windowId of [1, 2]) {
  test(`窗口${windowId}预约/offer不是实际出画，停止先释放本地且保持待确认`, async () => {
    const { owner, calls, media, peer, api, setSnapshot } = fixture(windowId);
    await owner.start(true);
    assert.deepEqual(calls.find((c) => Array.isArray(c) && c[0] === 'create'), ['create', windowId, false]);
    assert.equal(owner.view.phase, 'connecting');
    assert.equal(owner.view.localHasAudio, false, '请求音频不等于浏览器返回音轨');
    setSnapshot({ answer: { type: 'answer', sdp: 'v=0\r\n' }, state: 'receiving', actual_frames: 12 });
    await until(() => owner.view.phase === 'sharing');
    assert.equal(calls.filter((c) => c === 'peer.answer').length, 1);
    const pending = deferred();
    api.stop = async (id) => { calls.push(['stop', id]); return pending.promise; };
    const closing = owner.stop();
    assert.ok(media.getTracks().every((t) => t.readyState === 'ended'));
    assert.equal(peer.closes, 1);
    assert.equal(owner.view.phase, 'stopping');
    pending.resolve({ ...owner.view.snapshot, state: 'stopping' }); await closing;
    await owner.stop();
    assert.equal(calls.filter((c) => Array.isArray(c) && c[0] === 'stop').length, 1, '重复停止不重发副作用');
    assert.equal(owner.view.phase, 'stopping', '受理停止不能冒充原生关闭');
    await owner.start(false);
    assert.equal(calls.filter((c) => Array.isArray(c) && c[0] === 'create').length, 1, '未确认释放不能重占目标');
  });
}
test('空视频拒绝；服务失败释放全部采集，不造永久源', async () => {
  const audio = track('audio');
  const empty = fixture(1, { capture: () => Promise.resolve(stream([audio])) });
  await empty.owner.start(true);
  assert.equal(audio.readyState, 'ended'); assert.equal(empty.calls.length, 0);
  const rejected = fixture(2, { api: { create: async () => { throw new Error('offline'); } } });
  await rejected.owner.start(false);
  assert.ok(rejected.media.getTracks().every((t) => t.readyState === 'ended'));
  assert.equal(rejected.owner.view.phase, 'failed');
});
test('两个独立目标分别持有轨道和peer，停止一窗不影响另一窗', async () => {
  const a = fixture(1), b = fixture(2);
  await Promise.all([a.owner.start(false), b.owner.start(false)]);
  await a.owner.stop();
  assert.equal(a.peer.closes, 1); assert.equal(b.peer.closes, 0);
  assert.equal(b.media.getVideoTracks()[0].readyState, 'live');
});
test('浏览器停止分享走相同有界停止，不自动重新采集', async () => {
  const { owner, media, calls, peer } = fixture();
  await owner.start(false);
  media.getVideoTracks()[0].readyState = 'ended';
  media.getVideoTracks()[0].dispatchEvent(new Event('ended'));
  await until(() => calls.some((c) => Array.isArray(c) && c[0] === 'stop'));
  assert.equal(peer.closes, 1);
  assert.equal(calls.filter((c) => Array.isArray(c) && c[0] === 'capture').length, 1);
});
test('主机/账号已换时释放轨道，不向新主机发送旧分享停止', async () => {
  let current = true;
  const { owner, calls, media } = fixture(1, { isCurrent: () => current });
  await owner.start(false); current = false; await owner.stop();
  assert.ok(media.getTracks().every((t) => t.readyState === 'ended'));
  assert.equal(calls.filter((c) => Array.isArray(c) && c[0] === 'stop').length, 0);
});
test('安全上下文、平台、浏览器及离线能力有明确原因', () => {
  const base = { secure: true, web: true, capture: true, peer: true, authenticated: true, online: true };
  assert.equal(availability(base), '');
  for (const [field, code] of [['secure', 'share_https_required'], ['web', 'share_browser_unsupported'], ['capture', 'share_browser_unsupported'],
    ['peer', 'share_browser_unsupported'], ['authenticated', 'share_login_required'], ['online', 'share_player_offline']]) {
    assert.equal(availability({ ...base, [field]: false }), code);
  }
});
test('不支持/离线点击不会采集或请求API', async () => {
  const { owner, calls } = fixture(1, { availability: () => 'share_player_offline' });
  await owner.start(false); assert.equal(calls.length, 0); assert.equal(owner.view.errorCode, 'share_player_offline');
});
test('服务端明确争抢拒绝不被泛化成网络错误，也不会泄漏未知错误码', async () => {
  const busy = fixture(1, { api: { create: async () => { throw Object.assign(new Error('opaque detail'), { code: 'share_window_busy' }); } } });
  await busy.owner.start(false); assert.equal(busy.owner.view.errorCode, 'share_window_busy');
  const unknown = fixture(1, { api: { create: async () => { throw new Error('share_secret_value'); } } });
  await unknown.owner.start(false); assert.equal(unknown.owner.view.errorCode, 'share_connection_failed');
});
test('停止后迟到的预约只撤销原ID，不发offer或重新取得采集', async () => {
  const allocation = deferred(); let requested = false;
  const { owner, calls, getSnapshot, media } = fixture(1, { api: { create: () => { requested = true; return allocation.promise; } } });
  const starting = owner.start(false); await until(() => requested);
  await owner.stop(); allocation.resolve(getSnapshot()); await starting;
  assert.equal(calls.filter((c) => Array.isArray(c) && c[0] === 'stop').length, 1);
  assert.equal(calls.filter((c) => Array.isArray(c) && c[0] === 'offer').length, 0);
  assert.ok(media.getTracks().every((t) => t.readyState === 'ended'));
});
