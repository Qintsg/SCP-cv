/*
 * 文件夹对话框行为回归：挂载真实 Vue/Ant Design Vue，验证原生输入与异步保持状态。
 * HTTP 仅在 fetch 边界替换，测试不会连接或控制播放主机。
 */
import { browserKeyCode, visibleText } from './dom-test-server.mjs';

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
let useSourceStore;
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
  ({ useSourceStore } = await server.ssrLoadModule('/src/stores/sources.ts'));
});

afterEach(async () => {
  // Vue过渡包含原生定时器；等公开动画状态结束后才卸载并释放浏览器全局。
  try { await waitForUi(() => ![...browser.document.querySelectorAll('[class*="-enter-active"], [class*="-leave-active"]')].some(element => {
    // 已取消入场且display:none的Dropdown缓存不会再播放CSS动画，不是活跃过渡。
    for (let node = element; node; node = node.parentElement) {
      const style = browser.getComputedStyle(node);
      if (style.display === 'none' || style.visibility === 'hidden') return false;
    }
    return true;
  })); }
  finally {
    app?.unmount(); await vue.nextTick(); app = undefined;
    browser.document.body.replaceChildren(); await browser.happyDOM.abort(); globalThis.fetch = originalFetch;
  }
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
 * 等待可见DOM状态满足条件，包含Ant的关闭动画。
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
    .find((candidate) => visibleText(candidate.textContent) === visibleText(label));
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
  input.dispatchEvent(new browser.KeyboardEvent('keyup', { keyCode: browserKeyCode('Enter'), which: browserKeyCode('Enter'),  key: 'Enter', bubbles: true }));
  await flushUi();
  assert.equal(requests.length, 1, '等待中的回车不能再创建一次');
  assert.equal(input.disabled, true, '等待期间输入框应禁用以防回车重复提交');
  assert.deepEqual(JSON.parse(requests[0].options.body), { name: '早会资料', parent_id: null });
  completeRequest(new Response(JSON.stringify({ success: true,
    folder: { id: 1, name: '早会资料', parent_id: null, relative_path: '早会资料' },
  }), { headers: { 'Content-Type': 'application/json' } }));
  await flushUi();
  assert.ok(browser.document.querySelector('.sources-view__folder-name')?.textContent.includes('早会资料'));
  await waitForUi(() => {
    const input=browser.document.querySelector('input[aria-label="文件夹名称"]');
    if (!input) return true;
    for(let element=input;element;element=element.parentElement){
      if(element.getAttribute('aria-hidden')==='true'||browser.getComputedStyle(element).display==='none')return true;
    }
    return false;
  });
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
  assert.equal(state.open, true, '操作回调未关闭时，Ant不能误关闭失败弹窗');
  assert.equal(browser.document.querySelector('input[type="checkbox"]')?.checked, true);
});

/** :returns: 通过真实目录菜单打开的重命名表单及触发按钮。 */
async function openRenameDialog() {
  await mountSources();
  useSourceStore(pinia).folders = [{ id: 8, name: '原目录', parent_id: null, relative_path: '/原目录' }];
  let nativeCalls = 0;
  browser.prompt = () => { nativeCalls++; return null; };
  await flushUi();
  const trigger = browser.document.querySelector('button[aria-label="目录 原目录 的操作"]');
  trigger.click(); await flushUi();
  await waitForUi(() => [...browser.document.querySelectorAll('[role="menuitem"]')].some(item => visibleText(item.textContent) === '重命名'));
  [...browser.document.querySelectorAll('[role="menuitem"]')].find(item => visibleText(item.textContent) === '重命名').click();
  await flushUi();
  assert.equal(nativeCalls, 0, '重命名应使用Ant表单，不再弹浏览器prompt');
  await waitForUi(() => browser.document.querySelector('input[aria-label="新目录名称"]'));
  return { input: browser.document.querySelector('input[aria-label="新目录名称"]'), trigger };
}

test('目录重命名预填原名称，取消零写入并恢复更多按钮焦点', async () => {
  const { input, trigger } = await openRenameDialog();
  assert.equal(input.value, '原目录');
  typeName(input, '不保存'); dialogButton('取消').click(); await flushUi();
  await waitForUi(() => browser.document.activeElement === trigger);
  assert.equal(requests.length, 0);
  assert.equal(useSourceStore(pinia).folders[0].name, '原目录');
});

test('目录重命名空白拒绝、等待防重、回车只提交冻结目标一次', async () => {
  const { input } = await openRenameDialog();
  typeName(input, ' '); dialogButton('重命名').click(); await flushUi();
  assert.equal(requests.length, 0);
  assert.equal(input.getAttribute('aria-invalid'), 'true');
  let complete;
  respond = (url, options) => options.method === 'PATCH'
    ? new Promise(resolve => { complete = resolve; })
    : Promise.resolve(new Response(JSON.stringify(String(url).includes('/folders/')
      ? { folders: [{ id: 8, name: '新目录', parent_id: null, relative_path: '/新目录' }] } : { sources: [] }),
      { headers: { 'Content-Type': 'application/json' } }));
  typeName(input, '  新目录  '); await flushUi();
  input.dispatchEvent(new browser.KeyboardEvent('keydown', { key: 'Enter', keyCode: 13, bubbles: true }));
  input.dispatchEvent(new browser.KeyboardEvent('keydown', { key: 'Enter', keyCode: 13, bubbles: true }));
  await flushUi();
  const writes = requests.filter(item => item.options.method === 'PATCH');
  assert.equal(writes.length, 1);
  assert.match(String(writes[0].url), /\/folders\/8\/$/);
  assert.deepEqual(JSON.parse(writes[0].options.body), { name: '新目录' });
  assert.equal(input.disabled, true);
  assert.equal(dialogButton('重命名').disabled, true);
  complete(new Response(JSON.stringify({ success: true }), { headers: { 'Content-Type': 'application/json' } }));
  await waitForUi(() => useSourceStore(pinia).folders[0]?.name === '新目录');
  assert.equal(useSourceStore(pinia).folders[0].name, '新目录');
});

test('目录重命名失败保留输入和目标，在表单内显示原始错误', async () => {
  const { input } = await openRenameDialog();
  respond = async () => new Response(JSON.stringify({ detail: '当前目录已存在同名目录' }),
    { status: 409, headers: { 'Content-Type': 'application/json' } });
  typeName(input, '保留我的输入'); dialogButton('重命名').click(); await flushUi();
  assert.equal(input.value, '保留我的输入');
  assert.equal(input.disabled, false);
  assert.equal(dialogButton('重命名').disabled, false);
  assert.match(browser.document.querySelector('[role="dialog"]').textContent, /当前目录已存在同名目录/);
});

test('打开菜单的Enter释放和中文组合回车不提交重命名', async () => {
  const state = vue.reactive({ open: true, name: '原目录', submissions: 0 });
  const host = browser.document.createElement('div'); browser.document.body.append(host);
  app = vue.createApp({ setup: () => () => vue.h(FolderDialogs, {
    createOpen:false,name:'',nameError:false,creating:false,deleting:false,deleteTarget:null,deleteOpen:false,deleteContents:false,
    createFolder:async()=>false,deleteFolder:async()=>{},renameOpen:state.open,renameName:state.name,renameTarget:{id:1,name:'原目录',parent_id:null},
    'onUpdate:renameOpen':value=>{state.open=value;},'onUpdate:renameName':value=>{state.name=value;},
    renameFolder:async()=>{state.submissions++;return true;},
  }) });
  app.use(i18n);app.mount(host);await flushUi();
  const input=browser.document.querySelector('input[aria-label="新目录名称"]');
  input.dispatchEvent(new browser.KeyboardEvent('keyup',{key:'Enter',keyCode:13,bubbles:true}));await flushUi();
  assert.equal(state.open,true);assert.equal(state.submissions,0);
  const composition=new browser.KeyboardEvent('keydown',{key:'Enter',keyCode:13,isComposing:true,bubbles:true});
  assert.equal(composition.isComposing,true);input.dispatchEvent(composition);await flushUi();
  assert.equal(state.open,true);assert.equal(state.submissions,0);
  input.dispatchEvent(new browser.KeyboardEvent('keydown',{key:'Enter',keyCode:13,bubbles:true}));await flushUi();
  assert.equal(state.submissions,1);assert.equal(state.open,false);
});
