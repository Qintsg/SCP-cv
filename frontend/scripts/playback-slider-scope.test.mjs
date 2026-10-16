/*
 * 真实播放控制页滑块在途回归：仅延迟外部HTTP，不操作组件内部状态或实际声卡。
 * @Project : SCP-cv
 * @File : playback-slider-scope.test.mjs
 * @Author : Qintsg
 * @Date : 2026-10-04
 */
import { prepareSliderGeometry } from './dom-test-server.mjs';
import { browserKeyCode } from './dom-test-server.mjs';

import assert from 'node:assert/strict';
import test from 'node:test';
import { browser, PlaybackControl, mountPreview, flushUi, observe, responseGate } from './presentation-preview/fixture.mjs';

/** :returns: 已确认视频会话及外部写请求门闩。 */
async function mountSlider() {
  const sessions = await mountPreview(PlaybackControl, { observation: {
    source_id: 40, source_type: 'video', source_name: '实际视频40', source_uri: '/media/40.mp4',
    actual_source_id: 40, actual_adapter_kind: 'vlc', actual_fit_text: null, playback_mode: '',
    volume: 50, position_ms: 10000, duration_ms: 60000, playback_state: 'playing', error_message: '',
    desired_generation: 7, observed_generation: 7,
  }, sourcesTransform: (rows) => rows.map((row) => ({ ...row, source_type: 'video', playback_mode: '' })) });
  const fallback = globalThis.fetch;
  const writes = [];
  const gate = responseGate();
  globalThis.fetch = async (url, options = {}) => {
    const path = new URL(String(url), browser.location.origin).pathname;
    if (!path.match(/^\/api\/playback\/1\/(volume|navigate)\/$/)) return fallback(url, options);
    writes.push({ path, body: JSON.parse(options.body) });
    if (writes.length === 1) await gate.promise;
    return new Response(JSON.stringify({ success: true, sessions: [sessions.byWindowId(1)] }),
      { headers: { 'Content-Type': 'application/json' } });
  };
  return { sessions, writes, gate };
}

/** :param label: 公开滑块名称。:returns: 真实Naive滑块，发送原生焦点/键盘事件，不直接调用回调。 */
function slider(label) {
  const element = browser.document.querySelector(`[role="slider"][aria-label="${label}"]`);
  assert.equal(Boolean(element), true, label);
  return element;
}

/** :param element: 真实滑块。:returns: 用户左方向键输入及keyup最终提交已经触发。 */
async function decrease(element) {
  prepareSliderGeometry(element); element.focus();
  element.dispatchEvent(new browser.KeyboardEvent('keydown', { keyCode: browserKeyCode('ArrowLeft'), which: browserKeyCode('ArrowLeft'),  key: 'ArrowLeft', bubbles: true, cancelable: true }));
  await flushUi();
  element.dispatchEvent(new browser.KeyboardEvent('keyup', { keyCode: browserKeyCode('ArrowLeft'), which: browserKeyCode('ArrowLeft'),  key: 'ArrowLeft', bubbles: true }));
  await flushUi();
}

test('窗口音量在途时实际适配器变为图片，旧未发音量不得补发', async () => {
  const { sessions, writes, gate } = await mountSlider();
  await decrease(slider('窗口音量'));
  assert.equal(writes.length, 1, '第一项合法输入应实际发送');
  await decrease(slider('窗口音量'));
  await observe(sessions, { actual_adapter_kind: 'image' });
  gate.resolve();
  await flushUi();
  await flushUi();
  assert.equal(writes.length, 1, '类型已不可操作，未发送的缓冲必须丢弃');
});

test('窗口音量在途时切到另一个视频源，旧缓冲不得投向新视频', async () => {
  const { sessions, writes, gate } = await mountSlider();
  await decrease(slider('窗口音量'));
  await decrease(slider('窗口音量'));
  await observe(sessions, { source_id: 41, source_uri: '/media/41.mp4', actual_source_id: 41,
    desired_generation: 8, observed_generation: 8 });
  gate.resolve();
  await flushUi();
  await flushUi();
  assert.equal(writes.length, 1, '同类型新源仍须使旧缓冲失效');
});

test('进度定位在途时实际适配器失效，旧未发SEEK不得补发', async () => {
  const { sessions, writes, gate } = await mountSlider();
  await decrease(slider('播放进度'));
  assert.equal(writes.length, 1);
  assert.equal(writes[0].body.action, 'seek');
  await decrease(slider('播放进度'));
  await observe(sessions, { actual_adapter_kind: 'image' });
  gate.resolve();
  await flushUi();
  await flushUi();
  assert.equal(writes.length, 1, '失效后不补发旧SEEK');
});

for (const label of ['窗口音量', '播放进度']) {
  test(`${label}同源重开代次变化使旧缓冲失效，但新输入仍可发送`, async () => {
    const { sessions, writes, gate } = await mountSlider();
    await decrease(slider(label));
    await decrease(slider(label));
    await observe(sessions, { desired_generation: 8, observed_generation: 8 });
    gate.resolve();
    await flushUi();
    await flushUi();
    assert.equal(writes.length, 1);
    await decrease(slider(label));
    assert.equal(writes.length >= 2, true, '新操作域必须能接收新输入');
  });
}

test('普通视频心跳与位置回写不丢弃合法音量缓冲', async () => {
  const { sessions, writes, gate } = await mountSlider();
  await decrease(slider('窗口音量'));
  await decrease(slider('窗口音量'));
  await observe(sessions, { position_ms: 12000, volume: 49 });
  gate.resolve();
  await flushUi();
  await flushUi();
  assert.deepEqual(writes.map((item) => item.body.volume), [49, 48]);
});
