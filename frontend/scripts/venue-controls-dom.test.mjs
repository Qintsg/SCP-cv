/* 真实设备页DOM与HTTP边界回归：不替换组件/store，不发送现场流量。 */
import { browserKeyCode, visibleText } from './dom-test-server.mjs';

import assert from 'node:assert/strict';
import { setImmediate as nextTurn } from 'node:timers/promises';
import test, { before, after, afterEach } from 'node:test';
import { Window } from 'happy-dom';
import { createDomTestServer } from './dom-test-server.mjs';
const browser = new Window({ url: 'http://localhost:5173/devices' });
const names = ['window','document','navigator','HTMLElement','HTMLInputElement','HTMLTextAreaElement','Element','Node','SVGElement','Document','MutationObserver','ResizeObserver','Image','InputEvent','KeyboardEvent','PointerEvent','getComputedStyle','requestAnimationFrame','cancelAnimationFrame'];
const previous = new Map(names.map(name => [name, Object.getOwnPropertyDescriptor(globalThis, name)]));
for (const name of names) Object.defineProperty(globalThis, name, { configurable: true, writable: true,
  value: typeof browser[name] === 'function' && /^[a-z]/.test(name) ? browser[name].bind(browser) : browser[name] });
const originalFetch = globalThis.fetch;
let vue, createPinia, server, DevicesView, ScenarioAdvancedSettings, ScenarioAdvancedSummary, fromScenarioItem, useAuthStore, useRuntimeStore, app;
let requests = []; let values = {};
const cameras = ['rear','center1','center2','left','right','fixed'].map((id, index) => ({
  id, name: ['后摄像机','中摄像机1','中摄像机2','左摄像机','右摄像机','固定监控'][index], path: '/Camera/' + id,
  source_id: index + 1, stream_configured: true, movable: id !== 'fixed', control_available: id !== 'fixed', position_count: id === 'fixed' ? 0 : 6,
  unavailable_reason: id === 'fixed' ? '固定监控不支持转动。' : '', motion_intent: 'idle',
}));
before(async () => {
  [vue, { createPinia }, server] = await Promise.all([import('vue'), import('pinia'), createDomTestServer()]);
  ({ default: DevicesView } = await server.ssrLoadModule('/src/features/devices/DevicesView.vue'));
  ({ default: ScenarioAdvancedSettings } = await server.ssrLoadModule('/src/features/scenarios/ScenarioAdvancedSettings.vue'));
  ({ default: ScenarioAdvancedSummary } = await server.ssrLoadModule('/src/features/scenarios/ScenarioAdvancedSummary.vue'));
  ({ fromScenarioItem } = await server.ssrLoadModule('/src/features/scenarios/scenarioModel.ts'));
  ({ useAuthStore } = await server.ssrLoadModule('/src/stores/auth.ts'));
  ({ useRuntimeStore } = await server.ssrLoadModule('/src/stores/runtime.ts'));
});
afterEach(async () => { app?.unmount(); app = undefined; await flush(); browser.document.body.replaceChildren(); await browser.happyDOM.abort(); globalThis.fetch = originalFetch; });
after(async () => { await server?.close(); await browser.happyDOM.close(); for (const [name, descriptor] of previous) { if (descriptor) Object.defineProperty(globalThis, name, descriptor); else delete globalThis[name]; } });
/** :returns: Vue与HTTP回调完成，不依赖真实设备或动画时长。 */
async function flush() { await nextTurn(); await vue.nextTick(); await nextTurn(); await vue.nextTick(); }
/** :returns: 完整设备页与受控HTTP记录。 */
async function mount(audio = {}, capabilities = {}) {
  requests = []; values = { screen_volume: 60, computer_volume: 25, handheld_volume: 35, headset_volume: 45, ...audio };
  globalThis.fetch = async (input, options = {}) => {
    const path = new URL(String(input), browser.location.href).pathname;
    const method = options.method ?? 'GET'; const body = options.body ? JSON.parse(options.body) : null;
    requests.push({ path, method, body }); let payload;
    if (path === '/api/venue/' && method === 'GET') payload = { success: true, venue: { cameras, audio: { ...values }, simulation: true, server_time_ms: Date.now(),
      capabilities: { screen_volume: true, computer_volume: true, handheld_volume: true, headset_volume: true, hanging_volume: true, source: false, reason: '来源未绑定', ...capabilities } } };
    else if (path === '/api/venue/audio/' && method === 'PUT') { values = { ...values, ...body }; payload = { success: true, audio: { status: 'sent_unconfirmed', detail: '配置已发送，设备未读回。' } }; }
    else if (path === '/api/venue/audio/read/') payload = { success: true, audio: { status: 'confirmed', detail: '155已读回', screen_volume: values.screen_volume } };
    else if (/\/cameras\//.test(path)) payload = { success: true, camera: { status: 'simulated', detail: '仿真，未控制实体', motion_id: body?.motion_id } };
    else if (path === '/api/sources/') payload = { success: true, sources: [] };
    else if (path === '/api/folders/') payload = { success: true, folders: [] };
    else throw new Error('Unexpected request: ' + method + ' ' + path);
    return new Response(JSON.stringify(payload), { status: 200, headers: { 'content-type': 'application/json' } });
  };
  const pinia = createPinia(); app = vue.createApp(DevicesView); app.use(pinia);
  useAuthStore(pinia).user = { id: 7, username: 'qa', is_staff: true, is_superuser: false, permissions: ['playback.control'] };
  const root = browser.document.createElement('div'); browser.document.body.append(root); app.mount(root); await flush();
  return { root, runtime: useRuntimeStore(pinia), auth: useAuthStore(pinia) };
}
/** :param text: 唯一按钮文案。:returns: 真实DOM按钮。 */
function button(text) { const matches = [...browser.document.querySelectorAll('button')].filter(node => visibleText(node.textContent) === visibleText(text)); assert.equal(matches.length, 1, text); return matches[0]; }
/** :returns: 通过真实Ant页签进入声音工作区，不直接改组件内部选择。 */
async function showAudio() {
  const tab=[...browser.document.querySelectorAll('[role="tab"]')].find(node=>node.textContent.trim()==='音量与来源');
  assert.ok(tab); tab.click(); await flush();
}

test('设备页仅有一个手控块；键盘按住和释放发送相同令牌', async () => {
  await mount(); assert.equal(browser.document.querySelectorAll('.camera-pad').length, 1);
  const up = browser.document.querySelector('[aria-label="中摄像机1向上"]'); assert.ok(up);
  up.dispatchEvent(new browser.KeyboardEvent('keydown', { keyCode: browserKeyCode(' '), which: browserKeyCode(' '),  key: ' ', bubbles: true })); await flush();
  up.dispatchEvent(new browser.KeyboardEvent('keyup', { keyCode: browserKeyCode(' '), which: browserKeyCode(' '),  key: ' ', bubbles: true })); await flush();
  const motion = requests.filter(item => /\/motion\//.test(item.path));
  assert.deepEqual(motion.map(item => item.body.phase), ['start','stop']);
  assert.equal(motion[0].body.motion_id, motion[1].body.motion_id);
  assert.equal(motion[0].path, '/api/venue/cameras/center1/motion/');
});

test('失焦释放一次；切换主机不把旧停止发往新主机', async () => {
  const mounted = await mount(); const up = browser.document.querySelector('[aria-label="中摄像机1向上"]');
  up.dispatchEvent(new browser.KeyboardEvent('keydown', { keyCode: browserKeyCode('Enter'), which: browserKeyCode('Enter'),  key: 'Enter', bubbles: true })); await flush();
  browser.dispatchEvent(new browser.Event('blur')); await flush();
  assert.deepEqual(requests.filter(item => /\/motion\//.test(item.path)).map(item => item.body.phase), ['start','stop']);
  up.dispatchEvent(new browser.KeyboardEvent('keydown', { keyCode: browserKeyCode('Enter'), which: browserKeyCode('Enter'),  key: 'Enter', bubbles: true })); await flush();
  mounted.runtime.connectionGeneration++; await flush();
  const motion = requests.filter(item => /\/motion\//.test(item.path)); assert.deepEqual(motion.map(item => item.body.phase), ['start','stop','start']);
});

test('一张卡片只写对应音量，保留别路未应用草稿；来源仍禁用', async () => {
  await mount(); await showAudio(); const handheld = browser.document.querySelector('input[aria-label="手持话筒"]'); assert.ok(handheld);
  handheld.value = '72'; handheld.dispatchEvent(new browser.Event('input', { bubbles: true })); await flush();
  button('应用大屏音量').click(); await flush();
  const writes = requests.filter(item => item.path === '/api/venue/audio/' && item.method === 'PUT');
  assert.deepEqual(writes.map(item => item.body), [{ computer_volume: 25 }]);
  assert.equal(browser.document.querySelector('input[aria-label="手持话筒"]').value, '72');
  assert.match(browser.document.body.textContent, /配置已发送，设备未读回/);
  assert.ok([...browser.document.querySelectorAll('input[type="radio"]')].every(node => node.disabled));
});

test('来源选择只是草稿，明确应用才发送一次且保留其它音量草稿', async () => {
  await mount({ source: 'ip81' }, { source: true, source_ip81: true, source_scp_cv: true, reason: '' });
  await showAudio();
  const radio = browser.document.querySelector('input[type="radio"][value="scp_cv"]');
  assert.ok(radio); assert.equal(radio.disabled, false);
  const handheld = browser.document.querySelector('input[aria-label="手持话筒"]');
  handheld.value = '72'; handheld.dispatchEvent(new browser.Event('input', { bubbles: true })); await flush();
  radio.click(); await flush(); assert.equal(requests.filter(item => item.method === 'PUT').length, 0);
  button('应用声音来源').click(); await flush();
  assert.deepEqual(requests.filter(item => item.method === 'PUT').map(item => item.body), [{ source: 'scp_cv' }]);
  assert.equal(browser.document.querySelector('input[aria-label="手持话筒"]').value, '72');
  assert.match(browser.document.body.textContent, /最后发送请求：SCP-cv/);
  assert.match(browser.document.body.textContent, /实际声音来源需现场确认/);
});

test('软件未就绪时仍可独立选择IP81，SCP-cv明确禁用', async () => {
  await mount({}, { source: true, source_ip81: true, source_scp_cv: false, reason: '软件输出未就绪' });
  await showAudio();
  const ip81 = browser.document.querySelector('input[type="radio"][value="ip81"]');
  const software = browser.document.querySelector('input[type="radio"][value="scp_cv"]');
  assert.equal(ip81.disabled, false); assert.equal(software.disabled, true);
  assert.equal(button('应用声音来源').disabled, true);
  ip81.click(); await flush(); button('应用声音来源').click(); await flush();
  assert.deepEqual(requests.filter(item => item.method === 'PUT').map(item => item.body), [{ source: 'ip81' }]);
  assert.match(browser.document.body.textContent, /软件输出未就绪/);
});

test('位置调用明确发一次；保存当前位置必须先确认', async () => {
  await mount(); button('调用位置').click(); await flush();
  assert.equal(requests.filter(item => item.path.endsWith('/positions/1/call/')).length, 1);
  button('保存当前位置…').click(); await flush();
  assert.equal(requests.filter(item => item.path.includes('/save/')).length, 0);
  assert.match(browser.document.body.textContent, /将覆盖中摄像机1的位置1/);
});

test('数字字段保留键盘输入，不暴露小于触控尺寸的微型加减按钮', async () => {
  await mount();
  assert.equal(browser.document.querySelectorAll('.ant-input-number button').length, 0);
  assert.equal(browser.document.querySelector('input[aria-label="中摄像机1位置"]').value, '1');
  await showAudio();
  assert.equal(browser.document.querySelector('input[aria-label="手持话筒"]').value, '35');
});

test('悬挂与头戴只显示一张音量卡；旧别名可读并按同一通道提交', async () => {
  await mount({ headset_volume: null, hanging_volume: 55 });
  await showAudio();
  const input = browser.document.querySelector('input[aria-label="悬挂（头戴）话筒"]');
  assert.ok(input); assert.equal(input.value, '55');
  assert.equal(browser.document.querySelectorAll('.audio-grid .ant-card').length, 3);
  button('应用悬挂（头戴）话筒').click(); await flush();
  assert.deepEqual(requests.filter(item => item.method === 'PUT').map(item => item.body), [{ headset_volume: 55 }]);
});

/** :param initial: 新旧字段的保存意图。:returns: 完整高级编辑和摘要组件。 */
async function mountAdvanced(initial) {
  const value = vue.ref(initial);
  app = vue.createApp({ setup: () => () => vue.h('div', [
    vue.h(ScenarioAdvancedSettings, { modelValue: value.value, 'onUpdate:modelValue': next => { value.value = next; } }),
    vue.h(ScenarioAdvancedSummary, { advanced: value.value }),
  ]) });
  const root = browser.document.createElement('div'); browser.document.body.append(root); app.mount(root); await flush();
  return value;
}

test('旧悬挂预设只有一个编辑项；修改和清空同时清除旧别名并保留其它配置', async () => {
  const value = await mountAdvanced({ camera_positions: { center1: 3 }, audio: { hanging_volume: 60, handheld_volume: 35 } });
  const input = browser.document.querySelector('input[aria-label="预设悬挂（头戴）话筒音量"]');
  assert.ok(input); assert.equal(input.value, '60');
  assert.equal(browser.document.querySelectorAll('input[aria-label*="头戴"]').length, 1);
  assert.equal(browser.document.querySelectorAll('.advanced-settings .ant-input-number button').length, 0);
  assert.doesNotMatch(browser.document.body.textContent, /设备未核对|尚未绑定的设备配置/);
  input.value = '75'; input.dispatchEvent(new browser.Event('input', { bubbles: true }));
  input.dispatchEvent(new browser.FocusEvent('blur')); await flush();
  assert.equal(value.value.audio.headset_volume, 75); assert.equal(value.value.audio.hanging_volume, null);
  assert.equal(value.value.audio.handheld_volume, 35); assert.equal(value.value.camera_positions.center1, 3);
  input.value = ''; input.dispatchEvent(new browser.Event('input', { bubbles: true }));
  input.dispatchEvent(new browser.FocusEvent('blur')); await flush();
  assert.equal(value.value.audio.headset_volume, null); assert.equal(value.value.audio.hanging_volume, null);
});

test('预设两个别名冲突时明确显示两值；用户编辑后消除冲突', async () => {
  await mountAdvanced({ audio: { headset_volume: 20, hanging_volume: 30 } });
  assert.match(browser.document.body.textContent, /冲突/);
  assert.match(browser.document.body.textContent, /20%/); assert.match(browser.document.body.textContent, /30%/);
  const input = browser.document.querySelector('input[aria-label="预设悬挂（头戴）话筒音量"]');
  assert.ok(input); input.value = '40'; input.dispatchEvent(new browser.Event('input', { bubbles: true }));
  input.dispatchEvent(new browser.FocusEvent('blur')); await flush();
  assert.doesNotMatch(browser.document.body.textContent, /冲突/);
});

for (const audio of [{ hanging_volume: 60 }, { headset_volume: 65 }, { headset_volume: 20, hanging_volume: 30 }]) {
  test('响应式预设可完整打开编辑；高级草稿与保存对象隔离 ' + JSON.stringify(audio), async () => {
    const original = vue.reactive({ id: 7, name: '保存的预案', description: '不能清空', folder_id: 3,
      big_screen_mode_state: 'unset', big_screen_mode: 'double', volume_state: 'unset', volume_level: 45,
      targets: [{ window_id: 1, source_state: 'set', source_id: 5, autoplay: true, resume: true }],
      advanced: { camera_positions: { center1: 3 }, audio: { handheld_volume: 35, source: null, ...audio } },
    });
    const draft = fromScenarioItem(original);
    assert.equal(draft.name, original.name); assert.equal(draft.id, 7); assert.equal(draft.folderId, 3);
    assert.equal(draft.description, original.description); assert.equal(draft.windows[0].sourceId, 5);
    assert.deepEqual(draft.advanced.audio, { handheld_volume: 35, source: null, ...audio });
    assert.equal(draft.advanced.camera_positions.center1, 3);
    draft.advanced.audio.headset_volume = 90; draft.advanced.camera_positions.center1 = 5;
    assert.equal(original.advanced.audio.headset_volume, audio.headset_volume);
    assert.equal(original.advanced.camera_positions.center1, 3);
  });
}
