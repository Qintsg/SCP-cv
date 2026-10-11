/** 播放控制的手机触控样式回归；计算样式不替代真实浏览器几何。SCP-cv 2026-10-11。 */
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import test from 'node:test';
import { compileStyle } from 'vue/compiler-sfc';
import { Window } from 'happy-dom';

test('手机播控的停止等按钮至少44px，桌面仍保留紧凑尺寸', async () => {
  const source = readFileSync(new URL('../src/features/display/PlaybackControl.css', import.meta.url), 'utf8');
  const result = compileStyle({ source, id: 'data-v-playback-touch', scoped: true });
  assert.deepEqual(result.errors, []);
  for (const width of [390, 1440]) {
    const browser = new Window({ width, height: 1000 });
    try {
      const style = browser.document.createElement('style');
      style.textContent = result.code;
      browser.document.head.append(style);
      browser.document.body.innerHTML = '<div data-v-playback-touch class="playback-control"><button class="n-button">停止共享</button></div>';
      const minimum = browser.getComputedStyle(browser.document.querySelector('button')).minHeight;
      if (width === 390) assert.ok(parseFloat(minimum) >= 44, minimum);
      else assert.ok(!minimum || parseFloat(minimum) < 44, minimum);
    } finally {
      await browser.happyDOM.close();
    }
  }
});
