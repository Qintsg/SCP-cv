// 文件描述：使用真实Vue作用域与文件提交验证滑块和文稿步进。
// 项目：SCP-cv；文件：verify-controls-real.mjs；作者：Qintsg；日期：2026-10-27。
import assert from 'node:assert/strict';
import { appendFile, mkdir, readFile } from 'node:fs/promises';
import { randomBytes } from 'node:crypto';
import { setImmediate as nextEvent, setTimeout as delay } from 'node:timers/promises';
import { fileURLToPath } from 'node:url';
import { createServer } from 'vite';
import { effectScope, ref } from 'vue';
const root = fileURLToPath(new URL('../', import.meta.url));
const directory = fileURLToPath(new URL(`../../.validation/controls-${randomBytes(8).toString('hex')}/`, import.meta.url));
await mkdir(directory);
const server = await createServer({ configFile: false, root, mode: 'web', resolve: { alias: { '@': fileURLToPath(new URL('../src/', import.meta.url)) } }, server: { middlewareMode: true }, logLevel: 'error' });
let scope;
try {
  const { useThrottledSlider } = await server.ssrLoadModule('/src/composables/useThrottledSlider.ts');
  const { presentationStep } = await server.ssrLoadModule('/src/features/display/presentationStep.ts');
  const remote = ref(50), target = ref('first');
  scope = effectScope();
  const slider = scope.run(() => useThrottledSlider(() => remote.value, { throttleMs: 0, scope: () => target.value,
    commit: async value => { const file = `${directory}/${target.value}.jsonl`; await appendFile(file, JSON.stringify(value) + '\n'); remote.value = value; } }));
  /** :returns: 实际文件提交完成。 */
  async function settled() {
    for (let attempt = 0; attempt < 200 && slider.busy.value; attempt++) await delay(5);
    assert.equal(slider.busy.value, false);
    await nextEvent();
  }
  /** :param name: 本次实际目标。:returns: 已写入的提交值。 */
  async function values(name) { return (await readFile(`${directory}/${name}.jsonl`, 'utf8')).trim().split('\n').map(JSON.parse); }
  slider.handleInput(62); await settled(); await nextEvent(); slider.handleChange(62); await settled();
  assert.deepEqual(await values('first'), [62], '同次拖动收尾重复提交');
  slider.handleChange(62); await settled();
  assert.deepEqual(await values('first'), [62, 62], '独立同值操作应可再次提交');
  for (let value = 64; value <= 88; value++) slider.handleInput(value);
  slider.handleChange(88); await settled();
  const recorded = await values('first');
  assert.equal(recorded.at(-1), 88); assert.ok(recorded.length <= 4);
  target.value = 'second'; slider.handleInput(31); await settled(); slider.handleChange(31); await settled();
  assert.deepEqual(await values('second'), [31]);
  assert.deepEqual(presentationStep('libreoffice', 'prev'), { kind: 'effect', action: 'previous_effect', command: 'PREVIOUS_EFFECT' });
  assert.deepEqual(presentationStep('libreoffice', 'next'), { kind: 'effect', action: 'next_effect', command: 'NEXT_EFFECT' });
  for (const mode of ['pdf', 'slide_images']) assert.deepEqual(presentationStep(mode, 'next'), { kind: 'slide', action: 'next', command: 'NEXT' });
  console.log('真实Vue与文件提交：跨事件收尾、独立同值、连续输入最终值、目标切换及文稿步进通过。');
} finally { scope?.stop(); await server.close(); }
