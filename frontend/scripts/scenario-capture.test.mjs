/** 当前播放生成预案：实际源与请求源分离，未知状态不写进可执行草稿。 */
import assert from 'node:assert/strict';
import test, { before, after } from 'node:test';
import { createDomTestServer } from './dom-test-server.mjs';
import { createPinia, setActivePinia } from 'pinia';
import { Window } from 'happy-dom';

let server, capture;
before(async () => {
  server = await createDomTestServer();
  ({ captureScenarioState: capture } = await server.ssrLoadModule('/src/features/scenarios/scenarioCapture.ts'));
});
after(async () => { await server?.close(); });
const base = { window_id: 1, source_id: 9, actual_source_id: 3, player_online: true,
  playback_state: 'playing', pending_command: '', desired_generation: 8, observed_generation: 7 };
/** :param sessions: 只读窗口快照。:returns: 带已确认音量和布局的调用参数。 */
function input(sessions = [base]) {
  return { sessions, runtime: { big_screen_mode: 'double' }, volume: { level: 35, systemSynced: true }, folderId: 4 };
}

test('请求源与实际源不同，只捕获已报告的实际源，保持原输入对象', () => {
  const params = input(); const before = JSON.stringify(params);
  const result = capture(params);
  assert.equal(result.draft.windows[0].sourceId, 3);
  assert.equal(result.draft.windows[0].sourceState, 'set');
  assert.equal(result.draft.windows[0].autoplay, true);
  assert.equal(result.draft.folderId, 4);
  assert.equal(result.draft.bigScreenMode, 'double');
  assert.equal(result.draft.volumeLevel, 35);
  assert.equal(JSON.stringify(params), before);
});

test('暂停的实际媒体不被捕获成自动播放', () => {
  const result = capture(input([{ ...base, playback_state: 'paused' }]));
  assert.equal(result.draft.windows[0].sourceId, 3);
  assert.equal(result.draft.windows[0].autoplay, false);
});

for (const [label, patch] of [
  ['离线', { player_online: false }], ['在途', { pending_command: 'OPEN' }],
  ['错误', { playback_state: 'error' }], ['加载', { playback_state: 'loading' }],
  ['没有实际源字段', { actual_source_id: undefined }], ['实际源为空', { actual_source_id: null }],
  ['没有实际代次', { observed_generation: undefined }],
]) test(`${label}的窗口保持原状，并给出未捕获原因，不回退请求源`, () => {
  const result = capture(input([{ ...base, ...patch }]));
  assert.equal(result.draft.windows[0].sourceState, 'unset');
  assert.equal(result.draft.windows[0].sourceId, null);
  assert.deepEqual(result.omittedWindowIds, [1, 2]);
});

test('明确空闲的空窗口保持原状，未加载窗口单独提示', () => {
  const result = capture(input([{ ...base, source_id: null, actual_source_id: null, playback_state: 'idle' }]));
  assert.equal(result.draft.windows[0].sourceState, 'unset');
  assert.deepEqual(result.omittedWindowIds, [2]);
});

for (const field of ['share_id', 'actual_share_id', 'actual_adapter_kind']) {
  test(`${field}临时共享阻止生成草稿`, () => {
    const result = capture(input([{ ...base, [field]: field === 'actual_adapter_kind' ? 'screen_share' : 'temporary-id' }]));
    assert.equal(result.draft, null);
    assert.equal(result.sharedWindowId, 1);
  });
}

test('未知布局及未同步系统音量保持原状，不把默认值写成现场配置', () => {
  const result = capture({ ...input(), runtime: null, volume: { level: 100, systemSynced: false } });
  assert.equal(result.draft.bigScreenModeState, 'unset');
  assert.equal(result.draft.volumeState, 'unset');
  assert.equal(result.omittedSystemVolume, true);
  const manual = capture({ ...input(), runtime: { big_screen_mode: 'custom' } });
  assert.equal(manual.draft.bigScreenModeState, 'unset');
});

test('切换主机后新音量读取失败，不捕获旧主机已同步的音量', async () => {
  const { useRuntimeStore } = await server.ssrLoadModule('/src/stores/runtime.ts');
  const previousFetch = globalThis.fetch;
  const previousWindow = Object.getOwnPropertyDescriptor(globalThis, 'window');
  const browser = new Window({ url: 'http://127.0.0.1:20980' });
  Object.defineProperty(globalThis, 'window', { value: browser, configurable: true, writable: true });
  const pinia = createPinia(); setActivePinia(pinia);
  const runtime = useRuntimeStore(pinia);
  runtime.applyVolume({ level: 35, muted: true, system_synced: true, backend: 'windows_core_audio' });
  globalThis.fetch = async url => new Response(JSON.stringify(String(url).includes('/volume/')
    ? { detail: '新主机音量暂不可用' } : { success: true, csrfToken: 'local-test-token' }), {
    status: String(url).includes('/volume/') ? 503 : 200, headers: { 'Content-Type': 'application/json' },
  });
  try {
    await runtime.switchServer({ origin: 'http://127.0.0.1:20981', displayName: '另一主机', allowInsecureDevelopment: true });
    await assert.rejects(runtime.refreshSystemVolume());
    assert.equal(runtime.systemVolume.systemSynced, false);
    const result = capture({ ...input(), volume: runtime.systemVolume });
    assert.equal(result.draft.volumeState, 'unset');
    assert.equal(result.omittedSystemVolume, true);
  } finally {
    globalThis.fetch = previousFetch;
    if (previousWindow) Object.defineProperty(globalThis, 'window', previousWindow); else delete globalThis.window;
    await browser.happyDOM.close();
  }
});
