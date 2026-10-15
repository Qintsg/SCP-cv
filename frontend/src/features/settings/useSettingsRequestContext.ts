/** 设置请求的反馈归属：沿用主机代次/账号身份，退出重登和卸载也使旧回执失效。 */
import { onScopeDispose, watch } from 'vue';
import { useRuntimeStore } from '@/stores/runtime';
import { useAuthStore } from '@/stores/auth';
import { clientConnection } from '@/platform/connection';

/** :param reset: 归属变化时清除旧草稿。:returns: 捕获当前归属并检查迟到反馈的函数。 */
export function useSettingsRequestContext(reset: () => void): () => () => boolean {
  const runtime = useRuntimeStore();
  const auth = useAuthStore();
  let active = true;
  let revision = 0;
  const invalidate = () => {
    revision++;
    reset();
  };
  watch(() => [runtime.connectionGeneration, auth.user?.id], invalidate, { flush: 'sync' });
  // coordinator先失效真实代次，再等待旧会话退出；此时runtime投影尚未更新。
  const unsubscribe = clientConnection.onGenerationChanged(invalidate);
  onScopeDispose(() => { active = false; revision++; unsubscribe(); });
  return () => {
    const captured = revision;
    const generation = runtime.connectionGeneration;
    const connectionGeneration = clientConnection.captureGeneration();
    const user = auth.user?.id;
    return () => active && captured === revision
      && generation === runtime.connectionGeneration && connectionGeneration === clientConnection.captureGeneration()
      && user === auth.user?.id;
  };
}
