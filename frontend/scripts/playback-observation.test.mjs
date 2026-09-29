/*
 * 播出状态投影回归：REST/SSE 选中或受理不能冒充在线播放器确认。
 * @Project : SCP-cv
 * @File : playback-observation.test.mjs
 * @Author : Qintsg
 * @Date : 2026-09-29
 */
import assert from 'node:assert/strict';
import test from 'node:test';
import { isConfirmedPlayback } from '../src/features/display/playbackObservation.ts';

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
