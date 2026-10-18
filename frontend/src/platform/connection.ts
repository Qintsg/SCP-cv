/** 控制端主机代次与离线围栏；共享资源订阅同步代次变化，不等待网络注销后才释放。SCP-cv 2026-10-11。 */
export type ClientConnectionStatus =
  | 'disconnected'
  | 'connecting'
  | 'unauthenticated'
  | 'connected'
  | 'stale'
  | 'reconnecting'
  | 'error';

export interface ServerProfile {
  readonly origin: string;
  readonly displayName: string;
  readonly allowInsecureDevelopment?: boolean;
}

export const SERVER_PROFILE_STORAGE_KEY = 'scp-cv.server-profile';

export function loadStoredServerProfile(
  storage: Pick<Storage, 'getItem'> = window.localStorage,
): ServerProfile | null {
  const raw = storage.getItem(SERVER_PROFILE_STORAGE_KEY);
  if (!raw) return null;
  try {
    return normalizeServerProfile(JSON.parse(raw) as ServerProfile);
  } catch {
    return null;
  }
}

export function saveServerProfile(
  profile: ServerProfile,
  storage: Pick<Storage, 'setItem'> = window.localStorage,
): void {
  storage.setItem(SERVER_PROFILE_STORAGE_KEY, JSON.stringify(normalizeServerProfile(profile)));
}

/** 仅代表本次意图，不允许调用方在await后借用后来的全局代次。 */
export interface ConnectionSwitchScope {
  readonly generation: number;
  isCurrent(): boolean;
}

export interface ConnectionCoordinatorDependencies {
  canContinue?(): boolean;
  closeEvents?(scope: ConnectionSwitchScope): void;
  clearSession?(scope: ConnectionSwitchScope): Promise<void>;
  clearStores?(scope: ConnectionSwitchScope): void;
  saveProfile?(profile: ServerProfile, scope: ConnectionSwitchScope): Promise<void>;
  refreshSnapshot?(): Promise<void>;
  openEvents?(): void;
  scheduleReconnect?(callback: () => void | Promise<void>): unknown;
}

export class OfflineCommandError extends Error {
  constructor() {
    super('控制端当前离线，操作未发送且不会自动重放。');
    this.name = 'OfflineCommandError';
  }
}

export class StaleConnectionResponseError extends Error {
  constructor() {
    super('响应属于已切换的播放主机，已丢弃。');
    this.name = 'StaleConnectionResponseError';
  }
}

/** 切换尚有实际清理/保存未结束，不允许把业务或密码发往中间地址。 */
export class ConnectionSwitchInProgressError extends Error {
  /** :returns: 不承诺自动重放的当前门禁错误。 */
  constructor() { super('播放主机正在切换，请等待当前清理结束后重试。'); this.name = 'ConnectionSwitchInProgressError'; }
}

export class ConnectionSwitchCancelledError extends Error {
  /** :returns: 页面或调用意图失效，不表示撤销已开始的原生阶段。 */
  constructor() { super('连接切换所属页面或意图已失效，旧操作已丢弃。'); this.name = 'ConnectionSwitchCancelledError'; }
}

/** :param profile: 用户地址，网页裸主机默认HTTP。:returns: 无凭据/路径的HTTP或HTTPS origin；不自动降级明确HTTPS。 */
export function normalizeServerProfile(profile: ServerProfile): ServerProfile {
  const rawOrigin = profile.origin.trim();
  if (!rawOrigin || rawOrigin.startsWith('//')) throw new Error('请输入明确的播放主机地址。');
  const url = new URL(/^[a-z][\w+.-]*:\/\//i.test(rawOrigin) ? rawOrigin : `http://${rawOrigin}`);
  const isLoopback = ['localhost', '127.0.0.1', '[::1]'].includes(url.hostname);
  const allowHttp = Boolean(profile.allowInsecureDevelopment && isLoopback);
  if (!['http:', 'https:'].includes(url.protocol)) {
    throw new Error('播放主机地址只支持HTTP或HTTPS。');
  }
  if (url.username || url.password) {
    throw new Error('播放主机地址不得包含用户名或密码。');
  }
  if ((url.pathname && url.pathname !== '/') || url.search || url.hash) {
    throw new Error('播放主机地址只能包含 scheme、host 和 port。');
  }
  return {
    origin: url.origin,
    displayName: profile.displayName.trim() || url.host,
    allowInsecureDevelopment: allowHttp,
  };
}

export class ClientConnectionCoordinator {
  private generation = 0;
  private reconnectScheduled = false;
  private dependencies: ConnectionCoordinatorDependencies;
  private currentProfile: ServerProfile | null = null;
  private pendingSwitches = 0;
  private mutationTail: Promise<void> = Promise.resolve();
  private readonly generationListeners = new Set<(generation: number) => void>();
  private readonly profileListeners = new Set<() => void>();

  status: ClientConnectionStatus = 'disconnected';

  constructor(dependencies: ConnectionCoordinatorDependencies = {}) {
    this.dependencies = dependencies;
  }

  get profile(): ServerProfile | null {
    return this.currentProfile;
  }

  /** :returns: 所有已启动的清理/保存实际结束后才解除，restore不能伪造其完成。 */
  get isSwitching(): boolean { return this.pendingSwitches > 0; }

  captureGeneration(): number {
    return this.generation;
  }

  /** :param listener: 主机寿命改变时的本地释放回调。:returns: 幂等取消订阅，不触发媒体或HTTP。 */
  onGenerationChanged(listener: (generation: number) => void): () => void {
    this.generationListeners.add(listener);
    return () => { this.generationListeners.delete(listener); };
  }

  /** :param listener: 实际地址提交后的只读观察者。:returns: 取消订阅；代次失效不等同地址已提交。 */
  onProfileChanged(listener: () => void): () => void {
    this.profileListeners.add(listener);
    return () => { this.profileListeners.delete(listener); };
  }

  /** :param profile: 已规范化的实际目标。:returns: 提交地址后同步通知本地占用视图。 */
  private commitProfile(profile: ServerProfile): void {
    this.currentProfile = profile;
    for (const listener of [...this.profileListeners]) listener();
  }

  /** :returns: 先失效原身份再同步通知资源所有者，不能等异步清会话结束。 */
  private advanceGeneration(): number {
    const generation = ++this.generation;
    for (const listener of [...this.generationListeners]) {
      if (generation !== this.generation) break;
      listener(generation);
    }
    return generation;
  }

  /** :param profile: 已验证的本地配置。:returns: 本次恢复代次，不借用回调重入后的新代次。 */
  restoreProfile(profile: ServerProfile): number {
    this.commitProfile(normalizeServerProfile(profile));
    this.status = 'disconnected';
    return this.advanceGeneration();
  }

  /** :param profile: 明确目标。:param dependencies: 受管阶段与调用归属。:returns: 本次冻结代次，失效或失败明确拒绝。 */
  async switchServer(
    profile: ServerProfile,
    dependencies: ConnectionCoordinatorDependencies = this.dependencies,
  ): Promise<number> {
    const normalized = normalizeServerProfile(profile);
    if (!(dependencies.canContinue?.() ?? true)) throw new ConnectionSwitchCancelledError();
    const generation = this.generation + 1;
    const scope: ConnectionSwitchScope = { generation,
      isCurrent: () => generation === this.generation && (dependencies.canContinue?.() ?? true) };
    const assertCurrent = () => {
      if (generation !== this.generation) throw new StaleConnectionResponseError();
      if (!(dependencies.canContinue?.() ?? true)) throw new ConnectionSwitchCancelledError();
    };
    this.pendingSwitches++;
    try {
      this.status = 'disconnected';
      this.reconnectScheduled = false;
      this.advanceGeneration();
      assertCurrent();
      dependencies.closeEvents?.(scope);
      assertCurrent();
      // 代次立即失效，无法撤销的原生阶段依次结束；新意图不与旧清理/保存并行。
      const operation = this.mutationTail.then(async () => {
        assertCurrent();
        await dependencies.clearSession?.(scope);
        assertCurrent();
        dependencies.clearStores?.(scope);
        assertCurrent();
        this.commitProfile(normalized);
        await dependencies.saveProfile?.(normalized, scope);
        assertCurrent();
        return generation;
      });
      this.mutationTail = operation.then(() => undefined, () => undefined);
      return await operation;
    } catch (error) { assertCurrent(); throw error; }
    finally { this.pendingSwitches--; }
  }

  applyIfCurrent(generation: number, apply: () => void): boolean {
    if (generation !== this.generation) return false;
    apply();
    return true;
  }

  applyEventIfCurrent(generation: number, apply: () => void): boolean {
    return this.applyIfCurrent(generation, apply);
  }

  markConnecting(): void {
    this.status = 'connecting';
  }

  markConnected(): void {
    this.status = 'connected';
    this.reconnectScheduled = false;
  }

  markDisconnected(): void {
    this.status = 'disconnected';
    this.reconnectScheduled = false;
  }

  markReconnecting(): void {
    this.status = 'reconnecting';
  }

  handleEventError(): void {
    this.status = 'reconnecting';
    if (this.reconnectScheduled) return;
    this.reconnectScheduled = true;
    const generation = this.captureGeneration();
    const reconnect = async (): Promise<void> => {
      this.reconnectScheduled = false;
      if (generation !== this.generation) return;
      try {
        await this.dependencies.refreshSnapshot?.();
        if (generation !== this.generation) return;
        this.dependencies.openEvents?.();
      } catch {
        if (generation === this.generation) this.status = 'error';
      }
    };
    if (this.dependencies.scheduleReconnect) {
      this.dependencies.scheduleReconnect(reconnect);
    } else {
      window.setTimeout(() => void reconnect(), 2000);
    }
  }

  async executeCommand<T>(command: () => Promise<T>): Promise<T> {
    if (this.status !== 'connected') throw new OfflineCommandError();
    return command();
  }
}

export const clientConnection = new ClientConnectionCoordinator();
