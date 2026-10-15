/** 专注页实际DOM验证：翻页名称与动画分组在两种播放模式保持，几何由真实浏览器另验。 */
import assert from 'node:assert/strict';
import test from 'node:test';
import { browser, PptFocusView, mountPreview, recordedRequests } from './presentation-preview/fixture.mjs';

for (const mode of ['libreoffice', 'slide_images']) {
  test(`专注页${mode}使用明确翻页名称，动画与翻页分组不会凭布局触发请求`, async () => {
    await mountPreview(PptFocusView, { observation: {
      source_id: 40, source_name: '实际文稿40', actual_source_id: 40,
      playback_mode: mode, actual_adapter_kind: mode, playback_state: 'playing',
      desired_generation: 7, observed_generation: 7,
    } });
    const controls = browser.document.querySelector('.ppt-focus__controls'); assert.ok(controls);
    assert.equal(controls.getAttribute('role'), 'group');
    const pages = [...controls.children].filter(element => element.tagName === 'BUTTON');
    assert.equal(pages.length, 2);
    assert.deepEqual(pages.map(button => button.getAttribute('aria-label')), ['上一页', '下一页']);
    assert.ok(pages.every(button => button.textContent.trim() && !button.disabled));
    assert.equal(controls.querySelectorAll('.presentation-effects__actions button').length, mode === 'libreoffice' ? 2 : 0);
    assert.equal(recordedRequests().filter(request => request.method !== 'GET').length, 0);
  });
}
