/* 相同持久错误不能重复堆叠遮住手机界面；不同错误与动作通知仍独立保留。 */
import assert from 'node:assert/strict';
import test from 'node:test';
import { createPinia, setActivePinia } from 'pinia';
import { useToastStore } from '../src/composables/useToast.ts';

test('相同无操作按钮的持久错误只保留一个通知', () => {
  setActivePinia(createPinia());
  const store = useToastStore();
  const first = store.error('删除失败', '媒体原件被占用，删除未生效');
  const second = store.error('删除失败', '媒体原件被占用，删除未生效');
  assert.equal(second, first);
  assert.equal(store.items.length, 1);
});

test('不同错误内容不能被合并', () => {
  setActivePinia(createPinia());
  const store = useToastStore();
  store.error('删除失败', '文件占用');
  store.error('删除失败', '文件只读');
  assert.equal(store.items.length, 2);
});

test('带动作的通知保留各自回调，不按文本去重', () => {
  setActivePinia(createPinia());
  const store = useToastStore();
  let firstCalls = 0;
  let secondCalls = 0;
  store.error('连接失败', '请重试', { label: '重试', onTrigger: () => { firstCalls += 1; } });
  store.error('连接失败', '请重试', { label: '重试', onTrigger: () => { secondCalls += 1; } });
  store.items[0].action.onTrigger();
  store.items[1].action.onTrigger();
  assert.equal(firstCalls, 1);
  assert.equal(secondCalls, 1);
});
