/*
 * 真实选源面板上传→OPEN 回归：公开文件输入、分类和布局选择决定请求。
 * 仅替换外部 HTTP 的 XHR/fetch 边界，不替换组件、store 或 API 方法。
 * @Project : SCP-cv
 * @File : source-picker-upload.test.mjs
 * @Author : Qintsg
 * @Date : 2026-10-03
 */
import assert from 'node:assert/strict';
import { setImmediate as nextTurn } from 'node:timers/promises';
import test, { after, afterEach, before } from 'node:test';
import { Window } from 'happy-dom';
import { createDomTestServer } from './dom-test-server.mjs';

const browser = new Window({ url: 'http://localhost:5173/display' });
const globalNames = ['window', 'document', 'navigator', 'HTMLElement', 'HTMLInputElement',
  'HTMLTextAreaElement', 'Element', 'Node', 'SVGElement', 'Document', 'MutationObserver',
  'ResizeObserver', 'Image', 'InputEvent', 'getComputedStyle', 'requestAnimationFrame', 'cancelAnimationFrame',
  'File', 'Blob', 'FormData', 'XMLHttpRequest'];
const previousGlobals = new Map(globalNames.map((name) => [name, Object.getOwnPropertyDescriptor(globalThis, name)]));
for (const name of globalNames) {
  const value = typeof browser[name] === 'function' && /^[a-z]/.test(name)
    ? browser[name].bind(browser) : browser[name];
  Object.defineProperty(globalThis, name, { value, configurable: true, writable: true });
}

const originalFetch = globalThis.fetch;
let server;
let vue;
let createPinia;
let SourcePicker;
let useSessionStore;
let useSourceStore;
let useToastStore;
let i18n;
let app;
let requests;
let completeUploadResponse;

before(async () => {
  [vue, { createPinia }, server] = await Promise.all([
    import('vue'), import('pinia'), createDomTestServer(),
  ]);
  ({ default: SourcePicker } = await server.ssrLoadModule('/src/features/display/SourcePicker.vue'));
  ({ useSessionStore } = await server.ssrLoadModule('/src/stores/sessions.ts'));
  ({ useSourceStore } = await server.ssrLoadModule('/src/stores/sources.ts'));
  ({ useToastStore } = await server.ssrLoadModule('/src/composables/useToast.ts'));
  ({ i18n } = await server.ssrLoadModule('/src/locales/index.ts'));
});

afterEach(async () => {
  useToastStore?.().clear();
  app?.unmount();
  app = undefined;
  browser.document.body.replaceChildren();
  await browser.happyDOM.abort();
  globalThis.fetch = originalFetch;
});

after(async () => {
  await server?.close();
  await browser.happyDOM.close();
  for (const [name, descriptor] of previousGlobals) {
    if (descriptor) Object.defineProperty(globalThis, name, descriptor);
    else delete globalThis[name];
  }
});

/** :returns: 当前 Vue 渲染与已完成 HTTP 回调已经结束。 */
async function flushUi() {
  await vue.nextTick();
  await nextTurn();
  await vue.nextTick();
}

/**
 * 经实际外部 HTTP 边界响应上传源，保留真实 API 序列化与 store 更新。
 * :param source: 上传接口返回的已登记源。
 * :param openFailure: 播放接口拒绝时的明确错误；默认受理。
 * :param delayUpload: 是否由测试从外部HTTP边界明确完成上传响应。
 * :returns: 公开媒体源 store，可检查上传成功后保留的源。
 */
async function mountPicker(source, openFailure = '', delayUpload = false, sessionOverrides = {}) {
  requests = [];
  completeUploadResponse = undefined;
  const session = { window_id: 1, source_id: null, source_type: '', player_online: true,
    playback_state: 'idle', last_updated_at: '2026-10-03T00:00:00Z', ...sessionOverrides };
  globalThis.XMLHttpRequest = class {
    upload = {};
    headers = {};
    /** :param method: HTTP 动作。:param url: 实际上传目标。 */
    open(method, url) { this.method = method; this.url = String(url); }
    /** :param name: 请求头名称。:param value: 实际请求头值。 */
    setRequestHeader(name, value) { this.headers[name] = value; }
    /** :returns: 上传响应保持实际 JSON 解析路径。 */
    getResponseHeader() { return 'application/json'; }
    /** :param body: 实际构造的 multipart 表单，不触发网络或写文件。 */
    send(body) {
      requests.push({ url: this.url, method: this.method, body, kind: 'upload' });
      const finish = () => {
        this.status = 200;
        this.responseText = JSON.stringify({ success: true, source });
        this.onload?.();
      };
      if (delayUpload) completeUploadResponse = finish;
      else queueMicrotask(finish);
    }
  };
  globalThis.fetch = async (url, options = {}) => {
    const path = String(url);
    requests.push({ url: path, method: options.method ?? 'GET', body: options.body, kind: 'rest' });
    const failed = path.endsWith('/playback/1/open/') && openFailure;
    return new Response(JSON.stringify(failed ? { detail: openFailure } : {
      success: true, sessions: [session], background_audio: { state: {}, playlist: [] },
    }), { status: failed ? 400 : 200, headers: { 'Content-Type': 'application/json' } });
  };
  const pinia = createPinia();
  useSessionStore(pinia).applyRemoteSessions([session]);
  const sources = useSourceStore(pinia);
  const host = browser.document.createElement('div');
  browser.document.body.append(host);
  app = vue.createApp({ setup: () => () => vue.h(SourcePicker, { windowId: 1 }) });
  app.use(pinia);
  app.use(i18n);
  app.mount(host);
  await flushUi();
  return sources;
}

/**
 * 从用户可见分类与开关开启文字适配。
 * :returns: 布局选择已由真实 DOM 事件更新。
 */
async function selectFittedPresentation() {
  const tab = [...browser.document.querySelectorAll('.n-tabs-tab')]
    .find((element) => element.textContent.trim() === '演示文稿');
  assert.ok(tab, '应有文稿分类');
  tab.click();
  await flushUi();
  const toggle = browser.document.querySelector('[role="switch"][aria-label="文字适配（不修改原件）"]');
  assert.ok(toggle, '应有文字适配开关');
  toggle.click();
  await flushUi();
}

/**
 * 通过文件输入 change 选择上传文件，不直接调用组件内部方法。
 * :param filename: 本轮上传文件名。
 * :returns: 文件选择和按钮状态已渲染。
 */
async function chooseFile(filename) {
  const input = browser.document.querySelector('input[type="file"]');
  assert.ok(input, '应有文件选择入口');
  const transfer = new browser.DataTransfer();
  transfer.items.add(new browser.File(['测试文件内容'], filename));
  input.files = transfer.files;
  input.dispatchEvent(new browser.Event('change', { bubbles: true }));
  await flushUi();
}

/**
 * 从真实下拉列表选择明确的文稿模式。
 * :param label: 用户可见的模式名称。
 * :returns: 当前模式已完成渲染。
 */
async function choosePresentationMode(label) {
  const select = browser.document.querySelector('.n-select[aria-label="文稿打开方式"] .n-base-selection');
  assert.ok(select, '应有文稿模式选择');
  select.click();
  await flushUi();
  // happy-dom没有菜单布局高度，虚拟滚动不能保证第三个选项已有实体节点。
  // 从当前可见的默认选项按公开键盘操作移动，仍以最终可见文案确认选择。
  assert.match(select.textContent, /LibreOffice 动态放映/);
  const steps = new Map([
    ['静态逐页图片（无动画／媒体）', 1], ['静态 PDF（无动画／媒体）', 2],
  ]).get(label);
  assert.notEqual(steps, undefined, '测试必须使用用户可见的已知静态模式');
  for (let index = 0; index < steps; index++) {
    select.dispatchEvent(new browser.KeyboardEvent('keydown', { key: 'ArrowDown', bubbles: true }));
    await flushUi();
  }
  select.dispatchEvent(new browser.KeyboardEvent('keydown', { key: 'Enter', bubbles: true }));
  await flushUi();
  assert.ok(select.textContent.includes(label), `键盘选择后应显示${label}；实际：${select.textContent}`);
}

/** :returns: 用户可点击的上传并打开动作。 */
function uploadAndOpenButton() {
  const button = [...browser.document.querySelectorAll('button')]
    .find((element) => ['上传并打开', '重试打开已上传源'].includes(element.textContent.trim()));
  assert.ok(button, '应有上传并打开按钮');
  assert.equal(button.disabled, false);
  return button;
}

/** :returns: 在真实 HTTP seam 观察到的目标窗口 OPEN 请求。 */
function openBodies() {
  return requests.filter((item) => item.url.endsWith('/playback/1/open/') && item.method === 'POST')
    .map((item) => JSON.parse(item.body));
}

test('上传文稿并打开沿用用户明确选择的 LibreOffice 文字适配', async () => {
  await mountPicker({ id: 42, source_type: 'ppt', playback_mode: 'libreoffice', name: '测试文稿',
    is_available: true, metadata: {} });
  await selectFittedPresentation();
  await chooseFile('测试文稿.pptx');
  uploadAndOpenButton().click();
  await flushUi();
  assert.deepEqual(openBodies(), [{ source_id: 42, autoplay: true, presentation_mode: 'libreoffice', fit_text: true }]);
});

test('上传视频并打开仅受理时通知明确为打开请求，不冒称已经打开', async () => {
  await mountPicker({ id: 47, source_type: 'video', playback_mode: '', name: '在途视频', is_available: true, metadata: {} }, '', false,
    { source_id: 47, source_type: 'video', actual_source_id: null, playback_state: 'loading', pending_command: 'OPEN' });
  await chooseFile('在途视频.mp4'); uploadAndOpenButton().click(); await flushUi();
  assert.deepEqual(openBodies(), [{ source_id: 47, autoplay: true }]);
  assert.equal(useToastStore().items.at(-1)?.message, '已上传并提交打开请求');
});

test('普通选源仅受理时提示打开请求，不冒称已出画', async () => {
  const sources = await mountPicker({}, '', false, { source_id: 48, source_type: 'image', actual_source_id: null,
    playback_state: 'loading', pending_command: 'OPEN' });
  sources.sources = [{ id: 48, name: '在途图片', source_type: 'image', is_available: true, metadata: {} }];
  await flushUi(); browser.document.querySelector('.source-picker__item button').click(); await flushUi();
  assert.deepEqual(openBodies(), [{ source_id: 48, autoplay: true }]);
  assert.equal(useToastStore().items.at(-1)?.message, '已提交打开请求：在途图片');
});

test('显式静态未准备保留原始错误和布局选择，再次点击只重试已上传源的 OPEN', async () => {
  const detail = '同源静态PDF尚未准备完成，请使用LibreOffice或重新准备静态页。';
  const sources = await mountPicker({ id: 42, source_type: 'ppt', playback_mode: 'libreoffice', name: '测试文稿',
    is_available: true, metadata: {}, preparation_state: 'queued' }, detail);
  await selectFittedPresentation();
  await choosePresentationMode('静态 PDF（无动画／媒体）');
  await chooseFile('测试文稿.pptx');
  uploadAndOpenButton().click();
  await flushUi();
  assert.equal(browser.document.querySelector('.source-picker__upload-error')?.textContent, detail);
  assert.equal(sources.findById(42)?.id, 42, '明确上传成功的源应保留');
  assert.equal(browser.document.querySelector('[role="switch"][aria-label="文字适配（不修改原件）"]')
    ?.getAttribute('aria-checked'), 'true');
  assert.match(browser.document.querySelector('.n-select[aria-label="文稿打开方式"]')?.textContent ?? '', /静态 PDF/);
  uploadAndOpenButton().click();
  await flushUi();
  assert.deepEqual(openBodies(), [0, 1].map(() => ({
    source_id: 42, autoplay: true, presentation_mode: 'pdf', fit_text: true,
  })));
  assert.equal(requests.filter((request) => request.kind === 'upload').length, 1,
    'OPEN失败不能触发已成功文件的重复上传');
});

test('上传 PDF 原源时不误带当前文稿模式或文字适配选项', async () => {
  await mountPicker({ id: 43, source_type: 'ppt', playback_mode: 'pdf', name: '原始PDF',
    is_available: true, metadata: {} });
  await selectFittedPresentation();
  await choosePresentationMode('静态 PDF（无动画／媒体）');
  await chooseFile('原始PDF.pdf');
  uploadAndOpenButton().click();
  await flushUi();
  assert.deepEqual(openBodies(), [{ source_id: 43, autoplay: true }]);
});

test('上传非文稿时不携带残留的静态模式或文字适配选择', async () => {
  await mountPicker({ id: 44, source_type: 'image', playback_mode: '', name: '测试图片',
    is_available: true, metadata: {} });
  await selectFittedPresentation();
  await choosePresentationMode('静态逐页图片（无动画／媒体）');
  await chooseFile('测试图片.png');
  uploadAndOpenButton().click();
  await flushUi();
  assert.deepEqual(openBodies(), [{ source_id: 44, autoplay: true }]);
});

test('上传音频仍调用独立背景音乐入口，不给大屏窗口发送文稿 OPEN', async () => {
  await mountPicker({ id: 45, source_type: 'audio', playback_mode: '', name: '测试音乐',
    is_available: true, metadata: {} });
  await selectFittedPresentation();
  await chooseFile('测试音乐.mp3');
  uploadAndOpenButton().click();
  await flushUi();
  assert.deepEqual(openBodies(), []);
  const audio = requests.filter((item) => item.url.endsWith('/background-audio/play-source/') && item.method === 'POST');
  assert.deepEqual(audio.map((item) => JSON.parse(item.body)), [{ source_id: 45 }]);
  assert.equal(useToastStore().items.at(-1)?.message, '已提交播放请求：测试音乐');
});

test('上传在途改变模式和文字适配只影响下一次点击，不改写本次已提交意图', async () => {
  await mountPicker({ id: 46, source_type: 'ppt', playback_mode: 'libreoffice', name: '在途文稿',
    is_available: true, metadata: {} }, '', true);
  await selectFittedPresentation();
  await chooseFile('在途文稿.pptx');
  uploadAndOpenButton().click();
  await flushUi();
  assert.deepEqual(openBodies(), [], '上传未响应前不得OPEN');
  const toggle = browser.document.querySelector('[role="switch"][aria-label="文字适配（不修改原件）"]');
  toggle.click();
  await flushUi();
  await choosePresentationMode('静态 PDF（无动画／媒体）');
  assert.equal(toggle.getAttribute('aria-checked'), 'false');
  assert.equal(typeof completeUploadResponse, 'function');
  completeUploadResponse();
  await flushUi();
  assert.deepEqual(openBodies(), [{ source_id: 46, autoplay: true, presentation_mode: 'libreoffice', fit_text: true }]);
});

test('切换请求尚未完成时源列表标记旧实际输出，不把新请求源标成当前输出', async () => {
  const sources = await mountPicker({}, '', false, { source_id: 52, actual_source_id: 51,
    actual_adapter_kind: 'image', source_type: 'video', player_online: true,
    playback_state: 'playing', desired_generation: 2, observed_generation: 1, pending_command: 'OPEN' });
  sources.sources = [
    { id: 51, source_type: 'image', name: '旧实际图片', is_available: true, metadata: {} },
    { id: 52, source_type: 'video', name: '新请求视频', is_available: true, metadata: {} },
  ];
  await flushUi();
  const rows = [...browser.document.querySelectorAll('.source-picker__item')];
  const old = rows.find((row) => row.textContent.includes('旧实际图片'));
  const desired = rows.find((row) => row.textContent.includes('新请求视频'));
  assert.equal(old.querySelector('button').getAttribute('aria-current'), 'true');
  assert.match(old.textContent, /当前输出/);
  assert.equal(desired.querySelector('button').getAttribute('aria-current'), null);
  assert.match(desired.textContent, /请求中/);
});

for (const [type, adapter] of [['image', 'image'], ['web', 'web'], ['video', 'vlc'],
  ['rtsp_stream', 'vlc'], ['srt_stream', 'vlc'], ['custom_stream', 'vlc'], ['ppt', 'pdf']]) {
  test(`${type}切换失败仍标旧实际输出，并明确标记失败请求源`, async () => {
    const sources = await mountPicker({}, '', false, { source_id: 62, actual_source_id: 61,
      actual_adapter_kind: adapter, source_type: type, player_online: true, error_message: '候选打开失败',
      playback_state: 'error', desired_generation: 2, observed_generation: 1 });
    sources.sources = [
      { id: 61, source_type: type, name: '旧实际源', is_available: true, metadata: {} },
      { id: 62, source_type: type, name: '失败请求源', is_available: true, metadata: {} },
    ];
    await flushUi();
    const rows = [...browser.document.querySelectorAll('.source-picker__item')];
    const old = rows.find((row) => row.textContent.includes('旧实际源'));
    const desired = rows.find((row) => row.textContent.includes('失败请求源'));
    assert.equal(old.querySelector('button').getAttribute('aria-current'), 'true');
    assert.match(old.textContent, /当前输出/);
    assert.match(desired.textContent, /请求失败/);
    assert.equal(desired.querySelector('button').getAttribute('aria-current'), null);
  });
}

test('实际源未知/明确为空时不借用请求源，离线保留最后报告的实际源', async () => {
  const sources = await mountPicker({}, '', false, { source_id: 72, actual_source_id: null,
    actual_adapter_kind: 'image', source_type: 'image', player_online: false });
  sources.sources = [{ id: 72, source_type: 'image', name: '请求图片', is_available: true, metadata: {} }];
  await flushUi();
  const sessions = useSessionStore();
  assert.equal(browser.document.querySelector('[aria-current="true"]'), null);
  assert.match(browser.document.querySelector('.source-picker__list').textContent, /请求源（播放器离线）/);
  sessions.applyRemoteSessions([{ ...sessions.byWindowId(1), actual_source_id: 72 }]);
  await flushUi();
  assert.match(browser.document.querySelector('[aria-current="true"]').textContent, /当前输出（离线，最后报告）/);
});
