/*
 * 背景音乐真实页面/Pinia/HTTP回归：外部延迟响应不能覆盖新观测，不连接现场播放器。
 * @Project : SCP-cv
 * @File : background-audio-observation.test.mjs
 * @Author : Qintsg
 * @Date : 2026-10-04
 */
import assert from 'node:assert/strict';
import { setImmediate as nextTurn } from 'node:timers/promises';
import test, { before, after, afterEach } from 'node:test';
import { Window } from 'happy-dom';
import { createDomTestServer } from './dom-test-server.mjs';

const browser = new Window({ url: 'http://localhost:5173/background-audio' });
const names = ['window', 'document', 'navigator', 'history', 'localStorage', 'HTMLElement', 'HTMLInputElement',
  'HTMLTextAreaElement', 'Element', 'Node', 'SVGElement', 'Document', 'MutationObserver', 'ResizeObserver',
  'Image', 'InputEvent', 'getComputedStyle', 'requestAnimationFrame', 'cancelAnimationFrame'];
const prior = new Map(names.map(name => [name, Object.getOwnPropertyDescriptor(globalThis, name)]));
for (const name of names) Object.defineProperty(globalThis, name, { configurable: true, writable: true,
  value: typeof browser[name] === 'function' && /^[a-z]/.test(name) ? browser[name].bind(browser) : browser[name] });
const previousFetch = globalThis.fetch;
let server, vue, createPinia, useAudioStore, useRuntimeStore, useToastStore, apiModule, AudioView, i18n, app;

before(async () => {
  [vue, { createPinia }, server] = await Promise.all([import('vue'), import('pinia'), createDomTestServer()]);
  ({ useBackgroundAudioStore: useAudioStore } = await server.ssrLoadModule('/src/stores/backgroundAudio.ts'));
  ({ useRuntimeStore } = await server.ssrLoadModule('/src/stores/runtime.ts'));
  ({ useToastStore } = await server.ssrLoadModule('/src/composables/useToast.ts'));
  apiModule = await server.ssrLoadModule('/src/services/api.ts');
  ({ default: AudioView } = await server.ssrLoadModule('/src/features/backgroundAudio/BackgroundAudioView.vue'));
  ({ i18n } = await server.ssrLoadModule('/src/locales/index.ts'));
});
afterEach(async () => {
  useToastStore?.().clear();
  app?.unmount(); app = undefined; browser.document.body.replaceChildren();
  await browser.happyDOM.abort(); globalThis.fetch = previousFetch;
});
after(async () => {
  await server?.close(); await browser.happyDOM.close();
  for (const [name, descriptor] of prior) {
    if (descriptor) Object.defineProperty(globalThis, name, descriptor); else delete globalThis[name];
  }
});

/** :param id: 外部源ID。:returns: 不带实体音频的源元数据。 */
function source(id) { return { id, name: `曲目${id}`, uri: `/media/${id}.mp3`, source_type: 'audio',
  is_available: true, metadata: {}, preview_kind: 'icon', preview_url: '', thumbnail_url: '' }; }

/** :param id: 当前源。:param second: 服务端单调更新时间。:returns: 完整HTTP/SSE快照。 */
function snapshot(id, second) {
  return { state: { id: 1, source_id: id, source_name: `曲目${id}`, source_uri: `/media/${id}.mp3`, source: source(id),
    current_item_id: id, playback_state: 'playing', playback_state_label: '播放中', error_message: '',
    position_ms: 1000, duration_ms: 60000, volume: 70, is_muted: true, loop_enabled: false, pending_command: '',
    updated_at: `2026-10-04T00:00:${String(second).padStart(2, '0')}Z` },
  playlist: [1, 2].map(id => ({ id, source_id: id, source_name: `曲目${id}`, source: source(id), sort_order: id,
    created_at: '2026-10-04T00:00:00Z' })) };
}

/** :param value: 外部HTTP数据。:returns: 真实JSON Response。 */
function response(value) { return new Response(JSON.stringify(value), { headers: { 'Content-Type': 'application/json' } }); }

/** :returns: 当前Vue/HTTP微任务完成，不用固定睡眠。 */
async function flush() { await vue.nextTick(); await nextTurn(); await vue.nextTick(); }

/** :returns: 从实际HTTP初始化并挂载公开音频页面的store。 */
async function mountAudio() {
  globalThis.fetch = async (url) => {
    const path = new URL(String(url), browser.location.origin).pathname;
    if (path === '/api/background-audio/') return response({ success: true, background_audio: snapshot(1, 1) });
    if (path === '/api/sources/') return response({ success: true, sources: [source(1), source(2)] });
    if (path === '/api/folders/') return response({ success: true, folders: [] });
    throw new Error(`未提供测试HTTP响应：${path}`);
  };
  const pinia = createPinia(); const audio = useAudioStore(pinia);
  app = vue.createApp(AudioView); app.use(pinia).use(i18n);
  const root = browser.document.createElement('div'); browser.document.body.append(root); app.mount(root);
  await flush(); await flush();
  return audio;
}

test('音频播放仅被受理时提示请求已提交，不冒称已经播放', async () => {
  const audio = await mountAudio();
  const fallback = globalThis.fetch; const writes = [];
  globalThis.fetch = async (url, options = {}) => {
    const path = new URL(String(url), browser.location.origin).pathname;
    if (path !== '/api/background-audio/play-source/') return fallback(url, options);
    writes.push(JSON.parse(options.body));
    const pending = snapshot(2, 2);
    Object.assign(pending.state, { playback_state: 'loading', pending_command: 'OPEN', position_ms: 0, duration_ms: 0 });
    return response({ success: true, background_audio: pending });
  };
  const row = [...browser.document.querySelectorAll('.background-audio__source-item')]
    .find(item => item.textContent.includes('曲目2'));
  [...row.querySelectorAll('button')].find(button => button.textContent.includes('立即播放')).click();
  await flush(); await flush();
  assert.deepEqual(writes, [{ source_id: 2 }]);
  assert.equal(audio.state.playback_state, 'loading');
  assert.equal(useToastStore().items.at(-1)?.message, '已提交播放请求：曲目2');
});

test('离开旧主机后迟到的音频请求不在新主机显示错误通知', async () => {
  await mountAudio();
  apiModule.configureApiServer({ origin: 'http://127.0.0.1:20830', displayName: 'Alpha', allowInsecureDevelopment: true });
  const fallback = globalThis.fetch; let release;
  const gate = new Promise(resolve => { release = resolve; });
  globalThis.fetch = async (url, options = {}) => {
    const path = new URL(String(url), browser.location.origin).pathname;
    if (path === '/api/auth/logout/') return response({ success: true });
    if (path !== '/api/background-audio/play-source/') return fallback(url, options);
    await gate;
    return response({ success: true, background_audio: snapshot(2, 2) });
  };
  const row = [...browser.document.querySelectorAll('.background-audio__source-item')]
    .find(item => item.textContent.includes('曲目2'));
  [...row.querySelectorAll('button')].find(button => button.textContent.includes('立即播放')).click();
  await flush();
  await useRuntimeStore().switchServer({ origin: 'http://127.0.0.1:20831', displayName: 'Beta', allowInsecureDevelopment: true });
  release(); await flush(); await flush();
  assert.deepEqual(useToastStore().items, [], 'Alpha迟到反馈不属于Beta');
});

test('当前主机拒绝播放时保留实际观测并显示错误，不提示已提交', async () => {
  const audio = await mountAudio(); const fallback = globalThis.fetch;
  globalThis.fetch = async (url, options = {}) => {
    const path = new URL(String(url), browser.location.origin).pathname;
    if (path !== '/api/background-audio/play-source/') return fallback(url, options);
    return new Response(JSON.stringify({ error: '播放器不可用', code: 'worker_unavailable' }),
      { status: 503, headers: { 'Content-Type': 'application/json' } });
  };
  const row = [...browser.document.querySelectorAll('.background-audio__source-item')]
    .find(item => item.textContent.includes('曲目2'));
  [...row.querySelectorAll('button')].find(button => button.textContent.includes('立即播放')).click();
  await flush(); await flush();
  assert.equal(audio.state.source_id, 1);
  assert.equal(useToastStore().items.at(-1)?.level, 'error');
  assert.equal(useToastStore().items.at(-1)?.message, '背景音乐操作失败');
  assert.ok(useToastStore().items.every(item => !item.message.includes('已提交')));
});

test('离开音频页后旧播放错误不能污染共享通知，新页面动作仍能提示', async () => {
  await mountAudio(); const fallback = globalThis.fetch; let release;
  const gate = new Promise(resolve => { release = resolve; });
  const notices = useToastStore();
  globalThis.fetch = async (url, options = {}) => {
    const path = new URL(String(url), browser.location.origin).pathname;
    if (path !== '/api/background-audio/play-source/') return fallback(url, options);
    await gate;
    return new Response(JSON.stringify({ detail: '旧页面音频播放拒绝', code: 'worker_unavailable' }),
      { status: 503, headers: { 'Content-Type': 'application/json' } });
  };
  const row = [...browser.document.querySelectorAll('.background-audio__source-item')]
    .find(item => item.textContent.includes('曲目2'));
  [...row.querySelectorAll('button')].find(button => button.textContent.includes('立即播放')).click();
  await flush();
  app.unmount(); app = undefined; browser.document.body.replaceChildren();
  assert.equal(notices.items.length, 0);
  release(); await flush(); await flush();
  assert.deepEqual(notices.items, [], '共享Pinia/通知仍存在，只有已离开页面的反馈需要失效');
  const audio = await mountAudio();
  assert.equal(audio.state.source_id, 1, '新页面仍可以正常加载');
});

test('离开音频页后旧刷新错误不污染共享通知', async () => {
  await mountAudio(); const fallback = globalThis.fetch; let release;
  const gate = new Promise(resolve => { release = resolve; }); const notices = useToastStore();
  globalThis.fetch = async (url, options = {}) => {
    const path = new URL(String(url), browser.location.origin).pathname;
    if (path !== '/api/background-audio/') return fallback(url, options);
    await gate;
    return new Response(JSON.stringify({ detail: '旧刷新失败', code: 'qa_refresh_failed' }),
      { status: 503, headers: { 'Content-Type': 'application/json' } });
  };
  [...browser.document.querySelectorAll('button')].find(button => button.textContent.trim() === '刷新').click();
  await flush(); app.unmount(); app = undefined; browser.document.body.replaceChildren();
  release(); await flush(); await flush();
  assert.deepEqual(notices.items, [], '读取请求的失败也只属于已离开的页面');
});

test('较早播放请求结束时不提前解除后续在途操作的等待状态', async () => {
  await mountAudio(); const fallback = globalThis.fetch; const gates = [], releases = [];
  for (let index = 0; index < 2; index++) gates.push(new Promise(resolve => { releases.push(resolve); }));
  let count = 0;
  globalThis.fetch = async (url, options = {}) => {
    const path = new URL(String(url), browser.location.origin).pathname;
    if (path !== '/api/background-audio/play-source/') return fallback(url, options);
    const current = count++; await gates[current];
    return response({ success: true, background_audio: snapshot(current + 1, current + 2) });
  };
  const buttons = [...browser.document.querySelectorAll('.background-audio__source-item button')]
    .filter(button => button.textContent.includes('立即播放'));
  buttons[0].click(); await flush(); buttons[1].click(); await flush();
  assert.equal(count, 2, '两次显式操作均已经提交，测试不伪造并发');
  const previous = browser.document.querySelector('button[aria-label="上一首"]');
  assert.equal(previous.disabled, true);
  releases[0](); await flush(); await flush();
  const busyAfterEarlierCompleted = previous.disabled;
  releases[1](); await flush(); await flush();
  assert.equal(busyAfterEarlierCompleted, true, '后续请求未结束，旧finally不能清除其等待状态');
  assert.equal(previous.disabled, false, '最后请求结束后正常解除等待');
});

test('播放列表项仅受理时使用相同请求提示', async () => {
  const audio = await mountAudio(); const fallback = globalThis.fetch; const writes = [];
  globalThis.fetch = async (url, options = {}) => {
    const path = new URL(String(url), browser.location.origin).pathname;
    if (path !== '/api/background-audio/playlist/2/play/') return fallback(url, options);
    writes.push(path);
    const pending = snapshot(2, 2);
    Object.assign(pending.state, { playback_state: 'loading', pending_command: 'OPEN' });
    return response({ success: true, background_audio: pending });
  };
  const row = [...browser.document.querySelectorAll('.background-audio__playlist-item')]
    .find(item => item.textContent.includes('曲目2'));
  [...row.querySelectorAll('button')].find(button => button.textContent.trim() === '播放').click();
  await flush(); await flush();
  assert.deepEqual(writes, ['/api/background-audio/playlist/2/play/']);
  assert.equal(audio.state.playback_state, 'loading');
  assert.equal(useToastStore().items.at(-1)?.message, '已提交播放请求：曲目2');
});

test('较旧音量REST晚于新音频观测返回，真实页面仍保留新曲目和新位置', async () => {
  const audio = await mountAudio();
  const fallback = globalThis.fetch;
  let release;
  const gate = new Promise(resolve => { release = resolve; });
  globalThis.fetch = async (url, options = {}) => {
    if (new URL(String(url), browser.location.origin).pathname !== '/api/background-audio/volume/') return fallback(url, options);
    await gate; return response({ success: true, background_audio: snapshot(1, 1) });
  };
  const update = audio.setVolume(30);
  const latest = snapshot(2, 2); latest.state.position_ms = 2000;
  audio.applyRemoteSnapshot(latest); await flush();
  assert.equal(browser.document.querySelector('.background-audio__now h3')?.textContent, '曲目2');
  release(); await update; await flush();
  assert.equal(browser.document.querySelector('.background-audio__now h3')?.textContent, '曲目2');
  assert.equal(audio.state.position_ms, 2000);
});

test('背景音频进度可用公开键盘定位，HTTP携带毫秒目标而不重开曲目', async () => {
  await mountAudio();
  const fallback = globalThis.fetch;
  const writes = [];
  globalThis.fetch = async (url, options = {}) => {
    if (options.method && options.method !== 'GET') {
      writes.push({ path: new URL(String(url), browser.location.origin).pathname, body: JSON.parse(options.body) });
      return response({ success: true, background_audio: snapshot(1, 2) });
    }
    return fallback(url, options);
  };
  const progress = browser.document.querySelector('[role="slider"][aria-label="播放进度"]');
  assert.ok(progress, '支持定位的音频应提供进度操作');
  progress.focus();
  progress.dispatchEvent(new browser.KeyboardEvent('keydown', { key: 'ArrowRight', bubbles: true, cancelable: true }));
  await flush();
  progress.dispatchEvent(new browser.KeyboardEvent('keyup', { key: 'ArrowRight', bubbles: true }));
  await flush();
  assert.ok(writes.length >= 1);
  assert.ok(writes.every((write) => write.path === '/api/background-audio/control/'));
  assert.deepEqual([...new Set(writes.map((write) => JSON.stringify(write.body)))], [JSON.stringify({ action: 'seek', position_ms: 2000 })]);
});

test('切换主机后背景音量未发缓冲不得自动投向新主机', async () => {
  const audio = await mountAudio();
  apiModule.configureApiServer({ origin: 'http://127.0.0.1:20820', displayName: 'Alpha', allowInsecureDevelopment: true });
  const fallback = globalThis.fetch;
  const writes = [];
  let release;
  const gate = new Promise(resolve => { release = resolve; });
  globalThis.fetch = async (url, options = {}) => {
    const target = new URL(String(url), browser.location.origin);
    if (target.pathname === '/api/auth/logout/') return response({ success: true });
    if (target.pathname !== '/api/background-audio/volume/') return fallback(url, options);
    writes.push({ port: target.port, body: JSON.parse(options.body) });
    if (writes.length === 1) await gate;
    return response({ success: true, background_audio: snapshot(1, 1) });
  };
  const volume = browser.document.querySelector('[role="slider"][aria-label="音量"]');
  volume.focus();
  for (let index = 0; index < 2; index++) {
    volume.dispatchEvent(new browser.KeyboardEvent('keydown', { key: 'ArrowLeft', bubbles: true, cancelable: true }));
    await flush();
    volume.dispatchEvent(new browser.KeyboardEvent('keyup', { key: 'ArrowLeft', bubbles: true }));
    await flush();
  }
  assert.equal(writes.length, 1);
  await useRuntimeStore().switchServer({ origin: 'http://127.0.0.1:20821', displayName: 'Beta', allowInsecureDevelopment: true });
  audio.applyRemoteSnapshot(snapshot(2, 2)); await flush();
  release(); await flush(); await flush();
  assert.deepEqual(writes.map(write => write.port), ['20820']);
});

for (const [label, patch] of [['待机', { playback_state: 'idle' }], ['加载', { playback_state: 'loading' }],
  ['停止', { playback_state: 'stopped' }], ['错误', { playback_state: 'error' }],
  ['未知时长', { duration_ms: 0 }], ['空源', { source_id: null }]]) {
  test(`${label}音频不发送进度定位`, async () => {
    const audio = await mountAudio();
    const value = snapshot(1, 2); Object.assign(value.state, patch); audio.applyRemoteSnapshot(value); await flush();
    const writes = [];
    globalThis.fetch = async (url, options = {}) => {
      if (options.method && options.method !== 'GET') writes.push(String(url));
      return response({ success: true, background_audio: value });
    };
    const progress = browser.document.querySelector('[role="slider"][aria-label="播放进度"]');
    progress.focus(); progress.dispatchEvent(new browser.KeyboardEvent('keydown', { key: 'ArrowRight', bubbles: true, cancelable: true }));
    await flush(); progress.dispatchEvent(new browser.KeyboardEvent('keyup', { key: 'ArrowRight', bubbles: true })); await flush();
    assert.deepEqual(writes, []);
  });
}

test('定位在途换曲丢弃旧缓冲，新的用户输入仍可定位新曲目', async () => {
  const audio = await mountAudio();
  const writes = []; let release;
  const gate = new Promise(resolve => { release = resolve; });
  globalThis.fetch = async (url, options = {}) => {
    writes.push({ path: new URL(String(url), browser.location.origin).pathname, body: JSON.parse(options.body) });
    if (writes.length === 1) await gate;
    return response({ success: true, background_audio: audio.snapshot });
  };
  const progress = browser.document.querySelector('[role="slider"][aria-label="播放进度"]'); progress.focus();
  for (let index = 0; index < 2; index++) {
    progress.dispatchEvent(new browser.KeyboardEvent('keydown', { key: 'ArrowRight', bubbles: true, cancelable: true }));
    await flush(); progress.dispatchEvent(new browser.KeyboardEvent('keyup', { key: 'ArrowRight', bubbles: true })); await flush();
  }
  assert.equal(writes.length, 1);
  const next = snapshot(2, 2); next.state.position_ms = 2000;
  audio.applyRemoteSnapshot(next); await flush(); release(); await flush(); await flush();
  assert.equal(writes.length, 1, '旧缓冲不能发给新曲目');
  assert.equal(audio.state.source_id, 2, '当前曲目应已更新');
  assert.equal(audio.state.position_ms, 2000, '实际远端位置应已更新');
  const currentProgress = browser.document.querySelector('[role="slider"][aria-label="播放进度"]');
  currentProgress.focus();
  assert.equal(currentProgress.getAttribute('aria-valuenow'), '2000');
  currentProgress.dispatchEvent(new browser.KeyboardEvent('keydown', { key: 'ArrowRight', bubbles: true, cancelable: true })); await flush();
  assert.equal(writes.length, 2, '新用户输入应可用');
  assert.deepEqual(writes[1].body, { action: 'seek', position_ms: 3000 });
});

test('并发刷新时旧请求先结束，不能提前清除较新读取的loading', async () => {
  const audio = await mountAudio();
  let releaseFirst, releaseSecond;
  const firstGate = new Promise(resolve => { releaseFirst = resolve; });
  const secondGate = new Promise(resolve => { releaseSecond = resolve; });
  let count = 0;
  const fallback = globalThis.fetch;
  globalThis.fetch = async (url, options = {}) => {
    if (new URL(String(url), browser.location.origin).pathname !== '/api/background-audio/') return fallback(url, options);
    const current = ++count; await (current === 1 ? firstGate : secondGate);
    return response({ success: true, background_audio: snapshot(current, current) });
  };
  const first = audio.refresh(); const second = audio.refresh();
  releaseFirst(); await first; await flush();
  const stillLoading = audio.loading;
  releaseSecond(); await second; await flush();
  assert.equal(stillLoading, true, '最新读取尚未结束');
  assert.equal(audio.loading, false);
});

for (const [name, action, method, path] of [
  ['刷新', store => store.refresh(), 'GET', '/api/background-audio/'],
  ['加入', store => store.addSource(2), 'POST', '/api/background-audio/playlist/'],
  ['播放源', store => store.playSource(1), 'POST', '/api/background-audio/play-source/'],
  ['播放列表项', store => store.playItem(1), 'POST', '/api/background-audio/playlist/1/play/'],
  ['删除项', store => store.removeItem(1), 'DELETE', '/api/background-audio/playlist/1/'],
  ['清空', store => store.clearPlaylist(), 'DELETE', '/api/background-audio/playlist/'],
  ['控制', store => store.control('pause'), 'POST', '/api/background-audio/control/'],
  ['静音', store => store.setMute(false), 'PATCH', '/api/background-audio/mute/'],
  ['循环', store => store.setLoop(true), 'PATCH', '/api/background-audio/loop/'],
]) {
  test(`${name}较旧REST不覆盖新观测，写请求不额外重发`, async () => {
    const audio = await mountAudio(); const fallback = globalThis.fetch; let release;
    const gate = new Promise(resolve => { release = resolve; }); let calls = 0;
    globalThis.fetch = async (url, options = {}) => {
      if (new URL(String(url), browser.location.origin).pathname !== path) return fallback(url, options);
      assert.equal(options.method ?? 'GET', method); calls++;
      await gate;
      const old = snapshot(1, 1);
      if (name === '清空') { old.state.source_id = null; old.state.source = null; old.playlist = []; }
      return response({ success: true, background_audio: old });
    };
    const pending = action(audio); audio.applyRemoteSnapshot(snapshot(2, 2)); await flush();
    release(); await pending; await flush();
    assert.equal(audio.state.source_id, 2); assert.equal(audio.playlist.length, 2); assert.equal(calls, 1);
  });
}

test('较旧SSE或未知时间不覆盖明确新观测，更新和同时间列表变化仍可采纳', async () => {
  const audio = await mountAudio(); audio.applyRemoteSnapshot(snapshot(2, 2));
  for (const timestamp of ['2026-10-04T00:00:01Z', '', 'invalid', undefined]) {
    const old = snapshot(1, 1); old.state.updated_at = timestamp; audio.applyRemoteSnapshot(old); await flush();
    assert.equal(audio.state.source_id, 2);
  }
  const equal = snapshot(2, 2); equal.playlist = [equal.playlist[1]];
  audio.applyRemoteSnapshot(equal); await flush(); assert.equal(audio.playlist.length, 1);
  audio.applyRemoteSnapshot(snapshot(1, 3)); await flush(); assert.equal(audio.state.source_id, 1);
});

test('切换主机清store后Alpha旧finally不解除Beta新读取，旧响应也不污染Beta', async () => {
  const audio = await mountAudio();
  apiModule.configureApiServer({ origin: 'http://127.0.0.1:20810', displayName: 'Alpha', allowInsecureDevelopment: true });
  let releaseAlpha, releaseBeta;
  const alphaGate = new Promise(resolve => { releaseAlpha = resolve; });
  const betaGate = new Promise(resolve => { releaseBeta = resolve; });
  const fallback = globalThis.fetch;
  globalThis.fetch = async (url, options = {}) => {
    const target = new URL(String(url), browser.location.origin);
    if (target.pathname === '/api/auth/logout/') return response({ success: true });
    if (target.pathname !== '/api/background-audio/') return fallback(url, options);
    await (target.port === '20810' ? alphaGate : betaGate);
    return response({ success: true, background_audio: snapshot(target.port === '20810' ? 1 : 2, 1) });
  };
  const alpha = audio.refresh().then(() => null, error => error);
  await useRuntimeStore().switchServer({ origin: 'http://127.0.0.1:20811', displayName: 'Beta', allowInsecureDevelopment: true });
  const beta = audio.refresh(); releaseAlpha(); const error = await alpha; await flush();
  const betaLoading = audio.loading; const betaStillUnknown = audio.snapshot === null;
  releaseBeta(); await beta; await flush();
  assert.equal(error?.name, 'StaleConnectionResponseError');
  assert.equal(betaLoading, true); assert.equal(betaStillUnknown, true);
  assert.equal(audio.state.source_id, 2); assert.equal(audio.loading, false);
});
