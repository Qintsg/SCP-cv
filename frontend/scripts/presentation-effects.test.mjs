/*
 * 动画步真实DOM回归：网络在途防重与REST/SSE观测，不判断动画是否结束。
 * 保留真实Vue、Naive UI、Pinia、API和通知宿主，仅替换外部fetch响应。
 * @Project : SCP-cv
 * @File : presentation-effects.test.mjs
 * @Author : Qintsg
 * @Date : 2026-10-03
 */
import { visibleText } from './dom-test-server.mjs';

import assert from 'node:assert/strict';
import test from 'node:test';
import { browser, PlaybackControl, PptFocusView, mountPreview, flushUi,
  responseGate, observe, currentImage, fittedImageUrl, unmountPreview } from './presentation-preview/fixture.mjs';

/**
 * 通过两处真实页面挂载动画控制，不替换组件或store内部方法。
 * :param component: 当前页面。
 * :param respondControl: 外部控制HTTP响应。
 * :param reuseStoreScope: 后续路由是否保留同一真实应用store。
 * :returns: 当前公开store与外部控制请求记录。
 */
async function mountEffects(component, respondControl, reuseStoreScope = false) {
  const sessions = await mountPreview(component, { withNotifications: true, reuseStoreScope, observation: {
    source_id: 40, source_name: '实际文稿40', actual_source_id: 40,
    playback_state: 'playing', playback_state_label: '播放中', error_message: '',
    desired_generation: 7, observed_generation: 7,
  } });
  const fallbackFetch = globalThis.fetch;
  const controls = [];
  globalThis.fetch = async (url, options = {}) => {
    const target = new URL(String(url), browser.location.origin);
    if (target.pathname !== '/api/playback/1/control/') return fallbackFetch(url, options);
    const request = { method: options.method, body: JSON.parse(options.body) };
    controls.push(request);
    return respondControl(request, sessions);
  };
  return { sessions, controls };
}

/** :param label: 公开按钮名称。:returns: 当前真实Naive UI动画按钮。 */
function effectButton(label) {
  const button = [...browser.document.querySelectorAll('.presentation-effects button')]
    .find((candidate) => visibleText(candidate.textContent) === visibleText(label));
  assert.ok(button, `应有真实动画按钮：${label}`);
  return button;
}

/** :param sessions: 服务端会话读回。:returns: 只表示HTTP受理的外部响应。 */
function acceptedResponse(sessions) {
  return new Response(JSON.stringify({ success: true, sessions }),
    { headers: { 'Content-Type': 'application/json' } });
}

for (const [name, component] of [['显示页', () => PlaybackControl], ['专注页', () => PptFocusView]]) {
  test(`${name}processing只读等待，终态后才提示且不阻塞或重发动画POST`, async () => {
    const id = 'a9ac79b8-f625-453a-81b4-a5f791cc4c89';
    const { controls } = await mountEffects(component(), (_request, store) => acceptedResponse([
      { ...store.byWindowId(1), submitted_command_id: id },
    ]));
    const callbacks = [];
    const originalTimeout = browser.setTimeout.bind(browser);
    browser.setTimeout = (callback, delay, ...args) => {
      if (delay !== 200) return originalTimeout(callback, delay, ...args);
      callbacks.push(() => callback(...args));
      return 1000000 + callbacks.length;
    };
    const fallback = globalThis.fetch;
    let queries = 0;
    globalThis.fetch = (url, options) => {
      if (!String(url).includes(`/commands/${id}/`)) return fallback(url, options);
      queries++;
      return Promise.resolve(new Response(JSON.stringify({ success: true, command: {
        command_id: id, window_id: 1, command: 'NEXT_EFFECT', source_generation: 7,
        status: queries === 1 ? 'processing' : 'completed',
        result_code: queries === 1 ? '' : 'presentation_at_last_slide',
        completed_at: queries === 1 ? null : '2026-10-04T00:00:00Z',
      } }), { headers: { 'Content-Type': 'application/json' } }));
    };
    effectButton('下一步动画').click();
    await flushUi();
    assert.equal(queries, 1);
    assert.equal(effectButton('下一步动画').disabled, false);
    assert.doesNotMatch(browser.document.body.textContent, /当前已是最后一页/);
    assert.equal(callbacks.length, 1);
    callbacks.shift()();
    await flushUi();
    assert.equal(queries, 2);
    assert.match(browser.document.body.textContent, /当前已是最后一页/);
    assert.equal(controls.length, 1);
  });

  for (const invalidate of ['换源', '卸载', 'ABA代次', '倒退']) {
    test(`${name}${invalidate}后旧命令末页终态不污染当前通知`, async () => {
      const id = '9c0d4055-8b57-4bfa-ae6f-549522bbdd7c';
      const terminal = responseGate();
      const { sessions } = await mountEffects(component(), (request, store) => acceptedResponse([
        { ...store.byWindowId(1), submitted_command_id: request.body.action === 'next_effect' ? id : undefined },
      ]));
      const fallback = globalThis.fetch;
      globalThis.fetch = (url, options) => String(url).includes(`/commands/${id}/`)
        ? terminal.promise : fallback(url, options);
      effectButton('下一步动画').click();
      await flushUi();
      if (invalidate === '倒退') {
        effectButton('上一步动画').click();
        await flushUi();
      } else if (invalidate === '卸载') {
        unmountPreview();
        await mountEffects(component(), (_request, store) => acceptedResponse([store.byWindowId(1)]), true);
      } else {
        await observe(sessions, { actual_source_id: 41, source_id: 41, desired_generation: 8, observed_generation: 8 });
        if (invalidate === 'ABA代次') await observe(sessions, {
          actual_source_id: 40, source_id: 40, desired_generation: 7, observed_generation: 7,
        });
      }
      terminal.resolve(new Response(JSON.stringify({ success: true, command: {
        command_id: id, window_id: 1, command: 'NEXT_EFFECT', source_generation: 7,
        status: 'completed', result_code: 'presentation_at_last_slide', completed_at: '2026-10-04T00:00:00Z',
      } }), { headers: { 'Content-Type': 'application/json' } }));
      await flushUi();
      assert.doesNotMatch(browser.document.body.textContent, /当前已是最后一页|无法读取操作结果/);
    });
  }

  for (const patch of [{ source_generation: 8 }, { command_id: 'other-command' },
    { command: 'PREVIOUS_EFFECT' }, { completed_at: null }, { result_code: 42 }]) {
    test(`${name}畸形或过期终态${JSON.stringify(patch)}不能产生末页提示`, async () => {
      const id = 'c377b97e-151e-4027-886e-88e1631d67e4';
      await mountEffects(component(), (_request, store) => acceptedResponse([
        { ...store.byWindowId(1), submitted_command_id: id },
      ]));
      const fallback = globalThis.fetch;
      globalThis.fetch = (url, options) => String(url).includes(`/commands/${id}/`)
        ? Promise.resolve(new Response(JSON.stringify({ success: true, command: {
          command_id: id, window_id: 1, command: 'NEXT_EFFECT', source_generation: 7,
          status: 'completed', result_code: 'presentation_at_last_slide', completed_at: '2026-10-04T00:00:00Z', ...patch,
        } }), { headers: { 'Content-Type': 'application/json' } })) : fallback(url, options);
      effectButton('下一步动画').click();
      await flushUi();
      assert.doesNotMatch(browser.document.body.textContent, /当前已是最后一页/);
      assert.match(browser.document.body.textContent, /无法读取操作结果/);
    });
  }

  test(`${name}整页下一页也跟踪自己的命令并显示确认边界`, async () => {
    const id = '334f4721-8173-4924-bf41-bbd5f96557b9';
    await mountEffects(component(), (_request, store) => acceptedResponse([store.byWindowId(1)]));
    const fallback = globalThis.fetch;
    globalThis.fetch = (url, options) => {
      const path = new URL(String(url), browser.location.origin).pathname;
      if (path.endsWith('/navigate/')) return Promise.resolve(acceptedResponse([
        { window_id: 1, submitted_command_id: id, desired_generation: 7 },
      ]));
      if (path.includes('/commands/')) return Promise.resolve(new Response(JSON.stringify({ success: true, command: {
        command_id: id, window_id: 1, command: 'NEXT', source_generation: 7,
        status: 'completed', result_code: 'presentation_at_last_slide', completed_at: '2026-10-04T00:00:00Z',
      } }), { headers: { 'Content-Type': 'application/json' } }));
      return fallback(url, options);
    };
    const next = [...browser.document.querySelectorAll('button')].find(button => visibleText(button.textContent) === visibleText('下一页'));
    assert.ok(next);
    next.click();
    await flushUi();
    assert.match(browser.document.body.textContent, /当前已是最后一页/);
  });

  for (const [status, code, message] of [
    ['completed', 'ok', null], ['superseded', 'superseded_by_newer_intent', null],
    ['failed', 'presentation_failed', '操作执行失败'], ['uncertain', 'presentation_transport_uncertain', '操作结果未确认'],
  ]) {
    test(`${name}持久${status}不冒充末页成功或重放`, async () => {
      const id = '0a5a8d62-dd5a-4d25-9a3f-ea7d8f37c4f3';
      const { controls } = await mountEffects(component(), (_request, store) => acceptedResponse([
        { ...store.byWindowId(1), submitted_command_id: id },
      ]));
      const fallback = globalThis.fetch;
      globalThis.fetch = (url, options) => String(url).includes(`/commands/${id}/`)
        ? Promise.resolve(new Response(JSON.stringify({ success: true, command: {
          command_id: id, window_id: 1, command: 'NEXT_EFFECT', source_generation: 7,
          status, result_code: code, completed_at: '2026-10-04T00:00:00Z',
        } }), { headers: { 'Content-Type': 'application/json' } })) : fallback(url, options);
      effectButton('下一步动画').click();
      await flushUi();
      assert.doesNotMatch(browser.document.body.textContent, /当前已是最后一页/);
      if (message) assert.ok(browser.document.body.textContent.includes(message));
      assert.equal(controls.length, 1);
    });
  }

  test(`${name}不同窗口的完成结果被拒绝，不能触发末页提示`, async () => {
    const id = '02eae472-60b1-486c-8ccf-397401baaf89';
    await mountEffects(component(), (_request, store) => acceptedResponse([
      { ...store.byWindowId(1), submitted_command_id: id },
    ]));
    const fallback = globalThis.fetch;
    globalThis.fetch = (url, options) => String(url).includes(`/commands/${id}/`)
      ? Promise.resolve(new Response(JSON.stringify({ success: true, command: {
        command_id: id, window_id: 2, command: 'NEXT_EFFECT', source_generation: 7,
        status: 'completed', result_code: 'presentation_at_last_slide', completed_at: '2026-10-04T00:00:00Z',
      } }), { headers: { 'Content-Type': 'application/json' } })) : fallback(url, options);
    effectButton('下一步动画').click();
    await flushUi();
    assert.doesNotMatch(browser.document.body.textContent, /当前已是最后一页/);
    assert.match(browser.document.body.textContent, /命令回执身份不匹配/);
  });

  test(`${name}只有自己提交的持久末页终态才提示，受理不提示或重放`, async () => {
    const commandId = 'ac8c1f1d-7bd9-4a8c-868a-9ae2e73c6c51';
    const terminal = responseGate();
    const { controls } = await mountEffects(component(), (_request, store) => acceptedResponse([
      { ...store.byWindowId(1), submitted_command_id: commandId },
    ]));
    const fallback = globalThis.fetch;
    const queries = [];
    globalThis.fetch = async (url, options) => {
      const target = new URL(String(url), browser.location.origin);
      if (!target.pathname.includes('/commands/')) return fallback(url, options);
      queries.push(target.pathname);
      return terminal.promise;
    };
    effectButton('下一步动画').click();
    await flushUi();
    assert.equal(controls.length, 1);
    assert.deepEqual(queries, [`/api/playback/1/commands/${commandId}/`]);
    assert.doesNotMatch(browser.document.body.textContent, /当前已是最后一页/);
    terminal.resolve(new Response(JSON.stringify({ success: true, command: {
      command_id: commandId, window_id: 1, command: 'NEXT_EFFECT', source_generation: 7,
      status: 'completed', result_code: 'presentation_at_last_slide', completed_at: '2026-10-04T00:00:00Z',
    } }), { headers: { 'Content-Type': 'application/json' } }));
    await flushUi();
    assert.match(browser.document.body.textContent, /当前已是最后一页/);
    assert.equal(controls.length, 1);
  });

  test(`${name}卸载后旧动画HTTP错误不能污染后续页面的通知`, async () => {
    const gate = responseGate();
    const { controls } = await mountEffects(component(), () => gate.promise);
    effectButton('下一步动画').click();
    await flushUi();
    assert.equal(controls.length, 1);
    unmountPreview();
    // 后续页面保留真实通知provider，不能用一起卸载provider把旧catch错误隐藏。
    await mountEffects(component(), (_request, sessions) => acceptedResponse([sessions.byWindowId(1)]), true);
    gate.resolve(new Response(JSON.stringify({ detail: '已卸载旧页面的动画失败' }), {
      status: 400, headers: { 'Content-Type': 'application/json' },
    }));
    await flushUi();
    assert.doesNotMatch(browser.document.body.textContent, /已卸载旧页面的动画失败|动画步操作失败/);
  });

  test(`${name}同步双点下一步并交错上一步在网络在途内仅发送一条动画POST`, async () => {
    const gate = responseGate();
    const { sessions, controls } = await mountEffects(component(), () => gate.promise);
    const next = effectButton('下一步动画');
    next.click();
    next.click();
    effectButton('上一步动画').click();
    await flushUi();
    assert.deepEqual(controls, [{ method: 'POST', body: { action: 'next_effect' } }]);
    assert.equal(effectButton('下一步动画').disabled, true);
    assert.equal(effectButton('上一步动画').disabled, true);
    gate.resolve(acceptedResponse([sessions.byWindowId(1)]));
    await flushUi();
  });

  test(`${name}动画HTTP错误显示真实通知并释放网络门闩，用户再次点击才重发`, async () => {
    const { controls } = await mountEffects(component(), (_request, sessions) => controls.length === 1
      ? new Response(JSON.stringify({ detail: '动画命令被拒绝：测试HTTP错误' }), {
        status: 400, headers: { 'Content-Type': 'application/json' },
      }) : acceptedResponse([sessions.byWindowId(1)]));
    effectButton('下一步动画').click();
    await flushUi();
    assert.deepEqual(controls, [{ method: 'POST', body: { action: 'next_effect' } }]);
    assert.match(browser.document.body.textContent, /动画步操作失败/);
    assert.match(browser.document.body.textContent, /动画命令被拒绝：测试HTTP错误/);
    assert.equal(effectButton('下一步动画').disabled, false);
    assert.equal(effectButton('上一步动画').disabled, false);
    // 失败后不自动重放；第二条请求只能来自用户这一轮显式点击。
    await flushUi();
    assert.equal(controls.length, 1);
    effectButton('上一步动画').click();
    await flushUi();
    assert.deepEqual(controls.map((request) => request.body),
      [{ action: 'next_effect' }, { action: 'previous_effect' }]);
  });

  test(`${name}动画网络受理保持实际页码和参考图，不展示动画完成`, async () => {
    const view = component();
    const { sessions, controls } = await mountEffects(view, (_request, store) => acceptedResponse([
      { ...store.byWindowId(1), pending_command: 'NEXT_EFFECT', last_updated_at: '2026-10-03T00:00:01Z' },
    ]));
    assert.equal(currentImage(view), fittedImageUrl(1));
    effectButton('下一步动画').click();
    await flushUi();
    assert.deepEqual(controls, [{ method: 'POST', body: { action: 'next_effect' } }]);
    assert.equal(sessions.byWindowId(1).pending_command, 'NEXT_EFFECT');
    assert.equal(sessions.byWindowId(1).current_slide, 1);
    assert.equal(currentImage(view), fittedImageUrl(1));
    assert.equal(browser.document.querySelectorAll('.ant-notification-notice-success').length, 0);
    assert.doesNotMatch(browser.document.body.textContent, /动画(?:已)?完成|动画(?:已)?结束/);
    // 当前两页面对pending禁用的差异不是本项合同，不决定动画播放中是否允许继续推进。
  });

  test(`${name}较旧动画REST在新SSE之后返回时不能覆写新的实际页码或参考图`, async () => {
    const view = component();
    const gate = responseGate();
    const { sessions, controls } = await mountEffects(view, () => gate.promise);
    const oldSnapshot = { ...sessions.byWindowId(1), pending_command: 'NEXT_EFFECT' };
    effectButton('下一步动画').click();
    await flushUi();
    assert.equal(controls.length, 1);
    await observe(sessions, { current_slide: 2, pending_command: '', playback_state: 'playing' });
    assert.equal(currentImage(view), fittedImageUrl(2));
    gate.resolve(acceptedResponse([oldSnapshot]));
    await flushUi();
    assert.equal(sessions.byWindowId(1).current_slide, 2);
    assert.equal(sessions.byWindowId(1).pending_command, '');
    assert.equal(currentImage(view), fittedImageUrl(2));
    assert.equal(effectButton('下一步动画').disabled, false);
    assert.equal(effectButton('上一步动画').disabled, false);
  });
}
