/*
 * 文稿依赖设置真实DOM合同：逐项原因与静态检查边界必须可见。
 * 使用真实Vue/Naive UI/API，只在外部fetch边界提供诊断响应，不启动Office。
 * @Project : SCP-cv
 * @File : presentation-settings.test.mjs
 * @Author : Qintsg
 * @Date : 2026-10-03
 */
import assert from 'node:assert/strict';
import { setImmediate as nextTurn } from 'node:timers/promises';
import test, { after, afterEach, before } from 'node:test';
import { Window } from 'happy-dom';
import { createDomTestServer } from './dom-test-server.mjs';

const browser = new Window({ url: 'http://localhost:5173/settings' });
const globalNames = ['window', 'document', 'navigator', 'HTMLElement', 'HTMLInputElement',
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
let apiModule;
let useRuntimeStore;
let PresentationSettings;
let app;
let requests;
let requestOrigins;

before(async () => {
  [vue, { createPinia }, server] = await Promise.all([import('vue'), import('pinia'), createDomTestServer()]);
  ({ default: PresentationSettings } = await server.ssrLoadModule('/src/features/settings/tabs/PresentationSettings.vue'));
  apiModule = await server.ssrLoadModule('/src/services/api.ts');
  ({ useRuntimeStore } = await server.ssrLoadModule('/src/stores/runtime.ts'));
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

/** :returns: 当前Vue渲染与已完成HTTP响应回调已结束。 */
async function flushUi() {
  await vue.nextTick();
  await nextTurn();
  await vue.nextTick();
}

/**
 * 返回既有overall字段与协调后的组件诊断合同，不读取真实安装或配置值。
 * :param available: 是否静态依赖检查通过。
 * :param components: 各组件的只读诊断。
 * :returns: 无路径/凭据的测试HTTP响应。
 */
function settingsResponse(available, components) {
  return new Response(JSON.stringify({ success: true, settings: {
    default_engine: 'libreoffice', available,
    dependency_status: available ? 'installed' : 'unavailable',
    detail: available ? '静态运行依赖检查通过。' : '存在缺失或不兼容的运行依赖。',
    components,
    capabilities: { slide_navigation: true, effect_navigation: true, media_pause: false,
      media_resume: false, media_seek: false, media_volume: false, media_mute: false },
  } }), { headers: { 'Content-Type': 'application/json' } });
}

/**
 * 挂载实际设置卡片，诊断仅从公开GET的外部fetch响应取得。
 * :param respond: 本轮外部HTTP响应器。
 * :returns: 卡片及初始HTTP响应已经渲染。
 */
async function mountSettings(respond) {
  requests = [];
  requestOrigins = [];
  globalThis.fetch = async (url, options = {}) => {
    const target = new URL(String(url), browser.location.origin);
    const path = target.pathname;
    requests.push({ path, method: options.method ?? 'GET' });
    if (path === '/api/auth/logout/') return new Response(JSON.stringify({ success: true }), {
      headers: { 'Content-Type': 'application/json' },
    });
    assert.equal(path, '/api/settings/presentation/');
    requestOrigins.push(target.origin);
    return respond();
  };
  const host = browser.document.createElement('div');
  browser.document.body.append(host);
  app = vue.createApp(PresentationSettings);
  const pinia = createPinia();
  app.use(pinia);
  app.mount(host);
  await flushUi();
  return useRuntimeStore(pinia);
}

test('总览只有通用缺失说明时，真实设置仍显示缺CLI组件的具体原因和静态检查边界', async () => {
  await mountSettings(() => settingsResponse(false, [
    { id: 'framework', status: 'installed', detail: 'Framework运行依赖静态检查通过。', reason_code: '' },
    { id: 'cli_oootypes', status: 'unavailable',
      detail: '缺少官方.NET互操作组件cli_oootypes，请用LibreOffice安装器补装该组件。', reason_code: 'cli_missing' },
  ]));
  const body = browser.document.querySelector('.presentation-settings')?.textContent ?? '';
  assert.match(body, /cli_oootypes/);
  assert.match(body, /缺少官方\.NET互操作组件cli_oootypes/);
  assert.match(body, /cli_missing/);
  assert.match(body, /缺失或不兼容/);
  assert.match(body, /静态检查.*不证明.*播放/);
  assert.deepEqual(requests, [{ path: '/api/settings/presentation/', method: 'GET' }]);
});

for (const failureKind of ['network', 'http']) {
  test(`成功静态检查后${failureKind}刷新失败应使旧snapshot失效，不保留通过标签与旧组件`, async () => {
    let count = 0;
    await mountSettings(() => {
      if (++count === 1) return settingsResponse(true, [{ id: 'framework', status: 'installed',
        detail: '上次完成的Framework检查', reason_code: '' }]);
      if (failureKind === 'network') throw new TypeError('QA network unavailable');
      return new Response(JSON.stringify({ detail: 'QA诊断读取失败' }), {
        status: 503, headers: { 'Content-Type': 'application/json' },
      });
    });
    assert.match(browser.document.querySelector('.presentation-settings .n-tag')?.textContent ?? '', /静态检查通过/);
    const refresh = [...browser.document.querySelectorAll('button')]
      .find((button) => button.textContent.trim() === '重新检查');
    assert.ok(refresh);
    refresh.click();
    await flushUi();
    const summary = browser.document.querySelector('.presentation-settings .n-tag')?.textContent ?? '';
    assert.doesNotMatch(summary, /静态检查通过/);
    assert.match(summary, /待检查|未确认/);
    assert.equal(browser.document.querySelector('[data-dependency-id="framework"]'), null);
    const alerts = [...browser.document.querySelectorAll('.presentation-settings [role="alert"]')]
      .map((alert) => alert.textContent);
    assert.ok(alerts.some((text) => failureKind === 'http' ? text.includes('QA诊断读取失败')
      : /连接|网络/.test(text)), '应保留本次读取失败反馈');
    assert.equal(count, 2);
  });
}

test('声明installed但缺少必需components的响应不能显示静态检查通过', async () => {
  const valid = await settingsResponse(true, [{ id: 'framework', status: 'installed',
    detail: '静态检查通过', reason_code: '' }]).json();
  delete valid.settings.components;
  await mountSettings(() => new Response(JSON.stringify(valid), {
    headers: { 'Content-Type': 'application/json' },
  }));
  const summary = browser.document.querySelector('.presentation-settings .n-tag')?.textContent ?? '';
  assert.doesNotMatch(summary, /静态检查通过/);
  assert.match(summary, /待检查|未确认/);
  assert.ok([...browser.document.querySelectorAll('[role="alert"]')]
    .some((alert) => /诊断.*无效|检查结果.*无效/.test(alert.textContent)), '结构不完整应明确反馈，不能当检查完成');
});

const malformedCases = [
  ['组件为空', (settings) => { settings.components = []; }],
  ['组件非数组', (settings) => { settings.components = {}; }],
  ['组件条目为空', (settings) => { settings.components = [null]; }],
  ['组件ID为空白', (settings) => { settings.components[0].id = ' '; }],
  ['组件状态非法', (settings) => { settings.components[0].status = 'ready'; }],
  ['组件状态不能从数组强转', (settings) => { settings.components[0].status = ['installed']; }],
  ['组件缺说明', (settings) => { delete settings.components[0].detail; }],
  ['组件缺原因码', (settings) => { delete settings.components[0].reason_code; }],
  ['available不是布尔', (settings) => { settings.available = 'true'; }],
  ['overall状态非法', (settings) => { settings.dependency_status = 'ready'; }],
  ['overall状态不能从数组强转', (settings) => { settings.dependency_status = ['installed']; }],
  ['available与overall矛盾', (settings) => { settings.available = false; }],
  ['存在unavailable组件却声明通过', (settings) => { settings.components[0].status = 'unavailable'; }],
  ['组件ID重复', (settings) => { settings.components.push({ ...settings.components[0] }); }],
  ['缺少能力结构', (settings) => { delete settings.capabilities; }],
  ['能力字段不是布尔', (settings) => { settings.capabilities.media_seek = 'false'; }],
  ['默认引擎不符当前合同', (settings) => { settings.default_engine = 'powerpoint'; }],
];

for (const [label, corrupt] of malformedCases) {
  test(`诊断合同保护：${label}保持未知，不采纳错误snapshot`, async () => {
    const payload = await settingsResponse(true, [{ id: 'framework', status: 'installed',
      detail: '静态检查通过', reason_code: '' }]).json();
    corrupt(payload.settings);
    await mountSettings(() => new Response(JSON.stringify(payload), { headers: { 'Content-Type': 'application/json' } }));
    const summary = browser.document.querySelector('.presentation-settings .n-tag')?.textContent ?? '';
    assert.doesNotMatch(summary, /静态检查通过/);
    assert.match(summary, /待检查|未确认/);
    assert.equal(browser.document.querySelector('[data-dependency-id="framework"]'), null);
    assert.ok([...browser.document.querySelectorAll('[role="alert"]')]
      .some((alert) => /诊断.*无效/.test(alert.textContent)));
  });
}

test('公开切换主机后Alpha迟到诊断的stale错误不能污染Beta未知界面，也不自动重放读取', async () => {
  apiModule.configureApiServer({ origin: 'https://alpha.example.test', displayName: 'Alpha' });
  let resolveOld;
  const oldResponse = new Promise((resolve) => { resolveOld = resolve; });
  let checks = 0;
  const runtime = await mountSettings(() => {
    checks++;
    if (checks === 2) return oldResponse;
    return settingsResponse(true, [{ id: checks === 1 ? 'alpha_component' : 'beta_component',
      status: 'installed', detail: '只读静态诊断', reason_code: '' }]);
  });
  assert.ok(browser.document.querySelector('[data-dependency-id="alpha_component"]'));
  const button = [...browser.document.querySelectorAll('button')]
    .find((candidate) => candidate.textContent.trim() === '重新检查');
  button.click();
  await flushUi();
  await runtime.switchServer({ origin: 'https://beta.example.test', displayName: 'Beta' });
  await flushUi();
  resolveOld(settingsResponse(true, [{ id: 'alpha_component', status: 'installed',
    detail: 'Alpha迟到结果', reason_code: '' }]));
  await flushUi();
  assert.equal(browser.document.querySelector('[data-dependency-id="alpha_component"]'), null);
  assert.doesNotMatch(browser.document.querySelector('.presentation-settings')?.textContent ?? '', /响应属于已切换的播放主机|Alpha迟到结果/);
  assert.equal(checks, 2, '切换主机不能自动重放诊断GET');
  assert.equal(button.disabled, false, '新主机可由用户显式重新检查');
  button.click();
  await flushUi();
  assert.ok(browser.document.querySelector('[data-dependency-id="beta_component"]'));
  assert.deepEqual(requestOrigins, ['https://alpha.example.test', 'https://alpha.example.test', 'https://beta.example.test']);
});

/** :returns: 可在外部HTTP边界明确结束的响应，不修改组件私有状态。 */
function deferredResponse() {
  let resolve;
  const promise = new Promise((complete) => { resolve = complete; });
  return { promise, resolve };
}

test('公开切换目标会清除已通过的旧本地诊断缓存，但不自动向新主机发送检查GET', async () => {
  apiModule.configureApiServer({ origin: 'https://alpha.example.test', displayName: 'Alpha' });
  let checks = 0;
  const runtime = await mountSettings(() => {
    checks++;
    return settingsResponse(true, [{ id: 'alpha_cached', status: 'installed', detail: 'Alpha已完成检查', reason_code: '' }]);
  });
  assert.ok(browser.document.querySelector('[data-dependency-id="alpha_cached"]'));
  await runtime.switchServer({ origin: 'https://beta.example.test', displayName: 'Beta' });
  await flushUi();
  assert.equal(browser.document.querySelector('[data-dependency-id="alpha_cached"]'), null);
  assert.doesNotMatch(browser.document.querySelector('.presentation-settings .n-tag')?.textContent ?? '', /静态检查通过/);
  assert.equal(checks, 1);
});

test('Alpha旧HTTP错误与finally不能清除Beta新检查的pending或写入错误', async () => {
  apiModule.configureApiServer({ origin: 'https://alpha.example.test', displayName: 'Alpha' });
  const old = deferredResponse();
  const current = deferredResponse();
  let checks = 0;
  const runtime = await mountSettings(() => ++checks === 1
    ? settingsResponse(true, [{ id: 'alpha_initial', status: 'installed', detail: 'Alpha初始检查', reason_code: '' }])
    : checks === 2 ? old.promise : current.promise);
  const button = [...browser.document.querySelectorAll('button')]
    .find((candidate) => candidate.textContent.trim() === '重新检查');
  button.click();
  await flushUi();
  await runtime.switchServer({ origin: 'https://beta.example.test', displayName: 'Beta' });
  await flushUi();
  button.click();
  await flushUi();
  assert.equal(checks, 3);
  assert.equal(button.disabled, true);
  old.resolve(new Response(JSON.stringify({ detail: 'Alpha旧HTTP读取失败' }), {
    status: 503, headers: { 'Content-Type': 'application/json' },
  }));
  await flushUi();
  assert.equal(button.disabled, true, 'Beta新读取仍在途，旧finally不能提前结束');
  assert.match(browser.document.querySelector('.presentation-settings .n-tag')?.textContent ?? '', /检查中/);
  assert.doesNotMatch(browser.document.querySelector('.presentation-settings')?.textContent ?? '', /Alpha旧HTTP|响应属于已切换/);
  current.resolve(settingsResponse(true, [{ id: 'beta_current', status: 'installed', detail: 'Beta当前检查', reason_code: '' }]));
  await flushUi();
  assert.equal(button.disabled, false);
  assert.ok(browser.document.querySelector('[data-dependency-id="beta_current"]'));
});

test('公开检查按钮快速重复点击只发送一个在途GET，完成后采用该结果', async () => {
  const pending = deferredResponse();
  let checks = 0;
  await mountSettings(() => ++checks === 1 ? settingsResponse(true, [{ id: 'initial', status: 'installed',
    detail: '初始检查', reason_code: '' }]) : pending.promise);
  const button = [...browser.document.querySelectorAll('button')]
    .find((candidate) => candidate.textContent.trim() === '重新检查');
  button.click();
  button.click();
  await flushUi();
  assert.equal(checks, 2, '初始GET加一次明确刷新，不重复下发');
  assert.equal(button.disabled, true);
  pending.resolve(settingsResponse(true, [{ id: 'refreshed', status: 'installed', detail: '已刷新检查', reason_code: '' }]));
  await flushUi();
  assert.equal(button.disabled, false);
  assert.ok(browser.document.querySelector('[data-dependency-id="refreshed"]'));
});

test('卸载旧卡片后迟到响应不会改变新实例诊断或安排额外读取', async () => {
  const old = deferredResponse();
  await mountSettings(() => old.promise);
  app.unmount();
  app = undefined;
  browser.document.body.replaceChildren();
  let checks = 0;
  await mountSettings(() => {
    checks++;
    return settingsResponse(true, [{ id: 'new_instance', status: 'installed', detail: '当前新实例检查', reason_code: '' }]);
  });
  old.resolve(settingsResponse(false, [{ id: 'old_instance', status: 'unavailable', detail: '旧实例迟到缺失', reason_code: 'old_missing' }]));
  await flushUi();
  assert.ok(browser.document.querySelector('[data-dependency-id="new_instance"]'));
  assert.equal(browser.document.querySelector('[data-dependency-id="old_instance"]'), null);
  assert.equal(checks, 1);
  assert.match(browser.document.querySelector('.presentation-settings .n-tag')?.textContent ?? '', /静态检查通过/);
});

test('HTTP200但success为false的诊断envelope不能采纳其完整installed设置', async () => {
  const payload = await settingsResponse(true, [{ id: 'framework', status: 'installed',
    detail: '不可采纳的检查结果', reason_code: '' }]).json();
  payload.success = false;
  await mountSettings(() => new Response(JSON.stringify(payload), {
    headers: { 'Content-Type': 'application/json' },
  }));
  const summary = browser.document.querySelector('.presentation-settings .n-tag')?.textContent ?? '';
  assert.doesNotMatch(summary, /静态检查通过/);
  assert.match(summary, /待检查|未确认/);
  assert.equal(browser.document.querySelector('[data-dependency-id="framework"]'), null);
  assert.ok([...browser.document.querySelectorAll('[role="alert"]')]
    .some((alert) => /诊断.*无效|检查结果.*无效/.test(alert.textContent)));
});
