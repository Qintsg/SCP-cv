/** 文件描述：专注页两按钮结构检查。项目：SCP-cv；文件：ppt-focus-experience.test.mjs；作者：Qintsg；日期：2026-10-27。 */
import assert from 'node:assert/strict';
import test from 'node:test';
import { browser, PptFocusView, mountPreview, recordedRequests } from './presentation-preview/fixture.mjs';

for (const mode of ['libreoffice', 'slide_images']) {
  test(`专注页${mode}保持两颗步进按钮，展示布局不触发控制请求`, async () => {
    await mountPreview(PptFocusView, { observation: {
      source_id: 40, source_name: '实际文稿40', actual_source_id: 40,
      playback_mode: mode, actual_adapter_kind: mode, playback_state: 'playing',
      desired_generation: 7, observed_generation: 7,
    } });
    const controls = browser.document.querySelector('.ppt-focus__controls'); assert.ok(controls);
    assert.equal(controls.querySelector('.presentation-navigation').getAttribute('role'), 'group');
    const pages = [...controls.querySelectorAll('button')];
    assert.equal(pages.length, 2);
    assert.deepEqual(pages.map(button => button.getAttribute('aria-label')), ['上一步', '下一步']);
    assert.ok(pages.every(button => button.textContent.trim() && !button.disabled));
    assert.equal(recordedRequests().filter(request => request.method !== 'GET').length, 0);
  });
}
