/*
 * 多源输出参考真实DOM回归：期望源不冒充实际输出，参考图仍不证明实体出画。
 * @Project : SCP-cv
 * @File : playback-output.test.mjs
 * @Author : Qintsg
 * @Date : 2026-10-04
 */
import { visibleText } from './dom-test-server.mjs';

import assert from 'node:assert/strict';
import test from 'node:test';
import { browser, PlaybackControl, mountPreview, observe, recordedRequests } from './presentation-preview/fixture.mjs';

/** :param type: 既有媒体类型。:returns: 真HTTP列表中不同的实际/期望源，不替换组件内部选择器。 */
function sourceRows(type) {
  return (rows) => rows.map((row) => ({ ...row, name: row.id === 40 ? '实际旧源40' : '请求新源41',
    source_type: type, playback_mode: '', uri: `/media/${row.id}`, preview_kind: 'image' }));
}

/** :param type: 既有源类型。:param patch: 服务端会话覆盖值。:returns: 通过实际HTTP初始化的公开会话store。 */
async function mountOutput(type, patch = {}) {
  return mountPreview(PlaybackControl, { sourcesTransform: sourceRows(type), observation: {
    source_type: type, source_name: '请求新源41', source_uri: '/media/41', playback_mode: '',
    actual_adapter_kind: type === 'image' ? 'image' : type === 'web' ? 'web' : 'vlc', actual_fit_text: null,
    playback_state: 'loading', playback_state_label: '切换中', pending_command: 'OPEN',
    desired_generation: 2, observed_generation: 1, ...patch,
  } });
}

for (const type of ['image', 'video', 'web', 'rtsp_stream', 'srt_stream', 'custom_stream']) {
  test(`${type}切源未完成时输出参考保留实际旧源，不借新源缩略图`, async () => {
    await mountOutput(type);
    assert.equal(browser.document.querySelector('.playback-control__monitor strong')?.textContent, '实际旧源40');
    const image = browser.document.querySelector('.playback-control__monitor img');
    assert.ok(image?.src.endsWith('/default-source-40.png'));
    assert.doesNotMatch(browser.document.querySelector('.playback-control__monitor')?.textContent ?? '', /请求新源41/);
    assert.match(browser.document.body.textContent, /请求源.*实际源.*不同|尚未切换到请求源/);
    assert.equal(recordedRequests().some((request) => request.method !== 'GET'), false);
  });
}

test('明确空输出时不显示期望源缩略图，仍保留请求名称和失败反馈', async () => {
  await mountOutput('image', { actual_source_id: null, actual_adapter_kind: '',
    playback_state: 'error', playback_state_label: '打开失败', error_message: '图片加载失败', pending_command: '' });
  assert.equal(Boolean(browser.document.querySelector('.playback-control__monitor img')), false);
  assert.match(browser.document.body.textContent, /未确认实际输出/);
  assert.match(browser.document.body.textContent, /请求新源41/);
  assert.match(browser.document.body.textContent, /图片加载失败/);
});

test('缺失实际源字段的旧会话只标请求参考，不假称当前输出', async () => {
  await mountOutput('web', { actual_source_id: undefined, actual_adapter_kind: undefined,
    desired_generation: undefined, observed_generation: undefined, playback_state: 'playing' });
  assert.doesNotMatch(browser.document.querySelector('.playback-control__monitor')?.textContent ?? '', /当前输出参考/);
  assert.match(browser.document.body.textContent, /未确认实际输出/);
});

test('新的已确认观测切到新源，迟到旧观测不倒退参考图', async () => {
  const sessions = await mountOutput('video');
  await observe(sessions, { actual_source_id: 41, observed_generation: 2, playback_state: 'playing', pending_command: '' });
  assert.equal(browser.document.querySelector('.playback-control__monitor strong')?.textContent, '请求新源41');
  const latest = sessions.byWindowId(1);
  sessions.applyRemoteSessions([{ ...latest, actual_source_id: 40, observed_generation: 1,
    last_updated_at: '2026-10-02T00:00:00Z' }]);
  await observe(sessions, {});
  assert.equal(browser.document.querySelector('.playback-control__monitor strong')?.textContent, '请求新源41');
});

test('实际源不在媒体列表时明确缺少信息，不用请求源补齐', async () => {
  await mountPreview(PlaybackControl, { sourcesTransform: (rows) => sourceRows('web')(rows).filter((row) => row.id === 41),
    observation: { source_type: 'web', playback_mode: '', actual_source_id: 40, actual_adapter_kind: 'web', actual_fit_text: null } });
  assert.equal(Boolean(browser.document.querySelector('.playback-control__monitor img')), false);
  assert.match(browser.document.body.textContent, /实际源信息不可用/);
});

test('播放器离线时输出参考明示为最后报告，不暗示仍在出画', async () => {
  await mountOutput('image', { source_id: 40, actual_source_id: 40, player_online: false,
    pending_command: '', observed_generation: 2 });
  assert.match(browser.document.querySelector('.playback-control__monitor')?.textContent ?? '', /最后报告.*播放器离线/);
});

test('请求PPT但实际仍是旧视频时不用文稿预览逻辑隐藏视频参考', async () => {
  await mountPreview(PlaybackControl, { sourcesTransform: (rows) => rows.map((row) => row.id === 40
    ? { ...row, source_type: 'video', playback_mode: '', name: '实际旧视频' } : row),
  observation: { actual_source_id: 40, actual_adapter_kind: 'vlc', actual_fit_text: null } });
  assert.equal(browser.document.querySelector('.playback-control__monitor strong')?.textContent, '实际旧视频');
  assert.ok(browser.document.querySelector('.playback-control__monitor img')?.src.endsWith('/default-source-40.png'));
});

test('请求PPT而实际仍为视频时禁用错误类型的翻页和动画，保留显式关闭', async () => {
  await mountPreview(PlaybackControl, { sourcesTransform: (rows) => rows.map((row) => row.id === 40
    ? { ...row, source_type: 'video', playback_mode: '', name: '实际旧视频' } : row),
  observation: { actual_source_id: 40, actual_adapter_kind: 'vlc', actual_fit_text: null } });
  for (const label of ['上一页', '下一页', '跳转', '上一步动画', '下一步动画']) {
    const button = [...browser.document.querySelectorAll('button')].find((item) => visibleText(item.textContent) === visibleText(label));
    assert.equal(button?.disabled, true, label);
  }
  const close = [...browser.document.querySelectorAll('button')].find((item) => visibleText(item.textContent) === visibleText('关闭显示'));
  assert.equal(close?.disabled, false);
  assert.match(browser.document.body.textContent, /实际输出类型.*请求源.*不同/);
  assert.equal(recordedRequests().some((request) => request.method !== 'GET'), false);
});

test('视频空输出或实际图片时禁用播放停止循环和进度，确认VLC后恢复', async () => {
  const sessions = await mountOutput('video', { actual_source_id: null, actual_adapter_kind: '', duration_ms: 60000 });
  const controls = () => [...browser.document.querySelectorAll('.playback-control__section button')]
    .filter((item) => ['播放', '停止'].includes(item.textContent.trim()));
  for (const patch of [{}, { actual_source_id: 40, actual_adapter_kind: 'image' }]) {
    await observe(sessions, patch);
    assert.equal(controls().every((item) => item.disabled), true);
    assert.equal(browser.document.querySelector('.playback-control__row--seek .ant-slider')?.classList.contains('ant-slider-disabled'), true);
  }
  await observe(sessions, { actual_source_id: 41, actual_adapter_kind: 'vlc', pending_command: '', playback_state: 'paused' });
  assert.equal(controls().every((item) => !item.disabled), true);
});

test('直播摘要隐藏凭据及查询秘密，但会话保留原地址', async () => {
  const uri = 'rtsp://fixture-user:fixture-pass@192.0.2.188:554/live?token=fixture-token&channel=5';
  const sessions = await mountOutput('rtsp_stream', { source_uri: uri });
  const summary = browser.document.querySelector('.playback-control__uri')?.textContent;
  assert.ok(summary?.includes('192.0.2.188'));
  for (const secret of ['fixture-user', 'fixture-pass', 'fixture-token']) assert.equal(summary.includes(secret), false);
  assert.equal(sessions.byWindowId(1).source_uri, uri);
  assert.equal(recordedRequests().some(request => request.method !== 'GET'), false);
});
