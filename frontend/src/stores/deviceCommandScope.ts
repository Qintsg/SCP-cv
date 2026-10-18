/** 设备指令的本机租约：按Store/设备防重，只消费原主机、认证和当前意图的结果。 */
import { clientConnection, ConnectionSwitchInProgressError } from '@/platform/connection';
import { captureAuthenticationScope, assertAuthenticationScope } from '@/services/authenticationScope';
import { isWritePending } from '@/services/writeFlights';

interface DeviceCommandLease { isCurrent(): boolean; assertCurrent(): void; release(): void }
const operations = new WeakMap<object, Map<string, DeviceCommandLease>>();

/** 同设备重复动作明确拒绝，不将未发送的命令伪装为成功或排队重放。 */
export class DeviceCommandPendingError extends Error {
  /** :returns: 当前本地防重说明，不表示实体状态。 */
  constructor() { super('该设备的指令仍在处理中，请等待完成后再操作。'); this.name = 'DeviceCommandPendingError'; }
}

/** :param owner: 当前Store。:param deviceType: 设备键。:returns: 该设备当前有效操作仍在途。 */
export function isDeviceCommandPending(owner: object, deviceType: string): boolean {
  return isWritePending(`device:${deviceType}`) || (operations.get(owner)?.get(deviceType)?.isCurrent() ?? false);
}

/** :param owner: 当前Store。:returns: reset使所有旧意图失效，不撤销已受理HTTP。 */
export function resetDeviceCommands(owner: object): void { operations.delete(owner); }

/** :param owner: 当前Store。:param deviceType: 冻结设备键。:returns: 仅本次操作可提交/释放的租约。 */
export function beginDeviceCommand(owner: object, deviceType: string): DeviceCommandLease {
  if (clientConnection.isSwitching) throw new ConnectionSwitchInProgressError();
  if (isDeviceCommandPending(owner, deviceType)) throw new DeviceCommandPendingError();
  let bucket = operations.get(owner);
  if (!bucket) { bucket = new Map(); operations.set(owner, bucket); }
  const owned = bucket, generation = clientConnection.captureGeneration(), authentication = captureAuthenticationScope();
  const operation: DeviceCommandLease = {
    isCurrent: () => operations.get(owner) === owned && owned.get(deviceType) === operation
      && generation === clientConnection.captureGeneration() && authentication(),
    assertCurrent: () => assertAuthenticationScope(operation.isCurrent),
    release: () => { if (owned.get(deviceType) === operation) owned.delete(deviceType); },
  };
  owned.set(deviceType, operation);
  return operation;
}
