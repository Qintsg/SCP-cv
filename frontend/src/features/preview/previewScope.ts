/** 共享资源的归属独立于画布/页面；同账号重新登录和主机ABA同样使其立即失效。 */
import { useAuthStore } from '@/stores/auth';
import { useRuntimeStore } from '@/stores/runtime';
import { captureAuthenticationScope } from '@/services/authenticationScope';
import { clientConnection } from '@/platform/connection';

/** :returns: 应用认证/主机寿命检查器，不捕获第一个消费者的组件scope。 */
export function capturePreviewScope(): () => boolean {
  const auth = useAuthStore(), runtime = useRuntimeStore();
  const account = auth.user, userId = account?.id, generation = runtime.connectionGeneration;
  const connection = clientConnection.captureGeneration(), authentication = captureAuthenticationScope();
  return () => auth.user === account && auth.user?.id === userId && runtime.connectionGeneration === generation
    && clientConnection.captureGeneration() === connection && authentication();
}
