/** 预案电视纯值与响应式克隆回归；不调用浏览器、设备或网络。 */
import assert from 'node:assert/strict';
import test, { before, after } from 'node:test';
import { reactive } from 'vue';
import { createDomTestServer } from './dom-test-server.mjs';
let server, model, helpers;
before(async () => {
  server = await createDomTestServer();
  model = await server.ssrLoadModule('/src/features/scenarios/scenarioModel.ts');
  helpers = await server.ssrLoadModule('/src/features/scenarios/scenarioTelevisions.ts');
});
after(async () => { await server?.close(); });
const existing = () => ({ id: 4, name: '电视配置', description: '', folder_id: null,
  big_screen_mode_state: 'unset', big_screen_mode: 'single', volume_state: 'unset', volume_level: 100, targets: [],
  advanced: { audio: { handheld_volume: 0 }, camera_positions: { rear: 3 }, televisions: {
    tv_left: { state: 'set', input: { kind: 'ip_stream', ip_address: '192.168.5.76' } },
    tv_right: { state: 'set', input: { kind: 'window_4' } },
  } } });
test('响应式已存预案的左右TV及payload嵌套输入均独立，不修改store/旧请求', () => {
  const source = reactive(existing()), draft = model.fromScenarioItem(source);
  draft.advanced.televisions.tv_left.input.ip_address = '192.168.5.85';
  draft.advanced.televisions.tv_right.input.kind = 'window_3';
  assert.equal(source.advanced.televisions.tv_left.input.ip_address, '192.168.5.76');
  assert.equal(source.advanced.televisions.tv_right.input.kind, 'window_4');
  const payload = model.toScenarioPayload(draft);
  draft.advanced.televisions.tv_left.input.ip_address = '192.168.5.87';
  assert.equal(payload.advanced.televisions.tv_left.input.ip_address, '192.168.5.85');
  assert.equal(payload.advanced.audio.handheld_volume, 0); assert.equal(payload.advanced.camera_positions.rear, 3);
});
test('旧预案缺少电视字段时不制造默认来源，摘要保持', () => {
  const item = existing(); delete item.advanced.televisions;
  const draft = model.fromScenarioItem(item);
  assert.equal(draft.advanced.televisions, undefined);
  assert.equal(helpers.televisionLabel(undefined), '保持');
  assert.equal(helpers.televisionError(undefined), '');
});
test('切换一台不改变另一台来源或对象；保持不夹带旧输入', () => {
  const item = existing(), next = helpers.setTelevisionInput(item.advanced.televisions, 'tv_left', { kind: 'window_2' });
  assert.equal(next.tv_right.input.kind, 'window_4');
  next.tv_right.input.kind = 'window_1';
  assert.equal(item.advanced.televisions.tv_right.input.kind, 'window_4');
  assert.deepEqual(helpers.copyTelevisions({ tv_left: { state: 'keep', input: { kind: 'window_1' } } }).tv_left, { state: 'keep' });
});
test('切换缺少输入显示本地错误；标签用短IP及用户窗口名', () => {
  assert.equal(helpers.televisionError({ tv_left: { state: 'set' } }), '请选择电视左的输入');
  assert.equal(helpers.televisionLabel(existing().advanced.televisions.tv_left), 'IP76');
  assert.equal(helpers.televisionLabel(existing().advanced.televisions.tv_right), '窗口 4');
  assert.equal(helpers.televisionLabel({ state: 'set', input: { kind: 'laptop' } }), 'IP81');
});
