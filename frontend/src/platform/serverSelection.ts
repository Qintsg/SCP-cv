/** 本地资源包只连接用户明确选择的播放主机；网页仍使用同源入口。 */
import { getClientBuildTarget } from './index';
import { clientConnection } from './connection';

/** 缺少目标不是已探测未登录，也不能从app://、localhost或编译地址猜测。 */
export class ServerProfileRequiredError extends Error {
  /** :returns: 可引导用户进入现有连接页的明确错误。 */
  constructor() { super('请先选择并保存播放主机，再执行此操作。'); this.name = 'ServerProfileRequiredError'; }
}

/** :returns: 封装端尚无用户选择；不改变网页同源与显式回环开发配置。 */
export function requiresServerProfile(): boolean {
  return getClientBuildTarget() === 'app' && clientConnection.profile === null;
}

/** :raises ServerProfileRequiredError: 本地资源包无目标时拒绝派发，包含绝对URL。 */
export function assertServerProfileSelected(): void {
  if (requiresServerProfile()) throw new ServerProfileRequiredError();
}
