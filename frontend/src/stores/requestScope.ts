/** Store的HTTP结果归属：主机或认证对象改变后，不提交旧索引或继续读取新账号。 */
import { clientConnection } from '@/platform/connection';
import { useAuthStore } from './auth';

/** 明确取消旧归属响应，不让调用方把已丢弃的数据当成功继续使用。 */
export class RequestScopeChangedError extends Error {
  /** :returns: 不携带账号数据的明确归属失效错误。 */
  constructor() {
    super('操作所属的主机或登录会话已改变，旧响应已丢弃。');
    this.name = 'RequestScopeChangedError';
  }
}

/** :returns: 捕获当前主机与认证对象；同ID重新登录仍属于新认证对象。 */
export function captureStoreRequestScope(): () => boolean {
  const auth = useAuthStore(), user = auth.user, id = user?.id;
  const generation = clientConnection.captureGeneration();
  return () => generation === clientConnection.captureGeneration() && user === auth.user && id === auth.user?.id;
}

/** :param current: 原归属检查。:returns: 仍有效时继续。:raises RequestScopeChangedError: 旧结果不可再消费。 */
export function assertStoreRequestScope(current: () => boolean): void {
  if (!current()) throw new RequestScopeChangedError();
}
