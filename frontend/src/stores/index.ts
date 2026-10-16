/*
 * Store 出口：业务层只从此处导入领域 store。
 * 同时提供 bootstrap() 一键拉取所有初始数据并建立 SSE 连接。
 */
import { useToastStore } from '@/composables/useToast';
import { t } from '@/locales';
import { clientConnection } from '@/platform/connection';
import { captureAuthenticationScope } from '@/services/authenticationScope';
import { useAuthStore } from './auth';

import { useDeviceStore } from './devices';
import { useDisplayStore } from './displays';
import { useBackgroundAudioStore } from './backgroundAudio';
import { useRuntimeStore } from './runtime';
import { useScenarioStore } from './scenarios';
import { useSessionStore } from './sessions';
import { useSourceStore } from './sources';

export {
  useRuntimeStore,
  useBackgroundAudioStore,
  useSessionStore,
  useSourceStore,
  useScenarioStore,
  useDeviceStore,
  useDisplayStore,
};

/** :param canContinue: 调用者生命周期。:returns: 先绑定认证，再冻结完整读取的主机/账号归属。 */
function captureSnapshotContext(canContinue: () => boolean): () => boolean {
  useAuthStore();
  const generation = clientConnection.captureGeneration(), authentication = captureAuthenticationScope();
  return () => canContinue() && generation === clientConnection.captureGeneration() && authentication() && !clientConnection.isSwitching;
}

/** :param canContinue: 原调用寿命。:returns: 本轮是否仍属于当前身份；当前部分失败保留警告，失效安静结束。 */
export async function refreshStores(canContinue: () => boolean = () => true): Promise<boolean> {
  const current = captureSnapshotContext(canContinue);
  if (!current()) return false;
  const runtime = useRuntimeStore();
  const backgroundAudio = useBackgroundAudioStore();
  const session = useSessionStore();
  const source = useSourceStore();
  const scenario = useScenarioStore();
  const device = useDeviceStore();
  const display = useDisplayStore();
  const toast = useToastStore();

  const tasks = await Promise.allSettled([
    runtime.refresh(),
    runtime.refreshSystemVolume(),
    backgroundAudio.refresh(),
    session.refresh(),
    source.refresh(),
    scenario.refresh(),
    device.refresh(),
    display.refresh(),
  ]);

  if (!current()) return false;

  // 单个失败不致命：使用 Toast 警告即可，让用户继续操作可用部分。
  const failed = tasks.find((task) => task.status === 'rejected');
  if (failed && failed.status === 'rejected') {
    toast.warning(
      t('bootstrap.partialFail'),
      failed.reason instanceof Error ? failed.reason.message : t('bootstrap.partialFailDetail'),
    );
  }

  return true;
}

/** :param canContinue: 原调用页寿命。:returns: 只有同一身份完整读取结束才建立SSE，旧读取安静结束。 */
export async function bootstrapStores(canContinue: () => boolean = () => true): Promise<void> {
  const current = captureSnapshotContext(canContinue);
  if (await refreshStores(current) && current()) useRuntimeStore().connectEvents();
}
