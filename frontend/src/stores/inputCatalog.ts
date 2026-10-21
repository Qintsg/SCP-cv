/** 应用级输入目录缓存；主机/完整认证寿命失效后不使用旧地址或旧刷新结果。 */
import { defineStore } from 'pinia';
import { onScopeDispose, ref, watch } from 'vue';
import { clientConnection } from '@/platform/connection';
import { captureAuthenticationScope } from '@/services/authenticationScope';
import { inputCatalogApi, type SiteInputItem } from '@/services/inputCatalogApi';
import { useRuntimeStore } from './runtime';
import { useAuthStore } from './auth';

export const useInputCatalogStore = defineStore('input-catalog', () => {
  const items = ref<SiteInputItem[]>([]), loading = ref(false), error = ref('');
  const auth = useAuthStore(), runtime = useRuntimeStore();
  let epoch = 0, pending: Promise<void> | undefined, pendingRefresh = false;
  /** :returns: 同步撤销旧目录和进行中请求的消费权，不向新主机补请求。 */
  function invalidate(): void { epoch++; items.value = []; error.value = ''; loading.value = false; pending = undefined; }
  watch(() => [runtime.connectionGeneration, captureAuthenticationScope(), auth.user], invalidate, { flush: 'sync' });
  const unsubscribe = clientConnection.onGenerationChanged(invalidate);
  onScopeDispose(unsubscribe);
  /** :param refresh: 显式目录进入需要探测时为true。:param force: 手动刷新跳过TTL。:returns: 相同归属内合并一个请求，旧finally不解新占用。 */
  async function load(refresh = false, force = false): Promise<void> {
    if (!auth.user) return;
    if (pending) {
      const waitingEpoch = epoch, upgrade = refresh && !pendingRefresh;
      await pending;
      if (upgrade && waitingEpoch === epoch) return load(true, force);
      return;
    }
    if (!refresh && items.value.length) return;
    const ticket = epoch, authentication = captureAuthenticationScope(), generation = clientConnection.captureGeneration();
    const current = () => ticket === epoch && authentication() && generation === clientConnection.captureGeneration();
    loading.value = true; error.value = ''; pendingRefresh = refresh;
    pending = (async () => {
      try {
        const result = refresh ? await inputCatalogApi.refresh(force, current) : await inputCatalogApi.get(current);
        if (!Array.isArray(result.inputs)) throw new Error('输入目录响应无效');
        if (current()) items.value = result.inputs;
      }
      catch { if (current()) error.value = '输入目录暂不可用'; }
      finally { if (current()) { loading.value = false; pending = undefined; } }
    })();
    return pending;
  }
  return { items, loading, error, load, invalidate };
});
