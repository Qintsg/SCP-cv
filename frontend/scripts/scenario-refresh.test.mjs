/** 预设真实Pinia/HTTP刷新回归：完整快照、失败保留与主机/账号寿命，不连接现场。 */
import assert from 'node:assert/strict';
import { setImmediate as nextTurn } from 'node:timers/promises';
import test, { before, after, afterEach } from 'node:test';
import { Window } from 'happy-dom';
import { createDomTestServer } from './dom-test-server.mjs';

const browser = new Window({ url: 'http://localhost:5173/scenarios' });
const globals = ['window', 'document', 'navigator', 'localStorage'];
const previous = new Map(globals.map(name => [name, Object.getOwnPropertyDescriptor(globalThis, name)]));
for (const name of globals) Object.defineProperty(globalThis, name, {
  configurable: true, writable: true, value: browser[name],
});
const priorFetch = globalThis.fetch;
let server, createPinia, useScenarioStore, useAuthStore, connection;

before(async () => {
  ({ createPinia } = await import('pinia'));
  server = await createDomTestServer();
  ({ useScenarioStore } = await server.ssrLoadModule('/src/stores/scenarios.ts'));
  ({ useAuthStore } = await server.ssrLoadModule('/src/stores/auth.ts'));
  ({ clientConnection: connection } = await server.ssrLoadModule('/src/platform/connection.ts'));
});
afterEach(() => { globalThis.fetch = priorFetch; });
after(async () => {
  await server?.close(); await browser.happyDOM.close();
  for (const [name, descriptor] of previous) {
    if (descriptor) Object.defineProperty(globalThis, name, descriptor); else delete globalThis[name];
  }
});

/** :param id: 快照标识。:returns: 可区分同次列表与目录的API数据。 */
function snapshot(id) {
  return { scenarios: [{ id, name: `预设${id}`, folder_id: id, sort_order: 0 }],
    folders: [{ id, name: `目录${id}`, parent_id: null }] };
}
/** :returns: 测试自行释放的外部响应，不改产品时限。 */
function deferred() {
  let resolve;
  const promise = new Promise(done => { resolve = done; });
  return { promise, resolve };
}
/** :param value: JSON正文。:param status: HTTP状态。:returns: 真实响应。 */
function response(value, status = 200) {
  return new Response(JSON.stringify(value), { status, headers: { 'Content-Type': 'application/json' } });
}
/** :returns: 有旧快照和明确账号的独立store。 */
function setup() {
  const pinia = createPinia(), store = useScenarioStore(pinia), auth = useAuthStore(pinia);
  auth.user = { id: 1, username: '测试用户', is_staff: true };
  Object.assign(store, snapshot(1));
  const rounds = [], counts = { scenarios: 0, folders: 0 };
  globalThis.fetch = async (url, init = {}) => {
    assert.equal(init.method ?? 'GET', 'GET', '刷新不得产生写操作');
    const path = new URL(String(url), browser.location.origin).pathname;
    const kind = path === '/api/scenarios/' ? 'scenarios' : path === '/api/scenario-folders/' ? 'folders' : null;
    assert.ok(kind, `未登记测试请求：${path}`);
    const round = rounds[counts[kind]++];
    assert.ok(round, `没有为${kind}准备响应`);
    return round[kind].promise;
  };
  return { store, auth, counts, round() {
    const result = { scenarios: deferred(), folders: deferred() }; rounds.push(result); return result;
  } };
}
/** :param round: 当前外部请求。:param id: 快照标识。:returns: 同次完整回应。 */
function complete(round, id) {
  const value = snapshot(id);
  round.scenarios.resolve(response({ scenarios: value.scenarios }));
  round.folders.resolve(response({ folders: value.folders }));
}

test('列表先返回时仍保留完整旧快照，目录完成后一次提交并结束加载', async () => {
  const fixture = setup(), round = fixture.round(), pending = fixture.store.refresh();
  const initialLoading = fixture.store.loading;
  round.scenarios.resolve(response({ scenarios: snapshot(2).scenarios }));
  await nextTurn(); await nextTurn();
  const interim = JSON.stringify({ scenarios: fixture.store.scenarios, folders: fixture.store.folders });
  round.folders.resolve(response({ folders: snapshot(2).folders })); await pending;
  assert.equal(interim, JSON.stringify(snapshot(1)), '不能发布新列表与旧目录的混合状态');
  assert.equal(initialLoading, true); assert.equal(fixture.store.loading, false);
  assert.deepEqual({ scenarios: fixture.store.scenarios, folders: fixture.store.folders }, snapshot(2));
  assert.equal(fixture.store.loadError, '');
  assert.deepEqual(fixture.counts, { scenarios: 1, folders: 1 });
});

test('目录读取失败保留原列表与目录和持久错误，明确重试成功才清除错误', async () => {
  const fixture = setup(), first = fixture.round();
  const pending = fixture.store.refresh().catch(error => error);
  first.scenarios.resolve(response({ scenarios: snapshot(2).scenarios }));
  first.folders.resolve(response({ detail: '目录暂时不可用' }, 503));
  const failure = await pending;
  const failed = { snapshot: JSON.stringify({ scenarios: fixture.store.scenarios, folders: fixture.store.folders }),
    error: fixture.store.loadError, loading: fixture.store.loading };
  const retry = fixture.round(), retryPending = fixture.store.refresh();
  const retryError = fixture.store.loadError;
  complete(retry, 3); await retryPending;
  assert.match(failure.message, /目录暂时不可用/);
  assert.equal(failed.snapshot, JSON.stringify(snapshot(1)));
  assert.equal(failed.error, '目录暂时不可用'); assert.equal(failed.loading, false);
  assert.equal(retryError, failed.error, '加载新结果前保留失败原因');
  assert.equal(fixture.store.loadError, ''); assert.equal(fixture.store.scenarios[0].id, 3);
});

test('较早成功响应不能覆盖后来成功的完整快照', async () => {
  const fixture = setup(), oldRound = fixture.round(), currentRound = fixture.round();
  const old = fixture.store.refresh(), current = fixture.store.refresh();
  complete(currentRound, 3); await current;
  complete(oldRound, 2); await old;
  assert.deepEqual({ scenarios: fixture.store.scenarios, folders: fixture.store.folders }, snapshot(3));
  assert.equal(fixture.store.loading, false); assert.equal(fixture.store.loadError, '');
});

test('较早失败不覆盖后来成功，较早成功也不清除后来失败原因', async () => {
  for (const lateFailure of [true, false]) {
    const fixture = setup(), oldRound = fixture.round(), currentRound = fixture.round();
    const old = fixture.store.refresh().catch(error => error), current = fixture.store.refresh().catch(error => error);
    if (lateFailure) complete(currentRound, 3);
    else {
      currentRound.scenarios.resolve(response({ scenarios: snapshot(3).scenarios }));
      currentRound.folders.resolve(response({ detail: '最新目录失败' }, 503));
    }
    await current;
    if (lateFailure) {
      oldRound.scenarios.resolve(response({ scenarios: snapshot(2).scenarios }));
      oldRound.folders.resolve(response({ detail: '旧目录失败' }, 503));
    } else complete(oldRound, 2);
    await old;
    assert.equal(fixture.store.scenarios[0].id, lateFailure ? 3 : 1);
    assert.equal(fixture.store.folders[0].id, lateFailure ? 3 : 1);
    assert.equal(fixture.store.loadError, lateFailure ? '' : '最新目录失败');
  }
});

test('reset后旧finally不能解除新读取，也不能覆盖新账号的数据', async () => {
  const fixture = setup(), oldRound = fixture.round(), currentRound = fixture.round();
  const old = fixture.store.refresh().catch(error => error);
  fixture.store.$reset(); fixture.auth.user = { id: 2, username: '另一账号' };
  const current = fixture.store.refresh();
  complete(oldRound, 2); await old;
  const loading = fixture.store.loading, data = [...fixture.store.scenarios];
  complete(currentRound, 3); await current;
  assert.equal(loading, true); assert.deepEqual(data, []);
  assert.equal(fixture.store.scenarios[0].id, 3); assert.equal(fixture.store.loading, false);
});

for (const boundary of ['主机', '账号', '重置']) {
  test(`${boundary}变化后的旧读取不能写回快照或失败提示`, async () => {
    for (const fails of [true, false]) {
      const fixture = setup(), round = fixture.round(), pending = fixture.store.refresh().catch(error => error);
      if (boundary === '主机') connection.restoreProfile({ origin: 'http://127.0.0.1:18993', displayName: '隔离主机' });
      if (boundary === '账号') fixture.auth.user = { id: 2, username: '另一账号' };
      if (boundary === '重置') fixture.store.$reset();
      if (fails) {
        round.scenarios.resolve(response({ scenarios: snapshot(2).scenarios }));
        round.folders.resolve(response({ detail: '旧读取失败' }, 503));
      } else complete(round, 2);
      await pending;
      assert.deepEqual({ scenarios: fixture.store.scenarios, folders: fixture.store.folders },
        boundary === '重置' ? { scenarios: [], folders: [] } : snapshot(1));
      assert.equal(fixture.store.loadError, '');
    }
  });
}

for (const identity of ['其他账号', '同账号退出重登']) {
  test(`${identity}开始刷新就清除上一会话快照和错误，新读取失败不保留旧库`, async () => {
    const fixture = setup(), initial = fixture.round(), initialized = fixture.store.refresh();
    complete(initial, 2); await initialized;
    const failedRound = fixture.round(), failed = fixture.store.refresh().catch(error => error);
    failedRound.scenarios.resolve(response({ scenarios: snapshot(3).scenarios }));
    failedRound.folders.resolve(response({ detail: '旧会话目录读取失败' }, 503));
    await failed;
    assert.equal(fixture.store.scenarios[0].id, 2);
    assert.equal(fixture.store.loadError, '旧会话目录读取失败');

    fixture.auth.user = null;
    fixture.auth.user = { id: identity === '其他账号' ? 2 : 1, username: '新会话' };
    const currentRound = fixture.round(), current = fixture.store.refresh().catch(error => error);
    const started = { scenarios: [...fixture.store.scenarios], folders: [...fixture.store.folders],
      error: fixture.store.loadError, loading: fixture.store.loading };
    currentRound.scenarios.resolve(response({ scenarios: snapshot(4).scenarios }));
    currentRound.folders.resolve(response({ detail: '新会话读取失败' }, 503));
    await current;
    assert.deepEqual(started, { scenarios: [], folders: [], error: '', loading: true });
    assert.deepEqual(fixture.store.scenarios, []); assert.deepEqual(fixture.store.folders, []);
    assert.equal(fixture.store.loadError, '新会话读取失败'); assert.equal(fixture.store.loading, false);
  });
}
