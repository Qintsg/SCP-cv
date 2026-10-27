/*
 * 文稿预览真实DOM合同：实际源/布局/代次和迟到HTTP都由公开界面验证。
 * 夹具只替换外部HTTP和轮询时钟，不替换组件、store或API内部实现。
 * @Project : SCP-cv
 * @File : presentation-preview.test.mjs
 * @Author : Qintsg
 * @Date : 2026-10-03
 */
import { visibleText } from './dom-test-server.mjs';

import assert from 'node:assert/strict';
import test from 'node:test';
import { browser, PlaybackControl, PptFocusView, flushUi, resourceRows, mountPreview,
  fittedImageUrl, resourceRequests, responseGate, resourceResponse, observe, currentImage,
  pollClock, unmountPreview, recordedRequests } from './presentation-preview/fixture.mjs';

test('播放控制监看只取实际源40的适配页图，不借失败的期望源41缩略图', async () => {
  await mountPreview(PlaybackControl);
  assert.deepEqual(resourceRequests(), ['/api/sources/40/ppt-resources/?fit_text=true']);
  assert.equal(browser.document.querySelector('.playback-control__monitor img')?.src, fittedImageUrl(1));
  assert.equal(browser.document.querySelector('.playback-control__monitor strong')?.textContent, '实际文稿40');
  assert.doesNotMatch(browser.document.querySelector('.playback-control__monitor')?.textContent ?? '', /期望文稿41/);
});

test('专注页的当前页、下一页和缩略栏都取实际源40适配版，而不是期望源41', async () => {
  await mountPreview(PptFocusView);
  assert.deepEqual(resourceRequests(), ['/api/sources/40/ppt-resources/?fit_text=true']);
  assert.equal(browser.document.querySelector('.ppt-focus__current img')?.src, fittedImageUrl(1));
  assert.equal(browser.document.querySelector('.ppt-focus__next img')?.src, fittedImageUrl(2));
  assert.deepEqual([...browser.document.querySelectorAll('.ppt-slide-rail__thumb img')]
    .map((image) => image.src), [fittedImageUrl(1), fittedImageUrl(2)]);
  assert.equal(browser.document.querySelector('.ppt-focus__identity h1')?.textContent, '实际文稿40');
});

for (const componentName of ['control', 'focus']) {
  for (const oldStatus of [200, 404]) {
    test(`${componentName}同源切到适配布局后，迟到的原版HTTP${oldStatus}不能覆盖新图或错误状态`, async () => {
      const component = componentName === 'control' ? PlaybackControl : PptFocusView;
      const original = responseGate();
      const fitted = responseGate();
      const sessions = await mountPreview(component, { observation: { actual_fit_text: false },
        respondResource: (_id, fit) => fit ? fitted.promise : original.promise });
      await observe(sessions, { actual_fit_text: true });
      fitted.resolve(resourceResponse(resourceRows(40, true)));
      await flushUi();
      assert.equal(currentImage(component), fittedImageUrl(1));
      original.resolve(resourceResponse(resourceRows(40, false), oldStatus));
      await flushUi();
      assert.equal(currentImage(component), fittedImageUrl(1));
      assert.doesNotMatch(browser.document.body.textContent, /旧布局资源已不可用/);
      assert.equal(browser.document.querySelector('.ppt-focus__loading'), null);
    });
  }

  test(`${componentName} A→B→A同源回切后，首个A的迟到响应不能覆盖最新A`, async () => {
    const component = componentName === 'control' ? PlaybackControl : PptFocusView;
    const gates = [responseGate(), responseGate(), responseGate()];
    let index = 0;
    const sessions = await mountPreview(component, { respondResource: () => gates[index++].promise });
    await observe(sessions, { actual_source_id: 41 });
    await observe(sessions, { actual_source_id: 40 });
    assert.equal(index, 3);
    const latest = resourceRows(40, true).map((row) => ({ ...row, slide_image: `${row.slide_image}&qa_response=latest-A` }));
    gates[2].resolve(resourceResponse(latest));
    await flushUi();
    const newestUrl = new URL(latest[0].slide_image, browser.location.origin).href;
    assert.equal(currentImage(component), newestUrl);
    gates[0].resolve(resourceResponse(resourceRows(40, true)));
    gates[1].resolve(resourceResponse(resourceRows(41, true)));
    await flushUi();
    assert.equal(currentImage(component), newestUrl);
  });

  test(`${componentName}未知源、未知布局、非文稿实际适配器与完全缺观测字段均不借期望图`, async () => {
    const component = componentName === 'control' ? PlaybackControl : PptFocusView;
    const sessions = await mountPreview(component, { observation: { actual_source_id: null } });
    for (const patch of [{ actual_source_id: 40, actual_fit_text: null },
      { actual_fit_text: true, actual_adapter_kind: 'vlc' },
      { actual_source_id: undefined, actual_fit_text: undefined, actual_adapter_kind: undefined }]) {
      assert.equal(currentImage(component), '');
      await observe(sessions, patch);
    }
    assert.deepEqual(resourceRequests(), []);
    assert.equal(currentImage(component), '');
    const preview = browser.document.querySelector(component === PlaybackControl
      ? '.playback-control__monitor' : '.ppt-focus__stage');
    assert.doesNotMatch(preview?.textContent ?? '', /期望文稿41/);
  });

  test(`${componentName}适配图缺失且原版缩略图存在时明确报错，不借原版`, async () => {
    const component = componentName === 'control' ? PlaybackControl : PptFocusView;
    await mountPreview(component, { respondResource: () => resourceResponse([], 404) });
    assert.equal(currentImage(component), '');
    assert.match(browser.document.body.textContent, /旧布局资源已不可用/);
    assert.deepEqual(resourceRequests(), ['/api/sources/40/ppt-resources/?fit_text=true']);
  });

  test(`${componentName}期望变成视频不改变实际文稿40适配预览，fit-only图不依赖默认缩略图`, async () => {
    const component = componentName === 'control' ? PlaybackControl : PptFocusView;
    await mountPreview(component, { observation: { source_type: 'video', playback_mode: '' },
      sourcesTransform: (sources) => sources.map((source) => ({ ...source, preview_kind: 'icon',
        thumbnail_url: '', preview_url: '' })) });
    assert.deepEqual(resourceRequests(), ['/api/sources/40/ppt-resources/?fit_text=true']);
    assert.equal(currentImage(component), fittedImageUrl(1));
  });
}

test('专注页旧原版先结束时，尚在等待的新适配请求不能提前结束loading', async () => {
  const original = responseGate();
  const fitted = responseGate();
  const sessions = await mountPreview(PptFocusView, { observation: { actual_fit_text: false },
    respondResource: (_id, fit) => fit ? fitted.promise : original.promise });
  await observe(sessions, { actual_fit_text: true });
  original.resolve(resourceResponse(resourceRows(40, false)));
  await flushUi();
  assert.ok(browser.document.querySelector('.ppt-focus__loading'));
  assert.equal(currentImage(PptFocusView), '');
  fitted.resolve(resourceResponse(resourceRows(40, true)));
  await flushUi();
  assert.equal(browser.document.querySelector('.ppt-focus__loading'), null);
  assert.equal(currentImage(PptFocusView), fittedImageUrl(1));
});

test('专注页原版轮询在途切适配布局，迟到原版不能换图或安排下一次轮询', async () => {
  const clock = pollClock();
  const oldPoll = responseGate();
  let originalCalls = 0;
  const sessions = await mountPreview(PptFocusView, { observation: { actual_fit_text: false },
    respondResource: (_id, fit) => fit ? resourceResponse(resourceRows(40, true))
      : ++originalCalls === 1 ? resourceResponse(resourceRows(40, false).slice(0, 1)) : oldPoll.promise });
  clock.fire();
  await flushUi();
  await observe(sessions, { actual_fit_text: true });
  assert.equal(currentImage(PptFocusView), fittedImageUrl(1));
  const scheduledBeforeLate = clock.scheduled.length;
  oldPoll.resolve(resourceResponse(resourceRows(40, false).slice(0, 1)));
  await flushUi();
  assert.equal(currentImage(PptFocusView), fittedImageUrl(1));
  assert.equal(clock.scheduled.length, scheduledBeforeLate);
});

test('专注页卸载后迟到的轮询不能重新安排timer或发送HTTP', async () => {
  const clock = pollClock();
  const oldPoll = responseGate();
  let calls = 0;
  await mountPreview(PptFocusView, { respondResource: () => ++calls === 1
    ? resourceResponse(resourceRows(40, true).slice(0, 1)) : oldPoll.promise });
  clock.fire();
  await flushUi();
  assert.equal(calls, 2);
  unmountPreview();
  const scheduledBeforeLate = clock.scheduled.length;
  oldPoll.resolve(resourceResponse(resourceRows(40, true).slice(0, 1)));
  await flushUi();
  assert.equal(clock.scheduled.length, scheduledBeforeLate);
  assert.equal(calls, 2);
});

for (const componentName of ['control', 'focus']) {
  test(`${componentName} HTTP200但无页图时保持空预览及原生播放状态`, async () => {
    const component = componentName === 'control' ? PlaybackControl : PptFocusView;
    const rows = resourceRows(40, true).map((row) => ({ ...row, slide_image: '', next_slide_image: '' }));
    const sessions = await mountPreview(component, {
      observation: { source_id: 40, source_name: '实际文稿40', playback_state: 'playing',
        playback_state_label: '播放中', error_message: '' },
      respondResource: () => resourceResponse(rows),
    });
    assert.equal(currentImage(component), '');
    assert.equal(sessions.byWindowId(1).playback_state, 'playing');
    assert.equal(sessions.byWindowId(1).error_message, '');
    assert.equal(sessions.byWindowId(1).actual_fit_text, true);
  });
}

test('同源同适配布局重开到新实际代次后，旧代次HTTP响应不能覆盖新图', async () => {
  const gates = [responseGate(), responseGate()];
  let index = 0;
  const sessions = await mountPreview(PlaybackControl, {
    observation: { observed_generation: 5 }, respondResource: () => gates[index++].promise,
  });
  await observe(sessions, { observed_generation: 6 });
  assert.equal(index, 2, '同id/fit的新实际代次应重新读取预览并失效旧请求');
  const latest = resourceRows(40, true).map((row) => ({ ...row, slide_image: `${row.slide_image}&qa_generation=6` }));
  gates[1].resolve(resourceResponse(latest));
  await flushUi();
  const latestUrl = new URL(latest[0].slide_image, browser.location.origin).href;
  assert.equal(currentImage(PlaybackControl), latestUrl);
  gates[0].resolve(resourceResponse(resourceRows(40, true)));
  await flushUi();
  assert.equal(currentImage(PlaybackControl), latestUrl);
});

for (const componentName of ['control', 'focus']) {
  test(`${componentName}通过公开刷新预览按钮只GET恢复同布局页图，不重开原生或增加自动轮询预算`, async () => {
    const component = componentName === 'control' ? PlaybackControl : PptFocusView;
    const clock = componentName === 'focus' ? pollClock() : null;
    let ready = false;
    const sessions = await mountPreview(component, {
      observation: { source_id: 40, source_name: '实际文稿40', playback_state: 'playing', error_message: '' },
      respondResource: () => resourceResponse(ready ? resourceRows(40, true) : []),
    });
    if (clock) {
      for (let index = 0; index < 8; index++) { clock.fire(); await flushUi(); }
      assert.equal(clock.scheduled.length, 8, '既有自动补齐预算已经用完');
      assert.equal(resourceRequests().length, 9, '初始读取加八次自动补齐');
    }
    const button = [...browser.document.querySelectorAll('button')]
      .find((element) => visibleText(element.textContent) === visibleText('刷新预览') || element.getAttribute('aria-label') === '刷新预览');
    assert.ok(button, '预览不可用时应有独立只读刷新入口');
    assert.equal(button.disabled, false);
    if (clock) {
      button.click();
      await flushUi();
      assert.equal(resourceRequests().length, 10, '手动刷新仅增加一条只读GET');
      // 一秒任务也包含Naive按钮波纹。执行所有待处理任务后，以公开HTTP计数核自动预算。
      while (clock.hasPending()) {
        clock.fire();
        await flushUi();
        assert.equal(resourceRequests().length, 10, '手动读取仍为空，不能重置自动预算后再发GET');
      }
    }
    ready = true;
    button.click();
    await flushUi();
    assert.equal(currentImage(component), fittedImageUrl(1));
    assert.equal(resourceRequests().length, clock ? 11 : 2);
    assert.ok(resourceRequests().every((request) => request === '/api/sources/40/ppt-resources/?fit_text=true'));
    assert.ok(recordedRequests().every((request) => request.method === 'GET'), '不能发送OPEN/navigation或其它写请求');
    assert.equal(sessions.byWindowId(1).playback_state, 'playing');
    assert.equal(sessions.byWindowId(1).actual_fit_text, true);
  });
}
