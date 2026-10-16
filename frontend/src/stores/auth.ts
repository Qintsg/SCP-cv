/** 认证Store：保持原公开字段/动作，认证探测与请求结果按当前主机和会话寿命提交。 */
import { defineStore } from 'pinia';
import { computed, onScopeDispose, ref, watch } from 'vue';
import { api, clearApiSessionState, type AuthUser } from '@/services/api';
import { clientConnection, ConnectionSwitchInProgressError } from '@/platform/connection';
import { assertServerProfileSelected, requiresServerProfile } from '@/platform/serverSelection';
import { bindAuthenticationScope, captureAuthenticationScope, assertAuthenticationScope } from '@/services/authenticationScope';

/** 退出的短期完成凭据；只证明页面状态/本次HTTP结果，不保证网络层Cookie已删除。 */
export interface LogoutResult {
  remoteSignedOut: boolean;
  detail: string;
  isCurrent: () => boolean;
}

/** 调用页只接收自己的认证完成；现有布尔/用户返回值与平台调用保持不变。 */
export interface AuthenticationOperationOptions {
  canContinue?: () => boolean;
  onCommitted?: (isCurrent: () => boolean) => void;
}

export const useAuthStore = defineStore('auth', () => {
  const user = ref<AuthUser | null>(null);
  const initialized = ref(false);
  const isAuthenticated = computed(() => user.value !== null);
  const username = computed(() => user.value?.username ?? '');
  // 私有修订也须可观察：改密等操作未替换user，仍应立即清掉旧UI缓冲。
  const revision = ref(0);
  // 公开State/$patch也能改变身份；记录历史，不能由ID改回原值产生ABA复活。
  watch([user, () => user.value?.id], () => { revision.value++; }, { flush: 'sync' });
  let initialization: { promise: Promise<void>; isCurrent: () => boolean } | null = null;
  const binding = bindAuthenticationScope(() => ({ revision: revision.value, account: user.value, userId: user.value?.id }));
  onScopeDispose(binding.dispose);

  /** :returns: 捕获认证及主机，不能在旧CSRF失败后把旧密码继续发往新主机。 */
  function currentScope(): () => boolean {
    const authentication = captureAuthenticationScope(), generation = clientConnection.captureGeneration();
    return () => binding.isCurrent() && authentication() && generation === clientConnection.captureGeneration();
  }

  /** :param clearUser: 是否进入未知身份状态。:returns: 本次认证意图，后来的意图使它失效。 */
  function begin(clearUser: boolean): () => boolean {
    assertAuthenticationScope(binding.isCurrent);
    if (clientConnection.isSwitching) throw new ConnectionSwitchInProgressError();
    assertServerProfileSelected();
    revision.value++;
    initialization = null;
    if (clearUser) user.value = null;
    return currentScope();
  }

  /** :param next: 已验证身份或明确未登录。:returns: 原子提交本地认证，丢弃此前请求。 */
  function commit(next: AuthUser | null): void {
    revision.value++;
    initialization = null;
    user.value = next;
    initialized.value = true;
  }

  /** :returns: 共用在途启动探测；过期探测安静退出，不回写新登录或初始化标志。 */
  async function ensureInitialized(): Promise<void> {
    if (requiresServerProfile()) return;
    if (clientConnection.isSwitching) return;
    if (initialized.value) return;
    if (initialization?.isCurrent()) return initialization.promise;
    initialization = null;
    const current = currentScope();
    if (!current()) return;
    const operation = (async () => {
      try { await api.fetchCsrfToken(); }
      catch { if (!current()) return; /* 保留原CSRF读取失败不阻塞公开状态探测的行为。 */ }
      if (!current()) return;
      try {
        const payload = await api.fetchAuthStatus();
        if (current()) commit(payload.authenticated ? payload.user : null);
      } catch { if (current()) commit(null); }
    })();
    initialization = { promise: operation, isCurrent: current };
    try { await operation; }
    finally { if (initialization?.promise === operation) initialization = null; }
  }

  /** :param options: 调用页与完成归属，默认保留布尔合同。:returns: 当前主机探测结果。:raises Error: 原错误或明确归属失效。 */
  async function probeCurrentServer(options: AuthenticationOperationOptions = {}): Promise<boolean> {
    const { canContinue = () => true, onCommitted } = options;
    assertAuthenticationScope(canContinue);
    const authentication = begin(true);
    const current = () => authentication() && canContinue();
    initialized.value = false;
    const operation = (async () => {
      try { await api.fetchCsrfToken(current); }
      catch (error) { if (!current()) throw error; }
      assertAuthenticationScope(current);
      try {
        const payload = await api.fetchAuthStatus(current);
        assertAuthenticationScope(current);
        commit(payload.authenticated ? payload.user : null);
        const completed = currentScope();
        onCommitted?.(() => canContinue() && completed());
        return user.value !== null;
      } catch (error) {
        if (current()) initialized.value = true;
        throw error;
      }
    })();
    // 路由的安静初始化复用显式探测；显式调用者仍收到原错误。
    const shared = operation.then(() => undefined, () => undefined);
    initialization = { promise: shared, isCurrent: current };
    try { return await operation; }
    finally { if (initialization?.promise === shared) initialization = null; }
  }

  /** :param name: 冻结的用户名。:param password: 仅用于本次主机。:param options: 调用页及完成归属。:returns: 本次AuthUser，旧链路不提交。 */
  async function login(name: string, password: string, options: AuthenticationOperationOptions = {}): Promise<AuthUser> {
    const { canContinue = () => true, onCommitted } = options;
    assertAuthenticationScope(canContinue);
    const authentication = begin(true);
    const current = () => authentication() && canContinue();
    initialized.value = true;
    try { await api.fetchCsrfToken(current); }
    catch (error) { if (!current()) throw error; }
    assertAuthenticationScope(current);
    const payload = await api.login({ username: name, password }, current);
    assertAuthenticationScope(current);
    commit(payload.user);
    const completed = currentScope();
    onCommitted?.(() => canContinue() && completed());
    return payload.user;
  }

  /** :returns: 本次退出后的身份凭据与服务器确认；过期结果不清新身份、不转为当前失败。 */
  async function logout(): Promise<LogoutResult> {
    const current = begin(false);
    let remoteSignedOut = false, detail = '';
    try { await api.logout(); remoteSignedOut = true; }
    catch (error) {
      assertAuthenticationScope(current);
      detail = error instanceof Error ? error.message : '';
    }
    assertAuthenticationScope(current);
    commit(null);
    return { remoteSignedOut, detail, isCurrent: currentScope() };
  }

  /** :returns: 显式重读本次主机的CSRF；不重放密码或其它业务写入。 */
  async function synchronizeSession(): Promise<void> {
    const current = currentScope();
    assertAuthenticationScope(current);
    await api.fetchCsrfToken();
    assertAuthenticationScope(current);
  }

  /** :param currentPassword: 当前密码。:param newPassword: 新密码。:returns: 密码已改后，CSRF是否同步；false不代表密码失败。 */
  async function changePassword(currentPassword: string, newPassword: string): Promise<boolean> {
    const current = currentScope();
    assertAuthenticationScope(current);
    await api.changePassword({ current_password: currentPassword, new_password: newPassword });
    assertAuthenticationScope(current);
    // 服务端重签身份Cookie；即使用户ID不变，之前的响应也不能再处理401。
    revision.value++;
    clearApiSessionState();
    const synchronized = currentScope();
    try { await synchronizeSession(); return true; }
    catch { assertAuthenticationScope(synchronized); return false; }
  }

  /** :returns: 当前401触发的本地失效，不发送网络或重放业务请求。 */
  function clearLocal(): void { commit(null); }

  /** :returns: 保持旧Store reset接口；清空未知主机的本地身份并使旧探测失效。 */
  function $reset(): void {
    revision.value++;
    initialization = null;
    user.value = null;
    initialized.value = false;
  }

  return { user, initialized, isAuthenticated, username, ensureInitialized, probeCurrentServer, login, logout, changePassword, synchronizeSession, clearLocal, $reset };
});
