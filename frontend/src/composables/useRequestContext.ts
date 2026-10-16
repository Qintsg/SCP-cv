/** 请求归属：主机、账号、页面寿命变化后，不提交旧意图或展示旧反馈。 */
import { onScopeDispose, watch } from 'vue';
import { useRuntimeStore } from '@/stores/runtime';
import { useAuthStore } from '@/stores/auth';
import { clientConnection } from '@/platform/connection';
import { captureAuthenticationScope } from '@/services/authenticationScope';

/** 认证表单消费自身改密/退出的完成事实，由Auth完成凭据接管修订保护。 */
interface RequestContextOptions { authenticationLifetime?: boolean }

/** :param reset: 归属变化时清除旧意图。:param options: 默认保护完整认证寿命，认证动作可明确交给自身完成凭据。:returns: 当前归属检查器。 */
export function useRequestContext(reset: () => void = () => {}, options: RequestContextOptions = {}): () => () => boolean {
  const runtime = useRuntimeStore();
  const auth = useAuthStore();
  let active = true;
  let revision = 0;
  const fullAuthentication = options.authenticationLifetime !== false;
  const invalidate = () => { revision++; reset(); };
  // 读取器跟踪私有认证修订；账号对象未变的会话更新同样使旧意图失效。
  watch(() => [runtime.connectionGeneration, fullAuthentication ? captureAuthenticationScope() : auth.user, auth.user?.id], invalidate, { flush: 'sync' });
  // coordinator先失效真实代次，再等待旧会话退出；runtime投影可能尚未更新。
  const unsubscribe = clientConnection.onGenerationChanged(invalidate);
  onScopeDispose(() => { active = false; revision++; unsubscribe(); reset(); });
  return () => {
    const captured = revision;
    const generation = runtime.connectionGeneration;
    const connectionGeneration = clientConnection.captureGeneration();
    const user = auth.user?.id;
    const account = auth.user;
    const authentication = fullAuthentication ? captureAuthenticationScope() : () => true;
    return () => active && captured === revision
      && generation === runtime.connectionGeneration && connectionGeneration === clientConnection.captureGeneration()
      && user === auth.user?.id && account === auth.user && authentication();
  };
}
