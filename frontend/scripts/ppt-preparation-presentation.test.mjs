/*
 * PPT 状态文案键回归；未知代码保留诊断，不宣称原件缺失。
 * @Project : SCP-cv
 * @File : ppt-preparation-presentation.test.mjs
 * @Author : Qintsg
 * @Date : 2026-09-29
 */
import assert from 'node:assert/strict';
import test from 'node:test';
import { pptPreparationLabelKey } from '../src/features/sources/pptPreparationPresentation.ts';

test('准备生命周期各阶段使用明确的文案键', () => {
  for (const state of ['queued', 'running', 'ready', 'failed', 'uncertain', 'missing']) {
    assert.equal(pptPreparationLabelKey(state), `sources.preparationState.${state}`);
  }
});

test('旧记录空准备状态只表示页图尚未准备', () => {
  for (const state of [undefined, null, '']) {
    assert.equal(pptPreparationLabelKey(state), 'sources.preparationState.missing');
  }
});

test('未知非空状态没有假定文案，由界面保留原码', () => {
  for (const state of ['new_backend_state', 'constructor', '__proto__', 'toString', 'READY']) {
    assert.equal(pptPreparationLabelKey(state), null);
  }
});
