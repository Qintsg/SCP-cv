/* 临时共享null源在真实Vue/Pinia控制条中的显示、条件停止和迟到SSE回归；HTTP替身不代表采集/出画。SCP-cv 2026-10-11。 */
import assert from 'node:assert/strict';
import test from 'node:test';
import { browser, PlaybackControl, mountPreview, flushUi, observe, recordedRequests } from './presentation-preview/fixture.mjs';

const id = '22222222-2222-4222-8222-222222222222';
const shared = { source_id: null, source_name: '屏幕共享', source_type: 'screen_share', source_type_label: '屏幕共享', source_uri: '',
  playback_mode: '', playback_state: 'playing', playback_state_label: '播放中', error_message: '', pending_command: '',
  actual_source_id: null, actual_adapter_kind: 'screen_share', share_id: id, actual_share_id: id,
  actual_frames: 30, actual_audio: false, desired_generation: 3, observed_generation: 3, current_slide: 0, total_slides: 0 };

/** :param patch: 服务端会话观测。:returns: 只经过外部HTTP的真实组件与公开store。 */
async function mount(patch = {}) { return mountPreview(PlaybackControl, { observation: { ...shared, ...patch } }); }
/** :param label: 用户按钮名称。:returns: 实际组件DOM，找不到就是失败。 */
function button(label) { const node = [...browser.document.querySelectorAll('button')].find((item) => item.textContent.trim() === label); assert.ok(node, label); return node; }

test('null媒体库ID的共享显示临时输出、实际帧，不显示未打开或借文件缩略图', async () => {
  await mount();
  assert.equal(browser.document.querySelector('h3')?.textContent.trim(), '屏幕共享');
  assert.match(browser.document.body.textContent, /已收到画面/);
  assert.match(browser.document.body.textContent, /已接收 30 帧/);
  assert.equal(browser.document.body.textContent.split('临时实时画面，不存入媒体库或预案').length - 1, 1, '共享限制说明不应重复堆叠');
  assert.doesNotMatch(browser.document.body.textContent, /未打开任何源|该窗口当前没有源/);
  assert.equal(browser.document.querySelectorAll('img').length, 0);
  assert.equal(button('停止共享').disabled, false);
  assert.equal(browser.document.querySelector('.n-slider')?.classList.contains('n-slider--disabled'), true);
  assert.equal(browser.document.querySelector('[role="switch"]')?.getAttribute('aria-disabled'), 'true');
});

test('仅连接或零帧playing不得在控制条假报已收到画面', async () => {
  await mount({ actual_frames: 0 });
  assert.match(browser.document.querySelector('.playback-control__heading')?.textContent ?? '', /等待共享画面/);
  assert.doesNotMatch(browser.document.querySelector('.playback-control__heading')?.textContent ?? '', /播放中|已收到画面/);
});

test('控制条停止共享只DELETE原身份并GET刷新，不发送无条件窗口关闭或重采', async () => {
  const sessions = await mount();
  const fallback = globalThis.fetch;
  const writes = [];
  globalThis.fetch = async (url, options = {}) => {
    const path = new URL(String(url), browser.location.origin).pathname;
    if (path === `/api/screen-shares/${id}/`) {
      writes.push({ path, method: options.method });
      return new Response(JSON.stringify({ success: true, share: { id, state: 'stopping' } }));
    }
    return fallback(url, options);
  };
  button('停止共享').click(); await flushUi(); await flushUi();
  assert.deepEqual(writes, [{ path: `/api/screen-shares/${id}/`, method: 'DELETE' }]);
  assert.equal(recordedRequests().some((item) => item.method !== 'GET'), false);
  await assert.rejects(sessions.reopenCurrentSource(1), /共享.*重新.*选择/);
  assert.equal(writes.length, 1);
});

test('停止待确认仍展示最后共享身份但禁用重复窗口关闭，迟到旧SSE不恢复原共享', async () => {
  const sessions = await mount();
  await observe(sessions, { share_id: null, source_type: '', source_name: '无', desired_generation: 4, playback_state: 'idle', pending_command: 'CLOSE' });
  assert.match(browser.document.body.textContent, /共享停止待确认/);
  assert.equal(button('停止共享').disabled, true);
  await observe(sessions, { share_id: '33333333-3333-4333-8333-333333333333', actual_share_id: '33333333-3333-4333-8333-333333333333',
    source_type: 'screen_share', desired_generation: 5, observed_generation: 5, playback_state: 'playing', pending_command: '', actual_frames: 40 });
  sessions.applyRemoteSessions([{ ...shared, window_id: 1, last_updated_at: '2026-10-03T00:00:00Z' }]); await flushUi();
  assert.equal(sessions.byWindowId(1).actual_share_id, '33333333-3333-4333-8333-333333333333');
  assert.match(browser.document.body.textContent, /已接收 40 帧/);
});
