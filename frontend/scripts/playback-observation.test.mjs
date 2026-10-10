/*
 * 播出状态投影回归：REST/SSE 选中或受理不能冒充在线播放器确认。
 * @Project : SCP-cv
 * @File : playback-observation.test.mjs
 * @Author : Qintsg
 * @Date : 2026-09-29
 */
import assert from 'node:assert/strict';
import test from 'node:test';
import { isConfirmedPlayback, playbackStateTagType } from '../src/features/display/playbackObservation.ts';

test('有效源及在线 playing 可以显示播出标签', () => {
  assert.equal(isConfirmedPlayback({ source_id: 41, player_online: true, playback_state: 'playing' }), true);
});

test('loading、error、paused 或未知实际状态不得宣称播出', () => {
  for (const playback_state of ['loading', 'error', 'paused', 'idle', '', 'unknown']) {
    assert.equal(isConfirmedPlayback({ source_id: 41, player_online: true, playback_state }), false, playback_state);
  }
});

test('播放器失联时历史 playing 不得宣称仍在播出', () => {
  assert.equal(isConfirmedPlayback({ source_id: 41, player_online: false, playback_state: 'playing' }), false);
});

test('未加载会话或没有有效源时不得宣称播出', () => {
  assert.equal(isConfirmedPlayback(undefined), false);
  assert.equal(isConfirmedPlayback(null), false);
  for (const source_id of [null, 0]) {
    assert.equal(isConfirmedPlayback({ source_id, player_online: true, playback_state: 'playing' }), false);
  }
});

test('明确的实际源不同或尚未观测到当前代次时不能标记新源播出', () => {
  const base = { source_id: 41, player_online: true, playback_state: 'playing' };
  assert.equal(isConfirmedPlayback({ ...base, actual_source_id: null }), false);
  assert.equal(isConfirmedPlayback({ ...base, actual_source_id: 40 }), false);
  assert.equal(isConfirmedPlayback({ ...base, actual_source_id: 41, desired_generation: 3, observed_generation: 2 }), false);
  assert.equal(isConfirmedPlayback({ ...base, actual_source_id: 41, desired_generation: 3, observed_generation: 3 }), true);
});

test('临时共享必须匹配请求与实际身份、代次及正后帧，不能以null源或playing单独确认', () => {
  const id = '22222222-2222-4222-8222-222222222222';
  const sharing = { source_id: null, source_type: 'screen_share', share_id: id, actual_share_id: id,
    actual_source_id: null, actual_adapter_kind: 'screen_share', actual_frames: 30,
    desired_generation: 3, observed_generation: 3, player_online: true, playback_state: 'playing' };
  assert.equal(isConfirmedPlayback(sharing), true);
  for (const patch of [{ actual_frames: 0 }, { actual_frames: -1 }, { actual_share_id: null },
    { actual_share_id: '33333333-3333-4333-8333-333333333333' }, { actual_adapter_kind: 'web' },
    { actual_source_id: 42 }, { observed_generation: 2 }, { desired_generation: undefined },
    { player_online: false }, { playback_state: 'loading' }, { share_id: 'not-a-guid' }]) {
    assert.equal(isConfirmedPlayback({ ...sharing, ...patch }), false, JSON.stringify(patch));
  }
});

test('共享停止待确认的历史playing和帧不能保留成功色', () => {
  const id = '22222222-2222-4222-8222-222222222222';
  const closing = { source_id: null, source_type: '', share_id: null, actual_share_id: id,
    actual_source_id: null, actual_adapter_kind: 'screen_share', actual_frames: 30,
    desired_generation: 4, observed_generation: 3, player_online: true, playback_state: 'playing' };
  assert.equal(playbackStateTagType(closing), 'warning');
  assert.equal(playbackStateTagType({ ...closing, player_online: false }), 'warning');
  assert.equal(playbackStateTagType({ ...closing, playback_state: 'error' }), 'error');
});
