/*
 * 通过真实 Vue 组件调用公共滑块接口，验证在途请求与未发送缓冲的作用域。
 * 不模拟组件内部字段，不发送硬件或后端请求。
 * @Project : SCP-cv
 * @File : throttled-slider-scope.test.mjs
 * @Author : Qintsg
 * @Date : 2026-09-29
 */
import assert from 'node:assert/strict';
import { setImmediate as nextTurn } from 'node:timers/promises';
import test, { before, after, afterEach } from 'node:test';
import { Window } from 'happy-dom';

const browser = new Window({ url: 'http://localhost/slider-scope' });
const names = ['window', 'document', 'navigator', 'HTMLElement', 'Element', 'Node', 'SVGElement'];
const descriptors = new Map(names.map(name => [name, Object.getOwnPropertyDescriptor(globalThis, name)]));
for (const name of names) Object.defineProperty(globalThis, name, {
  value: browser[name], configurable: true, writable: true,
});

let vue;
let useThrottledSlider;
let app;

before(async () => {
  vue = await import('vue');
  ({ useThrottledSlider } = await import('../src/composables/useThrottledSlider.ts'));
});
afterEach(() => { app?.unmount(); app = undefined; browser.document.body.replaceChildren(); });
after(async () => {
  await browser.happyDOM.close();
  for (const [name, descriptor] of descriptors) {
    if (descriptor) Object.defineProperty(globalThis, name, descriptor);
    else delete globalThis[name];
  }
});

/**
 * 创建公开接口可操作的 Vue 滑块宿主与受控外部提交边界。
 * :returns: 状态、滑块接口与已接收请求。
 */
function fixture() {
  const state = vue.reactive({ online: true, scope: 'A', remote: 0 });
  const calls = [];
  let slider;
  app = vue.createApp({
    setup() {
      slider = useThrottledSlider(() => state.remote, {
        throttleMs: 0,
        scope: () => state.scope,
        isActive: () => state.online,
        commit(value) {
          let complete;
          const promise = new Promise(resolve => { complete = resolve; });
          calls.push({ value, scope: state.scope, complete });
          return promise;
        },
      });
      return () => vue.h('div');
    },
  });
  const root = browser.document.createElement('div');
  browser.document.body.append(root);
  app.mount(root);
  return { state, calls, slider };
}

/**
 * 等待已接收外部提交 Promise 和 Vue microtask 完成，不用任意睡眠。
 * :returns: 当前响应处理完成。
 */
async function flush() { await vue.nextTick(); await nextTurn(); await vue.nextTick(); }

test('在途第一请求后的缓冲不能在离线恢复时自动重放', async () => {
  const { state, calls, slider } = fixture();
  slider.handleInput(10);
  slider.handleInput(20);
  state.online = false;
  await flush();
  state.online = true;
  await flush();
  calls[0].complete();
  await flush();
  assert.deepEqual(calls.map(call => call.value), [10]);
});

test('旧作用域缓冲不能转送到新源或新窗口', async () => {
  const { state, calls, slider } = fixture();
  slider.handleInput(10);
  slider.handleInput(20);
  state.scope = 'B';
  await flush();
  calls[0].complete();
  await flush();
  assert.deepEqual(calls.map(({ value, scope }) => ({ value, scope })), [{ value: 10, scope: 'A' }]);
});

test('组件卸载后旧请求完成不能再发送缓存', async () => {
  const { calls, slider } = fixture();
  slider.handleInput(10);
  slider.handleInput(20);
  app.unmount();
  app = undefined;
  calls[0].complete();
  await flush();
  assert.deepEqual(calls.map(call => call.value), [10]);
});

test('健康同源缓冲保留最后一次合法输入，普通心跳不取消', async () => {
  const { state, calls, slider } = fixture();
  slider.handleInput(10);
  slider.handleInput(20);
  slider.handleChange(30);
  state.remote = 11;
  await flush();
  calls[0].complete();
  await flush();
  assert.deepEqual(calls.map(call => call.value), [10, 30]);
  calls[1].complete();
});

test('新作用域的新输入可发送，旧响应不能解除新在途请求或触发其缓冲', async () => {
  const { state, calls, slider } = fixture();
  slider.handleInput(10);
  slider.handleInput(20);
  state.scope = 'B';
  await flush();
  slider.handleInput(40);
  slider.handleInput(50);
  assert.equal(calls.length, 2);
  calls[0].complete();
  await flush();
  assert.deepEqual(calls.map(call => call.value), [10, 40]);
  calls[1].complete();
  await flush();
  assert.deepEqual(calls.map(call => call.value), [10, 40, 50]);
  calls[2].complete();
});
