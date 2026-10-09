/*
 * 文稿预览真实DOM合同：按实际源与实际文字布局取图，不借期望源缩略图。
 * 真实Vue/Naive UI/Pinia/API均保留，只在外部fetch边界提供两布局资源。
 * @Project : SCP-cv
 * @File : fixture.mjs
 * @Author : Qintsg
 * @Date : 2026-10-03
 */
import assert from 'node:assert/strict';
import { setImmediate as nextTurn } from 'node:timers/promises';
import { after, afterEach, before } from 'node:test';
import { Window } from 'happy-dom';
import { createDomTestServer } from '../dom-test-server.mjs';

export const browser = new Window({ url: 'http://localhost:5173/display' });
const globalNames = ['window', 'document', 'navigator', 'history', 'localStorage', 'HTMLElement', 'HTMLInputElement',
  'HTMLTextAreaElement', 'Element', 'Node', 'SVGElement', 'Document', 'MutationObserver',
  'ResizeObserver', 'Image', 'InputEvent', 'getComputedStyle', 'requestAnimationFrame', 'cancelAnimationFrame'];
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
let routerModule;
export let PlaybackControl;
export let PptFocusView;
let useSessionStore;
let useSourceStore;
let i18n;
let app;
let requests;
let applicationPinia;
let observationNumber = 0;
const originalWindowTimeout = browser.setTimeout.bind(browser);
const originalWindowClearTimeout = browser.clearTimeout.bind(browser);

before(async () => {
  [vue, { createPinia }, routerModule, server] = await Promise.all([
    import('vue'), import('pinia'), import('vue-router'), createDomTestServer(),
  ]);
  ({ default: PlaybackControl } = await server.ssrLoadModule('/src/features/display/PlaybackControl.vue'));
  ({ default: PptFocusView } = await server.ssrLoadModule('/src/features/pptFocus/PptFocusView.vue'));
  ({ useSessionStore } = await server.ssrLoadModule('/src/stores/sessions.ts'));
  ({ useSourceStore } = await server.ssrLoadModule('/src/stores/sources.ts'));
  ({ i18n } = await server.ssrLoadModule('/src/locales/index.ts'));
});

afterEach(async () => {
  app?.unmount();
  app = undefined;
  applicationPinia = undefined;
  browser.document.body.replaceChildren();
  await browser.happyDOM.abort();
  globalThis.fetch = originalFetch;
  browser.setTimeout = originalWindowTimeout;
  browser.clearTimeout = originalWindowClearTimeout;
});

after(async () => {
  await server?.close();
  await browser.happyDOM.close();
  for (const [name, descriptor] of previousGlobals) {
    if (descriptor) Object.defineProperty(globalThis, name, descriptor);
    else delete globalThis[name];
  }
});

/** :returns: 当前Vue渲染与已完成HTTP回调已结束。 */
export async function flushUi() {
  await vue.nextTick();
  await nextTurn();
  await vue.nextTick();
}

/**
 * 外部HTTP资源数据；错误源和原排版仍返回有效图，避免仅靠空数据假绿。
 * :param sourceId: 所请求的源。
 * :param fitText: 所请求的布局。
 * :returns: 明确属于该源/布局的两页资源。
 */
export function resourceRows(sourceId, fitText) {
  return [1, 2].map((page) => ({
    id: sourceId * 10 + page, source_id: sourceId, page_index: page,
    slide_image: `/api/sources/${sourceId}/slides/${page}/${fitText ? '?fit_text=true' : ''}`,
    next_slide_image: page === 1 ? `/api/sources/${sourceId}/slides/2/${fitText ? '?fit_text=true' : ''}` : '',
    speaker_notes: `源${sourceId}${fitText ? '适配' : '原排版'}第${page}页`,
    has_media: false, media_items: [], created_at: '2026-10-03T00:00:00Z',
  }));
}

/**
 * 经公开refresh挂载真实页面；首次设置保留失败的期望源41和实际适配源40。
 * :param component: 播放控制或专注页。
 * :param reuseStoreScope: 路由替换回归保留应用的真实Pinia/通知store；独立测试默认新建。
 * :returns: 当前页面已从测试HTTP响应初始化。
 */
export async function mountPreview(component, { observation = {}, respondResource, respondNavigation, sourcesTransform, withNotifications = false, reuseStoreScope = false } = {}) {
  requests = [];
  observationNumber = 0;
  const session = {
    window_id: 1, session_id: 1, source_id: 41, source_name: '期望文稿41', source_type: 'ppt',
    source_uri: 'desired.pptx', source_type_label: 'PPT', playback_mode: 'libreoffice',
    playback_state: 'error', playback_state_label: '切源失败', error_message: '期望源打开失败',
    current_slide: 1, total_slides: 2, position_ms: null, duration_ms: null,
    volume: 100, is_muted: false, loop_enabled: false, player_online: true, pending_command: '',
    actual_source_id: 40, actual_adapter_kind: 'libreoffice', actual_fit_text: true,
    last_updated_at: '2026-10-03T00:00:00Z',
    ...observation,
  };
  const sources = [40, 41].map((id) => ({
    id, name: id === 40 ? '实际文稿40' : '期望文稿41', source_type: 'ppt', playback_mode: 'libreoffice',
    is_available: true, metadata: {}, preview_kind: 'image',
    preview_url: `/default-source-${id}.png`, thumbnail_url: `/default-source-${id}.png`,
  }));
  globalThis.fetch = async (url, options = {}) => {
    const target = new URL(String(url), browser.location.origin);
    requests.push({ pathname: target.pathname, search: target.search, method: options.method ?? 'GET' });
    let payload;
    const resourceMatch = /^\/api\/sources\/(\d+)\/ppt-resources\/$/.exec(target.pathname);
    if (resourceMatch) {
      const sourceId = Number(resourceMatch[1]);
      const fitText = target.searchParams.get('fit_text') === 'true';
      const supplied = await respondResource?.(sourceId, fitText);
      if (supplied instanceof Response) return supplied;
      payload = supplied ?? { success: true, resources: resourceRows(sourceId, fitText) };
    } else if (target.pathname === '/api/sources/') {
      payload = { success: true, sources: sourcesTransform ? sourcesTransform(sources) : sources };
    } else if (target.pathname === '/api/folders/') {
      payload = { success: true, folders: [] };
    } else if (target.pathname === '/api/sessions/') {
      payload = { success: true, sessions: [session] };
    } else if (target.pathname === '/api/runtime/') {
      payload = { success: true, runtime: { big_screen_mode: 'double' } };
    } else if (target.pathname === '/api/playback/1/navigate/' && respondNavigation) {
      payload = await respondNavigation(JSON.parse(options.body));
    } else {
      throw new Error(`测试未提供外部HTTP响应：${target.pathname}`);
    }
    return new Response(JSON.stringify(payload), { headers: { 'Content-Type': 'application/json' } });
  };
  // 路由页面更换时应用Pinia/通知仍存活；该外部框架接缝不能被新store掩盖。
  const pinia = reuseStoreScope && applicationPinia ? applicationPinia : createPinia();
  applicationPinia = pinia;
  const sessions = useSessionStore(pinia);
  await Promise.all([sessions.refresh(), useSourceStore(pinia).refresh()]);
  let ToastHost;
  let NotificationProvider;
  if (withNotifications) {
    ({ default: ToastHost } = await server.ssrLoadModule('/src/design-system/FToastHost.vue'));
    ({ NNotificationProvider: NotificationProvider } = await server.ssrLoadModule('naive-ui'));
  }
  app = vue.createApp({ setup: () => () => {
    const view = vue.h(component, component === PlaybackControl ? { session: sessions.byWindowId(1) } : {});
    return withNotifications ? vue.h(NotificationProvider, null, { default: () => [view, vue.h(ToastHost)] }) : view;
  } });
  app.use(pinia);
  app.use(i18n);
  const router = routerModule.createRouter({ history: routerModule.createMemoryHistory(), routes: [
    { path: '/', component: { render: () => null } },
    { path: '/display', component: { render: () => null } },
    { path: '/ppt-focus/:windowId', component: { render: () => null } },
  ] });
  app.use(router);
  await router.push(component === PlaybackControl ? '/display' : '/ppt-focus/1');
  const host = browser.document.createElement('div');
  browser.document.body.append(host);
  app.mount(host);
  await flushUi();
  return sessions;
}

/** :returns: 当前页图的绝对URL，保留实际源40与fit布局身份。 */
export function fittedImageUrl(page) {
  return new URL(`/api/sources/40/slides/${page}/?fit_text=true`, browser.location.origin).href;
}

/** :returns: 本轮实际资源请求，仅白名单路径/布局，无凭据。 */
export function resourceRequests() {
  return requests.filter((request) => request.pathname.endsWith('/ppt-resources/'))
    .map((request) => request.pathname + request.search);
}

/** :returns: 只读外部HTTP记录副本，可确认刷新不发任何控制写请求。 */
export function recordedRequests() {
  return requests.map((request) => ({ ...request }));
}

/** :returns: 外部HTTP响应门闩，不暴露组件或API内部方法。 */
export function responseGate() {
  let resolve;
  const promise = new Promise((complete) => { resolve = complete; });
  return { promise, resolve };
}

/** :param rows: 该布局的HTTP资源。:param status: HTTP状态。:returns: 真实Response供fetch边界返回。 */
export function resourceResponse(rows, status = 200) {
  return new Response(JSON.stringify(status === 200 ? { success: true, resources: rows }
    : { detail: '旧布局资源已不可用' }), { status, headers: { 'Content-Type': 'application/json' } });
}

/**
 * 通过公开SSE合并入口更新观测，不修改组件私有refs。
 * :param sessions: 当前实际Pinia store。
 * :param patch: 新的实际观测字段。
 * :returns: 新观测已经渲染并触发其资源请求。
 */
export async function observe(sessions, patch) {
  sessions.applyRemoteSessions([{ ...sessions.byWindowId(1), ...patch,
    last_updated_at: `2026-10-03T00:00:${String(++observationNumber).padStart(2, '0')}Z` }]);
  await flushUi();
}

/** :param component: 实际挂载的页面。:returns: 当前页预览的公开DOM URL。 */
export function currentImage(component) {
  return browser.document.querySelector(component === PlaybackControl
    ? '.playback-control__monitor img' : '.ppt-focus__current img')?.src ?? '';
}

/**
 * 在浏览器timer外部接缝控制一秒任务，包含轮询与按钮波纹清理，不假定全部为HTTP轮询。
 * :returns: 可显式触发的一秒任务及只读安排历史；其它延迟保持实际行为。
 */
export function pollClock() {
  const scheduled = [];
  const tasks = new Map();
  let nextId = 1000000;
  browser.setTimeout = (callback, delay, ...args) => {
    if (delay !== 1000) return originalWindowTimeout(callback, delay, ...args);
    const id = ++nextId;
    scheduled.push(id);
    tasks.set(id, () => callback(...args));
    return id;
  };
  browser.clearTimeout = (id) => {
    if (tasks.delete(id)) return;
    originalWindowClearTimeout(id);
  };
  return {
    scheduled,
    /** :returns: 当前是否仍有外部一秒任务，不能以数量直接推断HTTP预算。 */
    hasPending() { return tasks.size > 0; },
    /** :returns: 触发一个实际已安排的一秒任务，不调用组件私有loader。 */
    fire() {
      const [id, callback] = tasks.entries().next().value ?? [];
      assert.ok(callback, '应已经安排下一项一秒任务');
      tasks.delete(id);
      callback();
    },
  };
}

/** :returns: 经Vue公开生命周期卸载页面，不读写组件私有状态。 */
export function unmountPreview() {
  app?.unmount();
  app = undefined;
}
