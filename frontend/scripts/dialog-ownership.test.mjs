/** 确认意图的页面/主机/账号归属回归；仅Promise和内存store，不发送设备命令。 */
import assert from 'node:assert/strict';
import test, { before, after } from 'node:test';
import { createDomTestServer } from './dom-test-server.mjs';
import { effectScope } from 'vue';
import { createPinia, setActivePinia } from 'pinia';
let server, useDialog, useDialogStore, useRuntimeStore, useAuthStore;
before(async () => {
  server = await createDomTestServer();
  ({ useDialog, useDialogStore } = await server.ssrLoadModule('/src/composables/useDialog.ts'));
  ({ useRuntimeStore } = await server.ssrLoadModule('/src/stores/runtime.ts'));
  ({ useAuthStore } = await server.ssrLoadModule('/src/stores/auth.ts'));
});
after(async () => { await server.close(); });
/** :returns: 独立页面作用域和确认宿主。 */
function setup() {
  setActivePinia(createPinia());
  const scope = effectScope();
  const dialog = scope.run(() => useDialog());
  return { scope, dialog, store: useDialogStore() };
}
for (const change of ['unmount', 'host', 'user']) {
  test(`未确认时${change}取消旧意图，不保留悬空确认`, async () => {
    const { scope, dialog, store } = setup();
    const result = dialog.danger({ title: '旧目标' });
    if (change === 'unmount') scope.stop();
    else if (change === 'host') useRuntimeStore().connectionGeneration++;
    else useAuthStore().user = { id: 2 };
    assert.equal(store.open, false);
    store.accept();
    assert.equal(await result, false);
    scope.stop();
  });
}
test('旧页面卸载不能取消另一个页面新开的确认', async () => {
  const { scope, dialog, store } = setup();
  const first = dialog.confirm({ title: '旧确认' });
  const newer = effectScope();
  const other = newer.run(() => useDialog());
  const second = other.confirm({ title: '新确认' });
  scope.stop();
  assert.equal(await first, false);
  assert.equal(store.open, true);
  store.accept();
  assert.equal(await second, true);
  newer.stop();
});
test('确认后同一轮切换主机，调用方复核不能提交旧动作', async () => {
  const { scope, dialog, store } = setup();
  const current = dialog.captureContext();
  const result = dialog.confirm({ title: '关机' });
  store.accept();
  useRuntimeStore().connectionGeneration++;
  let calls = 0;
  if (await result && current()) calls++;
  assert.equal(calls, 0);
  scope.stop();
});

test('相同ID重新认证也取消旧会话的确认意图', async () => {
  const { scope, dialog, store } = setup();
  useAuthStore().user = { id: 1, username: 'first' };
  const result = dialog.confirm({ title: '原会话确认' });
  useAuthStore().user = { id: 1, username: 'new-session' };
  assert.equal(store.open, false);
  assert.equal(await result, false);
  scope.stop();
});
