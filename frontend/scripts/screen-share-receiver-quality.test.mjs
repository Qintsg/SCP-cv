/* 接收统计只能作为可选诊断，永远不能阻塞实际帧报告或复活已停止资源。SCP-cv 2026-10-12。 */
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import test from 'node:test';
import { setImmediate as nextTurn } from 'node:timers/promises';
import { runInNewContext } from 'node:vm';

const receiver = readFileSync(new URL('../../runtime-dotnet/src/ScpCv.ControlHost/ScreenSharing/receiver.js', import.meta.url), 'utf8');

/** :returns: 只模拟外部统计与帧事件，执行完整生产接收脚本，不调用真实网络。 */
async function fixture() {
  let resolveStats;
  const pending = new Promise(resolve => { resolveStats = resolve; });
  let peer, callback, clock = 1000, statistics = 0;
  const requests = [], messages = [];
  class FakeStream {
    tracks = [];
    addTrack(track) { this.tracks.push(track); }
    getTracks() { return this.tracks; }
    getAudioTracks() { return []; }
  }
  class FakePeer extends EventTarget {
    iceGatheringState = 'complete';
    localDescription = { type: 'answer', sdp: 'v=0\r\n' };
    constructor() { super(); peer = this; }
    async setRemoteDescription() {}
    async setLocalDescription() {}
    async createAnswer() { return this.localDescription; }
    getStats() { statistics++; return pending; }
    close() { this.connectionState = 'closed'; }
  }
  const video = {
    videoWidth: 1920, videoHeight: 1080,
    requestVideoFrameCallback(value) { callback = value; return 1; },
    cancelVideoFrameCallback() { callback = undefined; },
    play: async () => {}, pause() {},
  };
  const target = new EventTarget();
  target.chrome = { webview: { postMessage(value) { messages.push(value); } } };
  runInNewContext(receiver, {
    URLSearchParams, AbortController, EventTarget, MediaStream: FakeStream, RTCPeerConnection: FakePeer,
    window: target, document: { querySelector: () => video }, performance: { now: () => clock },
    location: { pathname: '/screen-share/11111111-1111-4111-8111-111111111111/',
      search: '?window_id=1&source_generation=7', hash: '#' + 'a'.repeat(64) }, history: { replaceState() {} },
    setTimeout, clearTimeout, setInterval: () => 1, clearInterval() {},
    fetch: async (url, options) => {
      requests.push({ url, body: options.body ? JSON.parse(options.body) : null });
      return { ok: true, json: async () => ({ type: 'offer', sdp: 'v=0\r\n' }) };
    },
  });
  await nextTurn();
  assert.ok(messages.some(message => message.type === 'ready'));
  const track = Object.assign(new EventTarget(), { kind: 'video', readyState: 'live', stop() { this.readyState = 'ended'; } });
  const event = new Event('track'); event.track = track; peer.dispatchEvent(event);
  assert.equal(target.scpScreenShare.activate(100, true).accepted, true);
  return {
    frames: () => requests.filter(request => request.url.includes('frames/')),
    statistics: () => statistics, resolveStats, messages,
    async frame() { clock += 1001; callback(clock, { mediaTime: clock / 1000 }); await nextTurn(); },
    stop: () => target.scpScreenShare.stop(),
  };
}

test('接收统计无响应仍报告实际激活帧，统计不重叠且停止后不再发送', async () => {
  const current = await fixture();
  try {
    await current.frame();
    assert.equal(current.frames().length, 1, '可选getStats不能阻塞实际后帧');
    await current.frame();
    assert.equal(current.frames().length, 2);
    assert.equal(current.statistics(), 1, '统计悬挂时不堆积新统计调用');
    current.stop();
    current.resolveStats(new Map()); await nextTurn();
    assert.equal(current.frames().length, 2, '迟到统计不能触发已停止资源的帧写入');
  } finally { current.stop(); }
});

test('迟到统计只能供下一真实帧诊断，不自行授予新帧或理论码率', async () => {
  const current = await fixture();
  try {
    await current.frame();
    assert.equal(current.frames().length, 1);
    assert.equal(current.frames()[0].body.quality.fps, null);
    current.resolveStats(new Map([['inbound', { id: 'inbound', type: 'inbound-rtp', kind: 'video',
      framesPerSecond: 29, bytesReceived: 1000, timestamp: 100 }]]));
    await nextTurn();
    assert.equal(current.frames().length, 1, '统计完成不制造视频帧');
    await current.frame();
    assert.equal(current.frames()[1].body.quality.fps, 29);
    assert.equal(current.frames()[1].body.quality.bitrate_mbps, null, '首样本没有可计算差分');
  } finally { current.stop(); }
});
