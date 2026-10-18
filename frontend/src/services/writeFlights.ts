/** 同一网页内的真实写入占用：按后端Origin/资源隔离，不因认证、页面或Store reset提前释放。 */
import { ref, shallowReactive } from 'vue';
import { clientConnection } from '@/platform/connection';

interface Flight { done: Promise<void>; finish(): void }
const flights = shallowReactive(new Map<string, Flight>());
const revisions = new Map<string, number>();
const targetRevision = ref(0);
clientConnection.onProfileChanged(() => { targetRevision.value++; });

/** :returns: 与REST目标相同的Origin；读取实际地址提交信号，使忙态可随主机切换更新。 */
export function currentWriteTarget(): string {
  void targetRevision.value;
  return clientConnection.profile?.origin || window.location.origin;
}

/** :param resource: 资源键。:param target: 冻结后端。:returns: 不包含认证身份的占用键。 */
function key(resource: string, target: string): string { return JSON.stringify([target, resource]); }

/** :param resource: 资源键。:param target: 当前或冻结后端。:returns: 原HTTP是否仍未终端。 */
export function isWritePending(resource: string, target = currentWriteTarget()): boolean {
  return flights.has(key(resource, target));
}

/** :param resource: 资源键。:param target: 冻结后端。:returns: 单调写修订，避免较旧读取覆盖新写入。 */
export function writeRevision(resource: string, target = currentWriteTarget()): number {
  return revisions.get(key(resource, target)) ?? 0;
}

/** 同资源重复写入明确拒绝，不排队，不声称撤销服务器已受理操作。 */
export class WriteInProgressError extends Error {
  /** :returns: 仍在途的可重试说明。 */
  constructor() { super('同一设备的请求仍在处理中，请等待完成后再操作。'); this.name = 'WriteInProgressError'; }
}

/** :param resource: 资源键。:param send: 原目标HTTP，不得借用新环境。:param target: 冻结后端。:returns: 实际请求终端后才释放；不受UI寿命影响。 */
export async function runExclusiveWrite<T>(resource: string, send: () => Promise<T>, target = currentWriteTarget()): Promise<T> {
  const id = key(resource, target);
  if (flights.has(id)) throw new WriteInProgressError();
  let finish!: () => void;
  const done = new Promise<void>(resolve => { finish = resolve; });
  const flight = { done, finish };
  revisions.set(id, (revisions.get(id) ?? 0) + 1);
  flights.set(id, flight);
  try { return await send(); }
  finally {
    if (flights.get(id) === flight) flights.delete(id);
    flight.finish();
  }
}

/** :param resource: 只读恢复所依赖的资源。:param target: 原后端。:returns: 等实际写入结束；调用者必须在读取前复核主机/认证，不重放旧命令。 */
export async function waitForWriteIdle(resource: string, target: string): Promise<void> {
  let flight: Flight | undefined;
  while ((flight = flights.get(key(resource, target)))) await flight.done;
}
