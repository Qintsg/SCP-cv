/** 精确关联点击回执与持久终态；不阻塞快速动画点击，不盲重放外部命令。 */
import { onScopeDispose, watch } from 'vue';
import { api, type SessionSnapshot } from '@/services/api';
import type { PlaybackCommandSnapshot } from '@/services/apiTypes';
import { useToast } from '@/composables/useToast';
import { clientConnection } from '@/platform/connection';

/**
 * :param readSession: 当前页面实际窗口/源身份。
 * :returns: 提交后后台只读跟踪的入口；离开、换源或代次变化使旧反馈失效。
 */
export function usePresentationCommandFeedback(readSession: () => SessionSnapshot | undefined, readCommandRevision: () => number) {
  const toast = useToast();
  let active = true;
  let revision = 0;
  const waits = new Set<() => void>();
  const identity = () => {
    const state = readSession();
    return JSON.stringify(state ? [state.window_id, state.actual_source_id, state.source_id,
      state.actual_adapter_kind, state.desired_generation, state.observed_generation, readCommandRevision()] : []);
  };
  watch(identity, () => { revision++; }, { flush: 'sync' });
  onScopeDispose(() => {
    active = false;
    for (const cancel of waits) cancel();
  });

  /** :returns: 有界只读轮询间隔；卸载立即释放定时器，不撤销已提交动作。 */
  function delay(): Promise<void> {
    return new Promise((resolve) => {
      const finish = () => { window.clearTimeout(timer); waits.delete(finish); resolve(); };
      const timer = window.setTimeout(finish, 200);
      waits.add(finish);
    });
  }

  /** :param receipt: 本次POST的目标回执。:param command: 预期动作。:param valid: 页面身份围栏。:returns: 真实持久终态反馈，不把页码当耗尽证明。 */
  async function follow(receipt: SessionSnapshot, command: string, valid: () => boolean): Promise<void> {
    const id = receipt.submitted_command_id;
    if (!id || !valid()) return;
    const deadline = Date.now() + 35_000;
    try {
      while (valid() && Date.now() < deadline) {
        const payload = await api.getPlaybackCommand(receipt.window_id, id);
        if (!valid()) return;
        const result: PlaybackCommandSnapshot = payload.command;
        if (payload.success !== true || !result || typeof result.result_code !== 'string'
          || !Number.isSafeInteger(result.source_generation) || result.source_generation < 0)
          throw new Error('命令结果格式未确认，未采用其反馈。');
        if (result.command_id !== id || result.window_id !== receipt.window_id || result.command !== command
          || result.source_generation !== receipt.desired_generation) throw new Error('命令回执身份不匹配，未采用其反馈。');
        if (result.status === 'completed') {
          if (typeof result.completed_at !== 'string' || !Number.isFinite(Date.parse(result.completed_at)))
            throw new Error('命令完成时间未确认，未采用其反馈。');
          if ((command === 'NEXT_EFFECT' || command === 'NEXT') && result.result_code === 'presentation_at_last_slide')
            toast.info('当前已是最后一页');
          return;
        }
        if (result.status === 'failed' || result.status === 'uncertain') {
          toast.error(result.status === 'uncertain' ? '操作结果未确认' : '操作执行失败', result.result_code);
          return;
        }
        if (result.status === 'superseded') return;
        if (result.status !== 'pending' && result.status !== 'processing') throw new Error('命令状态未知，未采用其反馈。');
        await delay();
      }
      if (valid()) toast.warning('操作结果未确认', '请检查播放主机状态。');
    } catch (error) {
      if (valid()) toast.warning('无法读取操作结果', error instanceof Error ? error.message : '请检查播放主机连接。');
    }
  }

  /** :param operation: 一次显式提交。:param command: 预期命令。:returns: HTTP受理后返回，持久反馈独立等待。 */
  async function submit(operation: () => Promise<SessionSnapshot | undefined>, command: string): Promise<void> {
    const before = readSession();
    const connectionGeneration = clientConnection.captureGeneration();
    // Store在调用开始时同步更新客户端操作围栏；本次反馈取更新后的代次，较早反馈随即失效。
    const pending = operation();
    const stamp = revision;
    const valid = () => active && revision === stamp && clientConnection.captureGeneration() === connectionGeneration;
    try {
      const receipt = await pending;
      if (receipt && before?.actual_adapter_kind === 'libreoffice' && valid()) void follow(receipt, command, valid);
    } catch (error) {
      if (valid()) throw error;
    }
  }
  return { submit };
}
