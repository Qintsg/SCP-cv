/** 认证响应寿命：纯服务边界，不依赖Pinia、路由或认证Cookie内容。 */

export interface AuthenticationIdentity {
  revision: number;
  account: object | null;
  userId?: number;
}
type IdentityReader = () => AuthenticationIdentity;
const emptyIdentity: IdentityReader = () => ({ revision: 0, account: null });
let readIdentity = emptyIdentity;
let bindingRevision = 0;

/** 明确丢弃旧结果，不表示撤销服务器已经受理的操作。 */
export class RequestScopeChangedError extends Error {
  /** :returns: 不携带认证数据的归属失效错误。 */
  constructor() {
    super('操作所属的主机或登录会话已改变，旧响应已丢弃。');
    this.name = 'RequestScopeChangedError';
  }
}

/** :param read: 当前认证Store读取器。:returns: 作用域归属与条件释放，不让旧Store释放新绑定。 */
export function bindAuthenticationScope(read: IdentityReader): { isCurrent: () => boolean; dispose: () => void } {
  readIdentity = read;
  bindingRevision++;
  return {
    isCurrent: () => readIdentity === read,
    dispose: () => { if (readIdentity === read) { readIdentity = emptyIdentity; bindingRevision++; } },
  };
}

/** :returns: 捕获认证操作、对象和ID；同ID重登、reset及Store重建均可识别。 */
export function captureAuthenticationScope(): () => boolean {
  const read = readIdentity, captured = read(), binding = bindingRevision;
  return () => {
    if (read !== readIdentity || binding !== bindingRevision) return false;
    const current = read();
    return current.revision === captured.revision && current.account === captured.account && current.userId === captured.userId;
  };
}

/** :param current: 原归属检查。:returns: 有效才继续。:raises RequestScopeChangedError: 旧结果不可消费。 */
export function assertAuthenticationScope(current: () => boolean): void {
  if (!current()) throw new RequestScopeChangedError();
}
