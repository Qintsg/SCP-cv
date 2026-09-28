/*
 * 文件夹对话框行为回归：挂载真实 Vue/Naive UI，验证原生输入与异步保持状态。
 * HTTP 仅在 fetch 边界替换，测试不会连接或控制播放主机。
 */
import assert from 'node:assert/strict';
import { setImmediate as nextTurn } from 'node:timers/promises';
import test, { after, afterEach, before } from 'node:test';
import { Window } from 'happy-dom';
import { createDomTestServer } from './dom-test-server.mjs';

const browser = new Window({ url: 'http://localhost:5173/sources' });
const globalNames = ['window', 'document', 'navigator', 'HTMLElement', 'HTMLInputElement',
  'HTMLTextAreaElement', 'Element', 'Node', 'SVGElement', 'Document', 'MutationObserver',
  'ResizeObserver', 'Image', 'InputEvent', 'getComputedStyle', 'requestAnimationFrame', 'cancelAnimationFrame'];
const previousGlobals = new Map(globalNames.map((name) => [name, Object.getOwnPropertyDescriptor(globalThis, name)]));
for (const name of globalNames) {
  const value = typeof browser[name] === 'function' && /^[a-z]/.test(name)
    ? browser[name].bind(browser) : browser[name];
  Object.defineProperty(globalThis, name, { value, configurable: true, writable: true });
}

let server;
let vue;
let createPinia;
let useToastStore;
let pinia;
let SourcesView;
let FolderDialogs;
let i18n;
let app;
let requests;
let respond;
const originalFetch = globalThis.fetch;

before(async () => {
  const [vueModule, piniaModule, testServer] = await Promise.all([
    import('vue'), import('pinia'), createDomTestServer(),
  ]);
  vue = vueModule;
  createPinia = piniaModule.createPinia;
  server = testServer;
  ({ default: SourcesView } = await server.ssrLoadModule('/src/features/sources/SourcesView.vue'));
  ({ default: FolderDialogs } = await server.ssrLoadModule('/src/features/sources/FolderDialogs.vue'));
  ({ i18n } = await server.ssrLoadModule('/src/locales/index.ts'));
  ({ useToastStore } = await server.ssrLoadModule('/src/composables/useToast.ts'));
});

afterEach(async () => {
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

/**
 * 等待 Vue 更新和已完成请求的后续回调。
 * :returns: 当前一轮渲染结束。
 */
async function flushUi() {
  await vue.nextTick();
  await nextTurn();
  await vue.nextTick();
}

/**
 * 等待可见 DOM 状态满足条件，包含 Naive UI 的关闭动画。
 * :param condition: 可见状态判定。
 * :returns: 条件满足。
 * :raises Error: 状态在一秒内未满足。
 */
async function waitForUi(condition) {
  if (condition()) return;
  await new Promise((resolve, reject) => {
    const observer = new browser.MutationObserver(() => {
      if (!condition()) return;
      clearTimeout(timeout);
      observer.disconnect();
      resolve();
    });
    const timeout = setTimeout(() => {
      observer.disconnect();
      reject(new Error('等待 DOM 状态超时'));
    }, 1000);
    observer.observe(browser.document.body, { childList: true, subtree: true, attributes: true });
  });
}

/**
 * 以空媒体库挂载页面，HTTP 请求由各用例提供结果。
 * :returns: 页面完成首次渲染。
 */
async function mountSources() {
  requests = [];
  respond = async () => new Response(JSON.stringify({ success: true }), {
    headers: { 'Content-Type': 'application/json' },
  });
  globalThis.fetch = async (url, options) => {
    requests.push({ url, options });
    return respond(url, options);
  };
  const host = browser.document.createElement('div');
  browser.document.body.append(host);
  app = vue.createApp(SourcesView);
  pinia = createPinia();
  app.use(pinia);
  app.use(i18n);
  app.mount(host);
  await flushUi();
}

/**
 * 按用户可见语义打开新建文件夹弹窗。
 * :returns: 原生文件夹名称输入框。
 */
async function openCreateDialog() {
  browser.document.querySelector('button[aria-label="新建文件夹"]').click();
  await flushUi();
  const input = browser.document.querySelector('input[placeholder="例如：早会资料"]');
  assert.ok(input, '新建文件夹弹窗应包含原生输入框');
  return input;
}

/**
 * 找到对话框内的可见动作按钮。
 * :param label: 按钮文字。
 * :returns: 匹配的原生按钮。
 */
function dialogButton(label) {
  const button = [...browser.document.querySelectorAll('[role="dialog"] button')]
    .find((candidate) => candidate.textContent.trim() === label);
  assert.ok(button, `对话框应包含「${label}」按钮`);
  return button;
}

/**
 * 向真实原生输入框输入文本。
 * :param input: 原生输入框。
 * :param value: 用户输入。
 * :returns: 输入事件已送达。
 */
function typeName(input, value) {
  input.value = value;
  input.dispatchEvent(new browser.InputEvent('input', { bubbles: true, data: value }));
}

/**
 * 按组件公开模型挂载删除弹窗，模拟父级操作失败后仍保留目标。
 * :returns: 父级可观察的弹窗模型。
 */
async function mountDeleteConfirmation() {
  const state = vue.reactive({ open: true, contents: true });
  const host = browser.document.createElement('div');
  browser.document.body.append(host);
  app = vue.createApp({
    setup: () => () => vue.h(FolderDialogs, {
      createOpen: false, name: '', nameError: false, creating: false, deleting: false,
      deleteTarget: { id: 1, name: '早会资料', parent_id: null, relative_path: '早会资料' },
      deleteOpen: state.open, deleteContents: state.contents,
      'onUpdate:deleteOpen': (value) => { state.open = value; },
      'onUpdate:deleteContents': (value) => { state.contents = value; },
      createFolder: async () => false,
      // 父级失败处理保留 open 与目标，和现有目录 composable 的返回语义一致。
      deleteFolder: async () => undefined,
    }),
  });
  app.use(i18n);
  app.mount(host);
  await flushUi();
  return state;
}

test('文件夹名称的可访问名称在原生 textbox 上', async () => {
  await mountSources();
  const input = await openCreateDialog();
  assert.equal(input.getAttribute('aria-label'), '文件夹名称');
});

test('空名称不发送请求，保持弹窗并标记原生 textbox 无效', async () => {
  await mountSources();
  const input = await openCreateDialog();
  dialogButton('创建').click();
  await flushUi();
  assert.equal(requests.length, 0, '空名称应在请求之前拒绝');
  assert.equal(browser.document.querySelector('[role="alert"]').textContent.trim(), '请输入文件夹名称');
  assert.equal(input.getAttribute('aria-invalid'), 'true');
  assert.ok(input.isConnected, '校验失败后弹窗应保持打开');
  typeName(input, '早会资料');
  await flushUi();
  assert.equal(input.getAttribute('aria-invalid'), 'false');
  assert.equal(Boolean(browser.document.querySelector('[role="alert"]')), false);
});

test('创建等待期间阻止重复提交，成功后关闭弹窗并展示目录', async () => {
  await mountSources();
  const input = await openCreateDialog();
  let completeRequest;
  respond = () => new Promise((resolve) => { completeRequest = resolve; });
  typeName(input, '  早会资料  ');
  await flushUi();
  const button = dialogButton('创建');
  button.click();
  await flushUi();
  assert.equal(button.disabled, true, '等待期间创建按钮应禁用');
  input.dispatchEvent(new browser.KeyboardEvent('keyup', { key: 'Enter', bubbles: true }));
  await flushUi();
  assert.equal(requests.length, 1, '等待中的回车不能再创建一次');
  assert.equal(input.disabled, true, '等待期间输入框应禁用以防回车重复提交');
  assert.deepEqual(JSON.parse(requests[0].options.body), { name: '早会资料', parent_id: null });
  completeRequest(new Response(JSON.stringify({ success: true,
    folder: { id: 1, name: '早会资料', parent_id: null, relative_path: '早会资料' },
  }), { headers: { 'Content-Type': 'application/json' } }));
  await flushUi();
  assert.ok(browser.document.querySelector('.sources-view__folder-name')?.textContent.includes('早会资料'));
  await waitForUi(() => !browser.document.querySelector('input[aria-label="文件夹名称"]'));
});

test('重名创建失败保持名称和弹窗，解除等待并呈现后端错误', async () => {
  await mountSources();
  const input = await openCreateDialog();
  typeName(input, '早会资料');
  await flushUi();
  respond = async () => new Response(JSON.stringify({ success: false, detail: '当前目录已存在同名文件夹' }), {
    status: 400, headers: { 'Content-Type': 'application/json' },
  });
  dialogButton('创建').click();
  await flushUi();
  assert.equal(input.value, '早会资料');
  assert.equal(input.disabled, false);
  assert.equal(dialogButton('创建').disabled, false);
  assert.equal(browser.document.querySelector('input[aria-label="文件夹名称"]') === input, true);
  const notification = useToastStore(pinia).items.find((item) => item.level === 'error');
  assert.equal(notification?.message, '文件夹操作失败');
  assert.equal(notification?.description, '当前目录已存在同名文件夹');
});

test('删除失败保持确认框和已勾选的删除范围', async () => {
  const state = await mountDeleteConfirmation();
  dialogButton('删除').click();
  await flushUi();
  assert.equal(state.open, true, '操作回调未关闭时，Naive UI 不能误关闭失败弹窗');
  assert.equal(browser.document.querySelector('[role="checkbox"]')?.getAttribute('aria-checked'), 'true');
});
