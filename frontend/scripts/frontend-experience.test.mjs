/** 前端体验回归：真实组件语义、两窗状态与筛选提示，不连接实体设备。 */
import assert from 'node:assert/strict';
import test, { before, after } from 'node:test';
import { createSSRApp, h } from 'vue';
import { renderToString } from 'vue/server-renderer';
import { createDomTestServer } from './dom-test-server.mjs';
let server, header, empty, presentation;
before(async () => {
  server = await createDomTestServer();
  ({ default: header } = await server.ssrLoadModule('/src/design-system/FPageHeader.vue'));
  ({ default: empty } = await server.ssrLoadModule('/src/design-system/FEmptyState.vue'));
  presentation = await server.ssrLoadModule('/src/features/dashboard/windowPresentation.ts');
});
after(async () => { await server?.close(); });
test('页头只有一个页面标题并保留说明和操作槽', async () => {
  const html = await renderToString(createSSRApp({ render: () => h(header, { title: '媒体文件', description: '管理当前目录' }, { actions: () => h('button', '上传文件') }) }));
  assert.equal((html.match(/<h1\b/g) ?? []).length, 1);
  for (const value of ['媒体文件', '管理当前目录', '上传文件']) assert.ok(html.includes(value));
});
test('空结果保留具体原因与恢复动作，不附带假成功状态', async () => {
  const html = await renderToString(createSSRApp({ render: () => h(empty, { title: '没有匹配的媒体', description: '试试其他名称，或清除筛选条件。' }, { default: () => h('button', '清除筛选') }) }));
  assert.ok(html.includes('没有匹配的媒体') && html.includes('清除筛选'));
});
const base = { source_id: 3, actual_source_id: 3, player_online: true, playback_state: 'playing', desired_generation: 7, observed_generation: 7 };
test('窗口在线不冒充实际播放，空/离线/切源/错误分别展示', () => {
  assert.equal(presentation.windowPresentation(undefined).label, '等待连接');
  assert.equal(presentation.windowPresentation({ ...base, player_online: false }).label, '播放器离线');
  assert.equal(presentation.windowPresentation({ ...base, actual_source_id: 4 }).label, '正在切换');
  assert.equal(presentation.windowPresentation({ ...base, observed_generation: 6 }).label, '正在切换');
  assert.equal(presentation.windowPresentation({ ...base, pending_command: 'open' }).label, '正在处理');
  assert.equal(presentation.windowPresentation({ ...base, playback_state: 'error' }).label, '播放失败');
  assert.equal(presentation.windowPresentation({ ...base, source_id: null, actual_source_id: null, playback_state: 'idle' }).label, '空闲');
  assert.equal(presentation.windowPresentation(base).label, '播放中');
});
test('预热开关描述配置意图，不把未执行的预热说成完成', async () => {
  const { default: copy } = await server.ssrLoadModule('/src/locales/zh-CN/sources.ts');
  assert.equal(copy.sources.add.preheat, '提前加载');
  assert.equal(copy.sources.add.preheatSwitch, '开启');
  assert.equal(copy.sources.editDrawer.preheat, '提前加载');
  assert.equal(copy.sources.editDrawer.preheatSwitch, '开启');
});
