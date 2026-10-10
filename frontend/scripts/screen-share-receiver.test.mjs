/** 屏幕共享接收脚本生命周期回归；模拟浏览器边界，不计作真实采集或原生出画。SCP-cv 2026-10-10。 */
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { setImmediate as nextTurn } from 'node:timers/promises';
import { runInNewContext } from 'node:vm';
import test from 'node:test';

const script = readFileSync(new URL('../../runtime-dotnet/src/ScpCv.ControlHost/ScreenSharing/receiver.js', import.meta.url), 'utf8');

/** :param options: 当前夹具的ICE阶段与目标；:returns: 完全独立的脚本资源，不开启浏览器或网络。 */
function receiver({ gathering = false, windowId = 1 } = {}) {
  const messages = [];
  const requests = [];
  const timers = new Map();
  const callbacks = new Map();
  let nextId = 1;
  let peer;
  let address;
  const video = {
    srcObject: null, muted: true, volume: 1,
    pause() {},
    play: async () => {},
    requestVideoFrameCallback(callback) { const id = nextId++; callbacks.set(id, callback); return id; },
    cancelVideoFrameCallback(id) { callbacks.delete(id); },
  };
  class Peer {
    constructor() { peer = this; this.listeners = new Map(); this.iceGatheringState = gathering ? 'gathering' : 'complete'; }
    addEventListener(name, listener) { this.listeners.set(name, listener); }
    removeEventListener(name) { this.listeners.delete(name); }
    setRemoteDescription = async () => {};
    createAnswer = async () => ({ type: 'answer', sdp: 'v=0\r\n' });
    setLocalDescription = async description => { this.localDescription = description; };
    close() { this.connectionState = 'closed'; }
  }
  class Stream {
    tracks = [];
    addTrack(track) { this.tracks.push(track); }
    getTracks() { return this.tracks; }
    getAudioTracks() { return this.tracks.filter(track => track.kind === 'audio'); }
  }
  const window = { chrome: { webview: { postMessage: message => messages.push(message) } }, addEventListener() {} };
  runInNewContext(script, {
    window, document: { querySelector: () => video },
    location: { pathname: '/api/screen-share/receiver/01000000-0000-0000-0000-000000000001/', search: `?window_id=${windowId}&source_generation=7`, hash: '#' + 'a'.repeat(64) },
    history: { replaceState(_state, _title, value) { address = value; } },
    URLSearchParams, RTCPeerConnection: Peer, MediaStream: Stream, AbortController,
    performance: { now: () => 5000 },
    setTimeout: (callback, delay) => { const id = nextId++; timers.set(id, { callback, delay }); return id; },
    clearTimeout: id => timers.delete(id),
    setInterval: (callback, delay) => { const id = nextId++; timers.set(id, { callback, delay }); return id; },
    clearInterval: id => timers.delete(id),
    fetch: async (url, options) => {
      requests.push({ url, options });
      return { ok: true, json: async () => ({ type: 'offer', sdp: 'v=0\r\n' }) };
    },
  });
  return {
    window, video, peer, messages, requests, timers, callbacks, address,
    /** :param kind: 当前模拟接收轨道类型；:returns: 可核实际释放的轨道对象。 */
    track(kind = 'video') {
      const track = { kind, readyState: 'live', addEventListener() {}, stop() { this.readyState = 'ended'; } };
      peer.listeners.get('track')({ track });
      return track;
    },
    /** :param time: 当前模拟媒体时间；:returns: 只触发真实回调边界，不伪造主机接收状态。 */
    frame(time) {
      const [id, callback] = callbacks.entries().next().value;
      callbacks.delete(id);
      callback(0, { mediaTime: time });
    },
  };
}

test('连接应答不算实际帧，私有cap不进入请求查询串或原生消息', async () => {
  const resource = receiver();
  await nextTurn();
  assert.ok(resource.messages.some(message => message.type === 'ready'));
  assert.ok(!resource.messages.some(message => message.type === 'frame'));
  assert.ok(!resource.address.includes('#'));
  assert.ok(resource.requests.every(request => !request.url.includes('token') && !request.url.includes('#')));
  assert.ok(!JSON.stringify(resource.messages).includes('a'.repeat(64)));
  resource.window.scpScreenShare.stop();
});

test('预备帧不发实际接收报告，激活后的后帧才上报', async () => {
  const resource = receiver();
  await nextTurn();
  resource.track();
  resource.frame(1);
  await nextTurn();
  assert.equal(resource.messages.find(message => message.type === 'frame').activated, false);
  assert.ok(!resource.requests.some(request => request.url.includes('/frames/')));
  assert.equal(resource.window.scpScreenShare.activate(50, false).accepted, true);
  resource.frame(2);
  await nextTurn();
  const report = resource.requests.find(request => request.url.includes('/frames/'));
  assert.equal(JSON.parse(report.options.body).activated, true);
  assert.equal(JSON.parse(report.options.body).frames, 2);
  assert.equal(resource.video.muted, true); // 未返回内容音轨时不能编造音频能力。
  resource.window.scpScreenShare.stop();
});

test('停止释放轨道、peer、视频、回调及轮询，重复停止幂等', async () => {
  const resource = receiver();
  await nextTurn();
  const track = resource.track();
  resource.frame(1);
  resource.window.scpScreenShare.stop();
  resource.window.scpScreenShare.stop();
  await nextTurn();
  assert.equal(track.readyState, 'ended');
  assert.equal(resource.peer.connectionState, 'closed');
  assert.equal(resource.video.srcObject, null);
  assert.equal(resource.callbacks.size, 0);
  assert.equal(resource.timers.size, 0);
  assert.equal(resource.messages.filter(message => message.type === 'closed').length, 1);
});

test('ICE未完成时停止也取消有界等待和监听，不遗留后台协商', async () => {
  const resource = receiver({ gathering: true });
  await nextTurn();
  assert.equal(resource.peer.listeners.has('icegatheringstatechange'), true);
  resource.window.scpScreenShare.stop();
  await nextTurn();
  assert.equal(resource.timers.size, 0);
  assert.equal(resource.peer.listeners.has('icegatheringstatechange'), false);
  assert.ok(!resource.requests.some(request => request.url.includes('/answer/')));
});

test('两套资源独立，停止窗口1不停止窗口2的轨道或后帧', async () => {
  const first = receiver();
  const second = receiver({ windowId: 2 });
  await nextTurn();
  first.track();
  const kept = second.track();
  first.window.scpScreenShare.stop();
  second.frame(1);
  assert.equal(kept.readyState, 'live');
  assert.notEqual(second.peer.connectionState, 'closed');
  assert.equal(second.messages.find(message => message.type === 'frame').window_id, 2);
  second.window.scpScreenShare.stop();
});
