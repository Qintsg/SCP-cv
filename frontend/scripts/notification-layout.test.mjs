/*
 * 全局通知布局回归：真实 Naive UI DOM 验证断点、安全区样式与关闭行为。
 * Happy DOM 只证明组件配置与事件，不作为真实浏览器几何或出画证据。
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
let FNotificationProvider;
let FToastHost;
let app;

before(async () => {
  const [vueModule, piniaModule, testServer] = await Promise.all([
    import('vue'), import('pinia'), createDomTestServer(),
  ]);
  vue = vueModule;
  createPinia = piniaModule.createPinia;
  server = testServer;
  ({ default: FNotificationProvider } = await server.ssrLoadModule('/src/design-system/FNotificationProvider.vue'));
  ({ default: FToastHost } = await server.ssrLoadModule('/src/design-system/FToastHost.vue'));
  ({ useToastStore } = await server.ssrLoadModule('/src/composables/useToast.ts'));
});

afterEach(async () => {
  app?.unmount();
  app = undefined;
  browser.document.body.replaceChildren();
  await browser.happyDOM.abort();
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
 * 等待原生通知 DOM 跟随响应式更新。
 * :returns: 当前一轮渲染完成。
 */
async function flushUi() {
  await vue.nextTick();
  await nextTurn();
  await vue.nextTick();
}

/**
 * 等待通知关闭动画完成，不把过渡中的 DOM 当作残留通知。
 * :param condition: 通知状态判定。
 * :returns: 状态满足。
 * :raises Error: 一秒内仍未达到预期状态。
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
      reject(new Error('等待通知 DOM 状态超时'));
    }, 1000);
    observer.observe(browser.document.body, { childList: true, subtree: true, attributes: true });
  });
}

/**
 * 通过业务公开 store 创建一条持久错误通知。
 * :param width: 测试视口宽度，只用于断点监听。
 * :returns: 通知 store 与原生容器。
 */
async function mountNotifications(width) {
  browser.happyDOM.setWindowSize({ width, height: 844 });
  const pinia = createPinia();
  const store = useToastStore(pinia);
  store.error('保存失败', '当前目录已存在同名媒体');
  const host = browser.document.createElement('div');
  browser.document.body.append(host);
  app = vue.createApp({
    render: () => vue.h(FNotificationProvider, null, { default: () => vue.h(FToastHost) }),
  });
  app.use(pinia);
  app.mount(host);
  await flushUi();
  const container = browser.document.querySelector('.n-notification-container');
  assert.ok(container, '持久通知应实际渲染');
  return { store, container };
}

test('手机通知位于顶部并为应用头部和底部操作保留安全区', async () => {
  const { container } = await mountNotifications(390);
  assert.equal(container.classList.contains('n-notification-container--top'), true);
  assert.ok(container.style.top.includes('safe-area-inset-top'));
  assert.ok(container.style.maxHeight.includes('safe-area-inset-bottom'));
  assert.equal(container.style.overflowY, 'auto');
});

test('桌面保留右下通知和默认宽度，不应用手机偏移', async () => {
  const { container } = await mountNotifications(1440);
  assert.equal(container.classList.contains('n-notification-container--bottom-right'), true);
  assert.equal(container.getAttribute('style'), null);
  const notification = browser.document.querySelector('.n-notification');
  assert.equal(notification.style.getPropertyValue('--n-width'), '365px');
});

test('通知随导航的599/600断点和窗口变化调整安全区', async () => {
  const { container } = await mountNotifications(599);
  assert.equal(container.classList.contains('n-notification-container--top'), true);
  const notification = browser.document.querySelector('.n-notification');
  // Happy DOM 不解析 max() 定位；这里只核对真实通知收到的安全区宽度表达式。
  assert.ok(notification.style.getPropertyValue('--n-width').includes('safe-area-inset-left'));
  assert.ok(notification.style.getPropertyValue('--n-width').includes('safe-area-inset-right'));
  browser.happyDOM.setWindowSize({ width: 600, height: 844 });
  await flushUi();
  assert.equal(container.classList.contains('n-notification-container--bottom-right'), true);
  assert.equal(container.style.top, '');
  browser.happyDOM.setWindowSize({ width: 390, height: 844 });
  await flushUi();
  assert.equal(container.classList.contains('n-notification-container--top'), true);
  assert.equal(container.style.overflowY, 'auto');
});

test('手机持久错误通知仍可通过实际关闭按钮移除', async () => {
  const { store, container } = await mountNotifications(390);
  const close = container.querySelector('.n-notification__close');
  assert.ok(close, '持久通知必须提供关闭入口');
  close.click();
  await waitForUi(() => store.items.length === 0);
  assert.equal(Boolean(browser.document.querySelector('.n-notification')), false);
});
