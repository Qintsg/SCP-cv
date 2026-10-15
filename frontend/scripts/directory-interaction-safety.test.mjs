/** 目录选择、确认快照、预设失败和键盘预览回归；全部请求使用内存替身。 */
import assert from 'node:assert/strict';
import test, { after, afterEach, before } from 'node:test';
import { setImmediate as nextTurn } from 'node:timers/promises';
import { Window } from 'happy-dom';
import { createDomTestServer } from './dom-test-server.mjs';

const browser = new Window({ url: 'http://localhost:5173/scenarios' });
const names = ['window', 'document', 'navigator', 'HTMLElement', 'HTMLInputElement', 'HTMLTextAreaElement',
  'Element', 'Node', 'SVGElement', 'Document', 'MutationObserver', 'ResizeObserver', 'Image', 'Event', 'MouseEvent', 'PointerEvent',
  'getComputedStyle', 'requestAnimationFrame', 'cancelAnimationFrame'];
const globals = new Map(names.map(name => [name, Object.getOwnPropertyDescriptor(globalThis, name)]));
for (const name of names) Object.defineProperty(globalThis, name, { configurable: true, writable: true,
  value: typeof browser[name] === 'function' && /^[a-z]/.test(name) ? browser[name].bind(browser) : browser[name] });
const originalFetch = globalThis.fetch;
let server, vue, createPinia, i18n, useSources, useScenarios, useDialog, useSelection, Tools, Scenarios, directoryApi;
let app, root, pinia, restore = [], renderErrors = [];
before(async () => {
  [vue, { createPinia }, server] = await Promise.all([import('vue'), import('pinia'), createDomTestServer()]);
  ({ i18n } = await server.ssrLoadModule('/src/locales/index.ts'));
  ({ useSourceStore: useSources } = await server.ssrLoadModule('/src/stores/sources.ts'));
  ({ useScenarioStore: useScenarios } = await server.ssrLoadModule('/src/stores/scenarios.ts'));
  ({ useDialogStore: useDialog } = await server.ssrLoadModule('/src/composables/useDialog.ts'));
  ({ useSourceSelection: useSelection } = await server.ssrLoadModule('/src/features/sources/useSourceSelection.ts'));
  ({ scenarioDirectoryApi: directoryApi } = await server.ssrLoadModule('/src/services/scenarioDirectoryApi.ts'));
  ({ default: Tools } = await server.ssrLoadModule('/src/features/scenarios/ScenarioDirectoryTools.vue'));
  ({ default: Scenarios } = await server.ssrLoadModule('/src/features/scenarios/ScenariosView.vue'));
});
afterEach(async () => {
  useDialog(pinia)?.cancel(); app?.unmount(); app = undefined; await flush();
  restore.forEach(fn => fn()); restore = []; browser.document.body.replaceChildren();
  await browser.happyDOM.abort(); globalThis.fetch = originalFetch;
});
after(async () => {
  await server?.close(); await browser.happyDOM.close();
  for (const [name, descriptor] of globals) {
    if (descriptor) Object.defineProperty(globalThis, name, descriptor); else delete globalThis[name];
  }
});
/** :returns: 排空本轮响应与组件更新，不依赖延时。 */
async function flush() { await vue.nextTick(); await nextTurn(); await vue.nextTick(); }
/** :returns: 可由测试明确完成的响应。 */
function deferred() { let resolve; const promise = new Promise(done => { resolve = done; }); return { promise, resolve }; }
/** :param object: 接口对象。:param key: 动作。:param replacement: 替身。:returns: 测试结束恢复真实方法。 */
function replace(object, key, replacement) { const previous = object[key]; object[key] = replacement; restore.push(() => { object[key] = previous; }); }
/** :param component: 真实组件或持有composable的最小宿主。:returns: 独立store与DOM。 */
async function mount(component) {
  pinia = createPinia(); globalThis.fetch = async () => { throw new Error('本测试不允许网络请求'); };
  renderErrors = [];
  root = browser.document.createElement('div'); browser.document.body.append(root);
  app = vue.createApp(component); app.config.errorHandler = error => { renderErrors.push(error.stack ?? String(error)); };
  app.use(pinia); app.use(i18n); app.mount(root); await flush();
}
/** :param id: 测试项编号。:returns: 可完整渲染的预设。 */
function scenario(id) { return { id, name: id === 1 ? '迎宾' : '散场', path: id === 1 ? '/迎宾' : '/散场',
  folder_id: null, sort_order: 0, description: '', targets: [], advanced: {}, big_screen_mode: 'single',
  big_screen_mode_state: 'unset', big_screen_mode_label: '', volume_state: 'unset', volume_level: 50,
  updated_at: '2026-10-15T01:00:00Z' }; }
/** :param text: 可见按钮名称。:returns: 当前真实按钮。 */
function button(text) { return [...browser.document.querySelectorAll('button')].find(item => item.textContent.trim() === text); }
/** :returns: 两个普通源和可直接操作的选择器。 */
async function mountSelection() {
  let selection;
  await mount({ setup() { selection = useSelection(); return () => vue.h('div'); } });
  const store = useSources(pinia);
  store.sources = [{ id: 1, name: 'A', folder_id: null, source_type: 'image', path: '/A.png' },
    { id: 2, name: 'B', folder_id: null, source_type: 'video', path: '/B.mp4' }];
  await flush(); return { store, selection, dialog: useDialog(pinia) };
}
/** :returns: 受控父组件，可模拟筛选、目录与目的地在请求期间变化。 */
async function mountTools() {
  const selected = vue.ref([1, 2]), visible = vue.ref([1, 2]), folder = vue.ref(null), search = vue.ref('');
  await mount({ setup() { return () => vue.h(Tools, { selected: selected.value, visibleIds: visible.value,
    folderId: folder.value, search: search.value, 'onUpdate:selected': value => { selected.value = value; },
    'onUpdate:folderId': value => { folder.value = value; }, 'onUpdate:search': value => { search.value = value; } }); } });
  const store = useScenarios(pinia); store.scenarios = [scenario(1), scenario(2)];
  store.folders = [{ id: 10, name: '目标', parent_id: null }]; store.refresh = async () => {};
  await flush(); return { selected, visible, folder, search, store, dialog: useDialog(pinia) };
}

test('媒体搜索及类型变化剔除不可见选择，内置源不能保留为批量对象', async () => {
  const { store, selection } = await mountSelection(); selection.toggle(1, true);
  store.setSearchKeyword('B'); await flush(); assert.deepEqual(selection.selected.value, []);
  store.setSearchKeyword(''); selection.toggle(2, true); store.setCategory('image'); await flush();
  assert.deepEqual(selection.selected.value, []);
  selection.toggle(1, true); store.sources[0].is_protected = true; await flush();
  assert.deepEqual(selection.selected.value, []);
});

test('媒体确认等待期间改变范围会使旧意图失效且不会误用后来选择', async () => {
  const { store, selection, dialog } = await mountSelection(); const deleted = [];
  store.deleteSource = async id => { deleted.push(id); }; selection.toggle(1, true);
  const pending = selection.execute('delete'); await flush();
  assert.match(dialog.config.description, /\/A.png/); assert.equal(selection.busy.value, true);
  store.setSearchKeyword('B'); await flush(); dialog.accept(); await pending;
  assert.deepEqual(deleted, []); assert.equal(selection.busy.value, false);
});

test('媒体批量开始后切目录保留已确认批次，逐项失败并清理旧选择', async () => {
  const { store, selection, dialog } = await mountSelection(); const gate = deferred(), deleted = [];
  store.deleteSource = async id => { deleted.push(id); if (id === 1) await gate.promise; else throw new Error('正在使用'); };
  selection.toggle(1, true); selection.toggle(2, true); const pending = selection.execute('delete');
  dialog.accept(); await flush(); store.currentFolderId = 10; await flush(); gate.resolve(); await pending; await flush();
  assert.deepEqual(deleted, [1, 2]); assert.deepEqual(selection.selected.value, []);
  assert.deepEqual(selection.results.value.map(item => item.success), [true, false]);
  assert.match(selection.results.value[1].detail, /正在使用/);
});

test('媒体移动固定确认目标，重复点击不会创建第二批请求', async () => {
  const { store, selection, dialog } = await mountSelection(); const moved = [], gate = deferred();
  store.folders = [{ id: 10, name: '目标', parent_id: null, relative_path: '/目标' }];
  store.moveSource = async (id, target) => { moved.push([id, target]); await gate.promise; };
  selection.toggle(1, true); selection.destination.value = 10; const pending = selection.execute('move');
  assert.match(dialog.config.description, /目标：\/目标/);
  await selection.execute('move'); selection.destination.value = 0; dialog.accept(); await flush();
  assert.deepEqual(moved, [[1, 10]]); gate.resolve(); await pending;
});

test('预设筛选收窄时清除隐藏选择，删除确认列出实际路径', async () => {
  const { selected, visible, dialog, store } = await mountTools(); const deleted = [];
  store.remove = async id => { deleted.push(id); }; visible.value = [2]; await flush();
  assert.deepEqual(selected.value, [2]); button('删除所选').click(); await flush();
  assert.match(dialog.config.description, /\/散场/); assert.doesNotMatch(dialog.config.description, /\/迎宾/);
  dialog.cancel(); await flush(); assert.deepEqual(deleted, []);
});

test('预设确认期间目录变化取消旧删除，固定对象不受后续选择替换', async () => {
  const { selected, folder, dialog, store } = await mountTools(); const deleted = [];
  store.remove = async id => { deleted.push(id); }; button('删除所选').click(); await flush();
  folder.value = 10; selected.value = [2]; await flush(); dialog.accept(); await flush();
  assert.deepEqual(deleted, []);
});

test('预设移动先确认完整对象及目标，取消不发送', async () => {
  const { dialog } = await mountTools(); const moved = [];
  replace(directoryApi, 'move', async (...args) => { moved.push(args); }); button('移动到…').click(); await flush();
  assert.equal(dialog.open, true); assert.match(dialog.config.description, /\/迎宾/);
  assert.match(dialog.config.description, /\/散场/); assert.match(dialog.config.description, /目标：\//);
  dialog.cancel(); await flush(); assert.deepEqual(moved, []);
});

test('预设确认的批次不会被确认期间的外部改选替换', async () => {
  const { selected, dialog, store } = await mountTools(); const deleted = [];
  store.remove = async id => { deleted.push(id); }; selected.value = [1]; await flush();
  button('删除所选').click(); await flush(); assert.match(dialog.config.description, /\/迎宾/);
  selected.value = [2]; await flush(); dialog.accept(); await flush();
  assert.deepEqual(deleted, [1]); assert.deepEqual(selected.value, [2]);
});

test('预设执行中切目录仍完成固定批次并逐项呈现失败，清理隐藏选择', async () => {
  const { selected, visible, folder, dialog, store } = await mountTools(); const deleted = [], gate = deferred();
  store.remove = async id => { deleted.push(id); if (id === 1) await gate.promise; else throw new Error('文件被使用'); };
  button('删除所选').click(); await flush(); dialog.accept(); await flush();
  assert.deepEqual(deleted, [1]); folder.value = 10; visible.value = []; await flush();
  gate.resolve(); await flush(); await flush();
  assert.deepEqual(deleted, [1, 2]); assert.deepEqual(selected.value, []);
  assert.match(root.textContent, /\/迎宾：已删除/); assert.match(root.textContent, /\/散场：文件被使用/);
});

test('预设错误状态优先于空目录且仍显示已有快照', async () => {
  await mount(Scenarios); const store = useScenarios(pinia);
  store.loadError = '目录读取失败'; await flush();
  assert.match(root.textContent, /预设列表读取失败/); assert.doesNotMatch(root.textContent, /当前目录没有预设/);
  assert.ok(button('重新读取')); store.scenarios = [scenario(1)]; await flush();
  assert.match(root.textContent, /预设列表读取失败/); assert.match(root.textContent, /迎宾/);
});

test('预设预览通过原生按钮进入，关闭还焦点且不激活', async () => {
  await mount(Scenarios); const store = useScenarios(pinia); store.scenarios = [scenario(1)];
  let activations = 0; store.activate = async () => { activations++; }; await flush();
  const trigger = root.querySelector('button[aria-label="查看预设 迎宾"]'); assert.ok(trigger);
  trigger.focus(); trigger.click(); await flush(); assert.deepEqual(renderErrors, []); assert.ok(browser.document.querySelector('.n-drawer'));
  button('关闭').click(); await flush(); await flush();
  assert.equal(browser.document.activeElement, trigger); assert.equal(activations, 0);
});
