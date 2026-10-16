/*
 * 文稿跳页输入真实DOM合同：非法整数文本不得被parseInt截断为导航命令。
 * 真实Pinia、API、ToastHost和Naive通知保留，只替换外部HTTP边界。
 * @Project : SCP-cv
 * @File : presentation-jump.test.mjs
 * @Author : Qintsg
 * @Date : 2026-10-03
 */
import { visibleText } from './dom-test-server.mjs';

import assert from 'node:assert/strict';
import test from 'node:test';
import { browser, PlaybackControl, mountPreview, flushUi, recordedRequests } from './presentation-preview/fixture.mjs';

test('跳页输入2a点击实际按钮时不发送navigate，并显示既有整数页码提示', async () => {
  await mountPreview(PlaybackControl, { withNotifications: true });
  const input = browser.document.querySelector('.playback-control__jump input');
  assert.ok(input, '应有真实跳页输入');
  input.value = '2a';
  input.dispatchEvent(new browser.Event('input', { bubbles: true }));
  await flushUi();
  const button = [...browser.document.querySelectorAll('.playback-control__jump button')]
    .find((candidate) => visibleText(candidate.textContent) === visibleText('跳转'));
  assert.ok(button, '应有实际跳转按钮');
  button.click();
  await flushUi();
  assert.deepEqual(recordedRequests().filter((request) => request.pathname.endsWith('/navigate/')), []);
  assert.match(browser.document.body.textContent, /页码无效/);
  assert.match(browser.document.body.textContent, /请输入大于 0 的整数/);
});

/**
 * 经真实输入事件和现有按钮提交文本，不调用组件私有onJump。
 * :param text: 本轮用户输入。
 * :returns: 输入与点击后的公开DOM已经渲染。
 */
async function submitJump(text) {
  const input = browser.document.querySelector('.playback-control__jump input');
  assert.ok(input);
  input.value = text;
  input.dispatchEvent(new browser.Event('input', { bubbles: true }));
  await flushUi();
  const button = [...browser.document.querySelectorAll('.playback-control__jump button')]
    .find((candidate) => visibleText(candidate.textContent) === visibleText('跳转'));
  assert.ok(button);
  button.click();
  await flushUi();
}

for (const text of ['1.8', '0', '-1', '', '   ', 'Infinity', '2147483648', '9007199254740992', '1e2']) {
  test(`跳页非法整数文本${JSON.stringify(text)}不发送命令且保持既有提示`, async () => {
    await mountPreview(PlaybackControl, { withNotifications: true });
    await submitJump(text);
    assert.deepEqual(recordedRequests().filter((request) => request.pathname.endsWith('/navigate/')), []);
    assert.match(browser.document.body.textContent, /页码无效/);
    assert.match(browser.document.body.textContent, /请输入大于 0 的整数/);
  });
}

for (const [text, target] of [['1', 1], [' 2 ', 2], ['0002', 2], ['501', 501], ['2147483647', 2147483647]]) {
  test(`跳页完整正整数${JSON.stringify(text)}按既有GET/POST边界提交${target}不新增500限制`, async () => {
    const bodies = [];
    let sessions;
    sessions = await mountPreview(PlaybackControl, { withNotifications: true, respondNavigation: (body) => {
      bodies.push(body);
      // 只模拟命令受理并保持最后实际页码，不伪造Native已跳到目标页。
      return { success: true, sessions: [sessions.byWindowId(1)] };
    } });
    await submitJump(text);
    assert.deepEqual(bodies, [{ action: 'goto', target_index: target, position_ms: 0 }]);
    assert.equal(recordedRequests().filter((request) => request.pathname.endsWith('/navigate/')).length, 1);
    assert.doesNotMatch(browser.document.body.textContent, /页码无效/);
  });
}
