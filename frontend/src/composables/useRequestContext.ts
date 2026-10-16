/** 请求归属：主机、账号、页面寿命变化后，不提交旧意图或展示旧反馈。 */
import { onScopeDispose, watch } from 'vue';
import { useRuntimeStore } from '@/stores/runtime';
import { useAuthStore } from '@/stores/auth';
import { clientConnection } from '@/platform/connection';

/** :param reset: 归属变化时清除旧意图。:returns: 捕获当前归属并复核的函数。 */
export function useRequestContext(reset: () => void = () => {}): () => () => boolean {
  const runtime = useRuntimeStore();
  const auth = useAuthStore();
  let active = true;
  let revision = 0;
  const invalidate = () => { revision++; reset(); };
  watch(() => [runtime.connectionGeneration, auth.user?.id], invalidate, { flush: 'sync' });
  // coordinator先失效真实代次，再等待旧会话退出；runtime投影可能尚未更新。
  const unsubscribe = clientConnection.onGenerationChanged(invalidate);
  onScopeDispose(() => { active = false; revision++; unsubscribe(); reset(); });
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
