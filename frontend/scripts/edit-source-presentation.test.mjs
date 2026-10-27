/*
 * 文稿编辑抽屉的布局制品状态回归，挂载真实 Vue/Ant Design Vue。
 * 只在 HTTP 边界提供测试响应，不连接播放主机或启动原生引擎。
 * @Project : SCP-cv
 * @File : edit-source-presentation.test.mjs
 * @Author : Qintsg
 * @Date : 2026-10-02
 */
import { visibleText } from './dom-test-server.mjs';

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
let EditSourceDrawer;
let i18n;
let app;

before(async () => {
  const [vueModule, piniaModule, testServer] = await Promise.all([
    import('vue'), import('pinia'), createDomTestServer(),
  ]);
  vue = vueModule;
  createPinia = piniaModule.createPinia;
  server = testServer;
  ({ default: EditSourceDrawer } = await server.ssrLoadModule('/src/features/sources/EditSourceDrawer.vue'));
  ({ i18n } = await server.ssrLoadModule('/src/locales/index.ts'));
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
 * 等待 Vue 更新和已完成请求的后续回调。
 * :returns: 当前一轮渲染结束。
 */
async function flushUi() {
  await vue.nextTick();
  await nextTurn();
  await vue.nextTick();
}

/**
 * 通过公开 props 挂载编辑抽屉，不提供原生引擎或服务器连接。
 * :param metadata: 默认/适配布局的页图制品信息。
 * :returns: 供后续公开 props 更新使用的父级状态。
 */
async function mountDrawer(metadata) {
  const state = vue.reactive({ open: true, source: {
    id: 1, name: '测试文稿', source_type: 'ppt', playback_mode: 'libreoffice',
    preparation_state: 'ready', page_count: 9, metadata,
  } });
  const host = browser.document.createElement('div');
  browser.document.body.append(host);
  app = vue.createApp({
    setup: () => () => vue.h(EditSourceDrawer, {
      open: state.open, source: state.source,
      'onUpdate:open': (value) => { state.open = value; },
    }),
  });
  app.use(createPinia());
  app.use(i18n);
  app.mount(host);
  await flushUi();
  return state;
}

/**
 * 获取用户可见的当前布局准备说明。
 * :returns: 抽屉中的状态和页数文本。
 */
function preparationText() {
  return browser.document.querySelector('.edit-source__preparation')?.textContent ?? '';
}

/**
 * 通过真实开关改变所查看的布局。
 * :returns: 开关操作后的渲染完成。
 */
async function toggleFit() {
  const toggle = browser.document.querySelector('[role="switch"][aria-label="页图文字适配"]');
  assert.ok(toggle, '应提供默认关闭的文字适配开关');
  toggle.click();
  await flushUi();
}

test('选择文字适配时，已准备页数来自适配制品而非原排版', async () => {
  await mountDrawer({ slide_images: { status: 'ready', page_count: 9 },
    fitted_slide_images: { status: 'ready', page_count: 3 } });
  assert.match(preparationText(), /已准备 9 页/);
  await toggleFit();
  assert.match(preparationText(), /已准备 3 页/);
  await toggleFit();
  assert.match(preparationText(), /已准备 9 页/);
});

test('缺失适配制品不显示原排版已准备页数，并保留显式准备入口', async () => {
  await mountDrawer({ slide_images: { status: 'ready', page_count: 9 } });
  await toggleFit();
  assert.match(preparationText(), /页图尚未准备；已准备 0 页/);
  assert.ok([...browser.document.querySelectorAll('.edit-source__preparation button')]
    .some((button) => visibleText(button.textContent) === visibleText('重试转换')));
});

test('适配转换结果待确认时显示独立状态和零页，不提供盲重试', async () => {
  await mountDrawer({ slide_images: { status: 'ready', page_count: 9 },
    fitted_slide_images: { status: 'uncertain', page_count: 0 } });
  await toggleFit();
  assert.match(preparationText(), /转换结果待人工确认；已准备 0 页/);
  assert.equal([...browser.document.querySelectorAll('.edit-source__preparation button')]
    .some((button) => visibleText(button.textContent) === visibleText('重试转换')), false);
});

test('无效制品页数不能冒充已准备页数，旧默认布局顶层页数仍兼容', async () => {
  const state = await mountDrawer({ fitted_slide_images: { status: 'ready', page_count: '3' } });
  assert.match(preparationText(), /已准备 9 页/);
  await toggleFit();
  for (const invalid of ['3', -1, 1.5, null, Number.NaN]) {
    state.source.metadata.fitted_slide_images.page_count = invalid;
    await flushUi();
    assert.match(preparationText(), /已准备 0 页/);
  }
});
