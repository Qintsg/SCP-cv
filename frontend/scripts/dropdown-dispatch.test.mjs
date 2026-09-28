/*
 * 下拉菜单动作回归：挂载真实 Vue/Naive UI，以受控 HTTP 验证派发次数与禁用门禁。
 * 所有请求均在 fetch 边界截断，不连接播放主机或执行应急物理动作。
 */
import assert from 'node:assert/strict';
import { setImmediate as nextTurn } from 'node:timers/promises';
import test, { after, afterEach, before } from 'node:test';
import { Window } from 'happy-dom';
import { createDomTestServer } from './dom-test-server.mjs';

const browser = new Window({ url: 'http://localhost:5173/sources' });
const globalNames = ['window', 'document', 'navigator', 'HTMLElement', 'HTMLInputElement',
  'HTMLTextAreaElement', 'Element', 'Node', 'SVGElement', 'Document', 'MutationObserver',
  'ResizeObserver', 'Image', 'getComputedStyle', 'requestAnimationFrame', 'cancelAnimationFrame'];
const previousGlobals = new Map(globalNames.map((name) => [name, Object.getOwnPropertyDescriptor(globalThis, name)]));
for (const name of globalNames) {
  const value = typeof browser[name] === 'function' && /^[a-z]/.test(name)
    ? browser[name].bind(browser) : browser[name];
  Object.defineProperty(globalThis, name, { value, configurable: true, writable: true });
}

let server;
let vue;
let createPinia;
let SourcesView;
let EmergencyMenu;
let useSourceStore;
let i18n;
let app;
let requests;
let source;
let folders;
const originalFetch = globalThis.fetch;

before(async () => {
  const [vueModule, piniaModule, testServer] = await Promise.all([
    import('vue'), import('pinia'), createDomTestServer(),
  ]);
  vue = vueModule;
  createPinia = piniaModule.createPinia;
  server = testServer;
  ({ default: SourcesView } = await server.ssrLoadModule('/src/features/sources/SourcesView.vue'));
  ({ default: EmergencyMenu } = await server.ssrLoadModule('/src/layouts/EmergencyMenu.vue'));
  ({ useSourceStore } = await server.ssrLoadModule('/src/stores/sources.ts'));
  ({ i18n } = await server.ssrLoadModule('/src/locales/index.ts'));
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
 * 等待 Vue 和已完成 HTTP 回调更新菜单。
 * :returns: 当前一轮渲染完成。
 */
async function flushUi() {
  await vue.nextTick();
  await nextTurn();
  await vue.nextTick();
}

/**
 * 挂载真实页面；HTTP 替身保留命令请求与列表回读语义。
 * :param component: 待验证页面。
 * :param folderId: 源所在目录；null 表示根目录。
 * :param folderFixture: 可选的受控目录树。
 * :returns: 已初始化的 Pinia。
 */
async function mountMenu(component = SourcesView, folderId = 1, folderFixture) {
  requests = [];
  source = { id: 1, name: '受控图卡', source_type: 'image', folder_id: folderId,
    uri: 'D:/受控资料/图卡.png', file_size: 1024, is_available: true,
    created_at: new Date().toISOString() };
  folders = folderFixture ?? [{ id: 1, name: '受控资料', parent_id: null, relative_path: '受控资料' }];
  globalThis.fetch = async (url, options = {}) => {
    const pathname = new URL(url, browser.location.href).pathname;
    requests.push({ pathname, method: options.method || 'GET', body: options.body });
    let payload = { success: true, sessions: [] };
    if (pathname === '/api/auth/csrf/') payload = { csrfToken: 'controlled-test-token' };
    if (pathname === '/api/sources/') payload = { sources: [source] };
    if (pathname === '/api/folders/') payload = { folders };
    if (pathname === '/api/sources/1/move/') {
      source = { ...source, folder_id: JSON.parse(options.body).folder_id };
      payload = { success: true, source };
    }
    return new Response(JSON.stringify(payload), { headers: { 'Content-Type': 'application/json' } });
  };
  const pinia = createPinia();
  const store = useSourceStore(pinia);
  store.sources = [source];
  store.folders = folders;
  store.currentFolderId = folderId;
  const host = browser.document.createElement('div');
  browser.document.body.append(host);
  app = vue.createApp(component);
  app.use(pinia);
  app.use(i18n);
  app.mount(host);
  await flushUi();
  return pinia;
}

/**
 * 按实际渲染文本找到菜单项，不读取组件私有方法。
 * :param label: 菜单可见文字。
 * :returns: 菜单项原生 DOM。
 */
function menuItem(label) {
  const item = [...browser.document.querySelectorAll('.n-dropdown-option-body')]
    .find((candidate) => candidate.textContent.trim() === label);
  assert.ok(item, `应渲染「${label}」菜单项`);
  return item;
}

/**
 * 从源操作按钮打开移动级联菜单。
 * :returns: 已渲染的移动目标项。
 */
async function openSourceMove() {
  browser.document.querySelector('button[aria-label="受控图卡 的操作菜单"]').click();
  await flushUi();
  menuItem('移动到…').dispatchEvent(new browser.MouseEvent('mouseenter', { bubbles: true }));
  await flushUi();
  return menuItem('移动到根目录');
}

/**
 * 从真实原生按钮打开目录的级联菜单。
 * :returns: 已渲染的根目录目标项。
 */
async function openFolderMove() {
  browser.document.querySelector('.sources-view__folder-card button[aria-label="编辑"]').click();
  await flushUi();
  menuItem('移动文件夹到…').dispatchEvent(new browser.MouseEvent('mouseenter', { bubbles: true }));
  await flushUi();
  return menuItem('移动到根目录');
}

/**
 * 发送受控 DOM 的公开键盘事件，不调用组件内部选中方法。
 * :param key: 方向键或确认键。
 * :returns: 键盘事件处理完成。
 */
async function pressKey(key) {
  browser.document.dispatchEvent(new browser.KeyboardEvent('keydown', { key, bubbles: true, cancelable: true }));
  await flushUi();
}

/**
 * 打开受控应急菜单，其 HTTP 动作同样只到 fetch 替身。
 * :returns: 应急菜单已渲染。
 */
async function openEmergency() {
  browser.document.querySelector('button[aria-label="应急控制"]').click();
  await flushUi();
}

test('鼠标从源菜单移动一次只发送一条命令', async () => {
  await mountMenu();
  const target = await openSourceMove();
  target.click();
  await flushUi();
  assert.equal(requests.filter((request) => request.pathname === '/api/sources/1/move/').length, 1);
});

test('键盘从源菜单移动一次只发送一条命令', async () => {
  await mountMenu();
  await openSourceMove();
  await pressKey('ArrowRight');
  assert.ok(menuItem('移动到根目录').classList.contains('n-dropdown-option-body--pending'));
  await pressKey('Enter');
  assert.equal(requests.filter((request) => request.pathname === '/api/sources/1/move/').length, 1);
});

test('已在根目录的源点击禁用目标不会发送移动命令', async () => {
  await mountMenu(SourcesView, null);
  const target = await openSourceMove();
  assert.ok(target.classList.contains('n-dropdown-option-body--disabled'));
  target.click();
  await flushUi();
  assert.equal(requests.filter((request) => request.pathname === '/api/sources/1/move/').length, 0);
});

test('鼠标从目录菜单移动一次只发送一条命令', async () => {
  await mountMenu(SourcesView, 1, [
    { id: 1, name: '受控资料', parent_id: null, relative_path: '受控资料' },
    { id: 2, name: '受控子目录', parent_id: 1, relative_path: '受控资料/受控子目录' },
  ]);
  const target = await openFolderMove();
  target.click();
  await flushUi();
  assert.equal(requests.filter((request) => request.pathname === '/api/folders/2/').length, 1);
});

test('已在根目录的文件夹点击禁用目标不会发送移动命令', async () => {
  await mountMenu(SourcesView, null);
  const target = await openFolderMove();
  assert.ok(target.classList.contains('n-dropdown-option-body--disabled'));
  target.click();
  await flushUi();
  assert.equal(requests.filter((request) => request.method === 'PATCH').length, 0);
});

test('鼠标应急动作只发送一条受控命令', async () => {
  await mountMenu(EmergencyMenu);
  await openEmergency();
  menuItem('重置全部窗口').click();
  await flushUi();
  assert.equal(requests.filter((request) => request.pathname === '/api/playback/reset-all/').length, 1);
});

test('键盘应急动作只发送一条受控命令', async () => {
  await mountMenu(EmergencyMenu);
  await openEmergency();
  await pressKey('ArrowDown');
  assert.ok(menuItem('重置全部窗口').classList.contains('n-dropdown-option-body--pending'));
  await pressKey('Enter');
  assert.equal(requests.filter((request) => request.pathname === '/api/playback/reset-all/').length, 1);
});

test('动作字段拆分后危险项仍保留 DOM 展示样式', async () => {
  await mountMenu(EmergencyMenu);
  await openEmergency();
  assert.ok(menuItem('关闭播放器服务').getAttribute('style').includes('var(--colorStatusDangerForeground1)'));
  assert.equal(requests.length, 0, '只查看菜单样式不应发送任何命令');
});
