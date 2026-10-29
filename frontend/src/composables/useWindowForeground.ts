// 文件描述：维护菜单依据实际窗口状态提交隐藏或显示，并跟踪持久回执。
// 项目：SCP-cv；文件：useWindowForeground.ts；作者：Qintsg；日期：2026-10-29。
import { computed, ref } from 'vue';
import { useSessionStore } from '@/stores/sessions';
import { api } from '@/services/api';
import { useRequestContext } from './useRequestContext';
import { useToast } from './useToast';

/** :returns: 实际状态、菜单文案及当前主机归属下的窗口维护动作。 */
export function useWindowForeground() {
  const session = useSessionStore(), toast = useToast(), pending = ref(false);
  let waiting: (() => void) | undefined;
  const capture = useRequestContext(() => { waiting?.(); pending.value = false; });
  const windows = computed(() => [session.byWindowId(1), session.byWindowId(2)]);
  const available = computed(() => windows.value.every(window => window?.player_online && typeof window.window_foreground === 'boolean'));
  const foreground = computed(() => available.value && windows.value.every(window => window!.window_foreground));
  const label = computed(() => foreground.value ? '隐藏窗口' : '显示窗口');

  /** :returns: 可在页面或主机归属变化时释放的只读轮询等待。 */
  function delay(): Promise<void> {
    return new Promise(resolve => {
      const finish = () => { clearTimeout(timer); waiting = undefined; resolve(); };
      const timer = setTimeout(finish, 250); waiting = finish;
    });
  }
  /** :returns: 仅提交一次绝对意图，后续只读查询直到实际执行或明确失败。 */
  async function toggle(): Promise<void> {
    if (pending.value || !available.value) return;
    const current = capture(), desired = !foreground.value;
    pending.value = true;
    try {
      const payload = await api.setWindowsForeground(desired);
      if (!current()) return;
      session.applyRemoteSessions(payload.sessions);
      const receipts = payload.sessions.map(window => ({ id: window.submitted_command_id!, windowId: window.window_id }));
      if (receipts.length !== 2 || receipts.some(receipt => !receipt.id)) throw new Error('窗口操作回执缺失。');
      const deadline = Date.now() + 35_000;
      while (current() && Date.now() < deadline) {
        const results = await Promise.all(receipts.map(receipt => api.getPlaybackCommand(receipt.windowId, receipt.id)));
        if (!current()) return;
        for (const [index, result] of results.entries()) {
          const command = result.command;
          if (command.command_id !== receipts[index]!.id || command.window_id !== receipts[index]!.windowId || command.command !== 'SET_WINDOW_FOREGROUND')
            throw new Error('窗口操作回执不匹配。');
          if (['failed', 'uncertain', 'superseded'].includes(command.status)) throw new Error(command.result_code || '窗口操作未完成。');
        }
        if (results.every(result => result.command.status === 'completed')) {
          await session.refresh();
          if (!current()) return;
          if (windows.value.every(window => window?.window_foreground === desired)) return;
        }
        await delay();
      }
      if (current()) throw new Error('等待窗口执行超时。');
    } catch (error) {
      if (current()) toast.error('窗口操作失败', error instanceof Error ? error.message : '请检查播放主机连接。');
    } finally { if (current()) pending.value = false; }
  }
  return { available, foreground, label, pending, toggle };
}
