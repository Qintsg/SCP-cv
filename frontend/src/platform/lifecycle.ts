/** 原生生命周期与认证恢复归属；旧active/inactive/卸载结果不能导航或重建新连接。 */
import type { Router } from 'vue-router';

import type { PlatformAdapter } from './index';
import { dismissTopLayer } from './lifecycleBack';
import { resumeClientConnection } from './lifecycleState';
import { useAuthStore } from '@/stores/auth';
import { refreshStores } from '@/stores';
import { useRuntimeStore } from '@/stores/runtime';
import { clientConnection } from './connection';

/** :param path: 当前路径。:returns: 是否属于原生返回键的根页面。 */
function isRootRoute(path: string): boolean {
  return ['/dashboard', '/login', '/connect'].includes(path);
}

/** :param adapter: 受信平台薄壳。:param router: 当前应用路由。:returns: 幂等释放并取消旧恢复意图。 */
export function installClientLifecycle(adapter: PlatformAdapter, router: Router): () => void {
  const runtime = useRuntimeStore();
  const auth = useAuthStore();
  let installed = true, epoch = 0;
  let resumeInFlight: { promise: Promise<boolean>; isCurrent: () => boolean } | null = null;

  const resume = () => {
    if (!installed || resumeInFlight?.isCurrent()) return;
    const version = ++epoch, generation = clientConnection.captureGeneration(), projected = runtime.connectionGeneration;
    const current = () => installed && version === epoch && generation === clientConnection.captureGeneration()
      && projected === runtime.connectionGeneration && !clientConnection.isSwitching;
    if (!current()) return;
    let completed: (() => boolean) | null = null;
    const live = () => current() && (completed?.() ?? true);
    const operation = resumeClientConnection({
      canContinue: live,
      hasServerProfile: () => runtime.serverProfile !== null,
      disconnectEvents: () => runtime.disconnectEvents(),
      probeSession: () => auth.probeCurrentServer({ canContinue: current, onCommitted: owns => { completed = owns; } }),
      refreshSnapshot: async () => { await refreshStores(live); },
      connectEvents: () => { if (live()) runtime.connectEvents(); },
    }).then(async (authenticated) => {
      if (!live()) return false;
      if (!authenticated && runtime.serverProfile && router.currentRoute.value.path !== '/login') {
        await router.replace('/login');
      }
      return authenticated;
    }).catch(() => false).finally(() => { if (resumeInFlight?.promise === operation) resumeInFlight = null; });
    resumeInFlight = { promise: operation, isCurrent: live };
  };

  const removeLifecycle = adapter.onLifecycleChanged((state) => {
    if (!installed) return;
    if (state === 'inactive') { epoch++; runtime.disconnectEvents(); }
    else resume();
  });
  const removeBack = adapter.onBackRequested(async () => {
    if (!installed) return false;
    if (dismissTopLayer()) return true;
    if (!isRootRoute(router.currentRoute.value.path)) {
      await router.back();
      return true;
    }
    return false;
  });

  return () => {
    if (!installed) return;
    installed = false;
    epoch++;
    resumeInFlight = null;
    removeLifecycle();
    removeBack();
  };
}
