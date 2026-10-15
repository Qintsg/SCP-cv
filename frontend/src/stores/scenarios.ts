/*
 * 预案 Store：列表、置顶、激活、新建/编辑/删除。
 * 设计稿 §4.5：列表 + 预览 Drawer + 编辑覆盖大卡共用。
 */
import { defineStore } from 'pinia';

import { api, type AuthUser, type ScenarioItem, type ScenarioPayload } from '@/services/api';
import { scenarioDirectoryApi } from '@/services/scenarioDirectoryApi';
import { clientConnection } from '@/platform/connection';

import { useAuthStore } from './auth';
import { useSessionStore } from './sessions';

interface ScenarioState {
  folders: import('@/services/api').MediaFolderItem[];
  scenarios: ScenarioItem[];
  loading: boolean;
  loadError: string;
  /** 当前读取身份；reset回0，后续读取使用新编号，旧finally不能解除新loading。 */
  loadRequestId: number;
}

let nextLoadRequestId = 0;
// 读取归属不进入可序列化业务状态；同ID重新登录也有新的认证对象。
const refreshScopes = new WeakMap<ScenarioState, { generation: number; user: AuthUser | null }>();

export const useScenarioStore = defineStore('scenarios', {
  state: (): ScenarioState => ({
    scenarios: [],
    folders: [],
    loading: false,
    loadError: '',
    loadRequestId: 0,
  }),
  getters: {
    /** 置顶预案排在前；置顶组内以后置顶的更靠前。 */
    sorted(state): ScenarioItem[] {
      const list = [...state.scenarios];
      list.sort((a, b) => {
        const aPinned = a.sort_order > 0 ? 0 : 1;
        const bPinned = b.sort_order > 0 ? 0 : 1;
        if (aPinned !== bPinned) return aPinned - bPinned;
        if (aPinned === 0) return b.sort_order - a.sort_order || a.name.localeCompare(b.name, 'zh-Hans-CN');
        return a.name.localeCompare(b.name, 'zh-Hans-CN');
      });
      return list;
    },
  },
  actions: {
    /**
     * 同次列表与目录均成功后提交完整快照；失败保持旧数据与可见原因。
     * :returns: 当前读取完成；失败继续抛出，过期读取不能改变当前快照或提示。
     */
    async refresh(): Promise<void> {
      const requestId = ++nextLoadRequestId;
      const generation = clientConnection.captureGeneration();
      const auth = useAuthStore();
      const user = auth.user;
      const previousScope = refreshScopes.get(this);
      if (previousScope && (previousScope.generation !== generation || previousScope.user !== user)) {
        this.$patch({ scenarios: [], folders: [], loadError: '' });
      }
      refreshScopes.set(this, { generation, user });
      const ownsRequest = () => this.loadRequestId === requestId;
      const current = () => ownsRequest() && generation === clientConnection.captureGeneration() && user === auth.user;
      this.loadRequestId = requestId;
      this.loading = true;
      try {
        const [payload, directories] = await Promise.all([api.listScenarios(), scenarioDirectoryApi.list()]);
        if (!current()) return;
        this.$patch({ scenarios: payload.scenarios, folders: directories.folders, loadError: '' });
      } catch (error) {
        if (current()) this.loadError = error instanceof Error ? error.message : '预设加载失败，请重试。';
        throw error;
      } finally {
        if (ownsRequest()) this.loading = false;
      }
    },
    async create(payload: ScenarioPayload): Promise<ScenarioItem> {
      const result = await api.createScenario(payload);
      this.scenarios = [result.scenario, ...this.scenarios];
      return result.scenario;
    },
    async update(scenarioId: number, payload: ScenarioPayload): Promise<ScenarioItem> {
      const result = await api.updateScenario(scenarioId, payload);
      this.scenarios = this.scenarios.map((item) => (item.id === scenarioId ? result.scenario : item));
      return result.scenario;
    },
    async remove(scenarioId: number): Promise<void> {
      await api.deleteScenario(scenarioId);
      this.scenarios = this.scenarios.filter((item) => item.id !== scenarioId);
    },
    async pin(scenarioId: number): Promise<ScenarioItem> {
      const result = await api.pinScenario(scenarioId);
      this.scenarios = this.scenarios.map((item) => (item.id === scenarioId ? result.scenario : item));
      return result.scenario;
    },
    async activate(scenarioId: number): Promise<void> {
      const payload = await api.activateScenario(scenarioId);
      // activate 会把当前播放快照写入 sessions，由 SessionStore 统一持有。
      useSessionStore().applyRemoteSessions(payload.sessions);
    },
  },
});
