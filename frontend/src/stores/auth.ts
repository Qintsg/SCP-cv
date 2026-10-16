/** 认证Store：保持原公开字段/动作，认证探测与请求结果按当前主机和会话寿命提交。 */
import { defineStore } from 'pinia';
import { computed, onScopeDispose, ref, watch } from 'vue';
import { api, type AuthUser } from '@/services/api';
import { clientConnection } from '@/platform/connection';
import { bindAuthenticationScope, captureAuthenticationScope, assertAuthenticationScope } from '@/services/authenticationScope';

export const useAuthStore = defineStore('auth', () => {
  const user = ref<AuthUser | null>(null);
  const initialized = ref(false);
  const isAuthenticated = computed(() => user.value !== null);
  const username = computed(() => user.value?.username ?? '');
  let revision = 0;
  // 公开State/$patch也能改变身份；记录历史，不能由ID改回原值产生ABA复活。
  watch([user, () => user.value?.id], () => { revision++; }, { flush: 'sync' });
  let initialization: Promise<void> | null = null;
  const binding = bindAuthenticationScope(() => ({ revision, account: user.value, userId: user.value?.id }));
  onScopeDispose(binding.dispose);

  /** :returns: 捕获认证及主机，不能在旧CSRF失败后把旧密码继续发往新主机。 */
  function currentScope(): () => boolean {
    const authentication = captureAuthenticationScope(), generation = clientConnection.captureGeneration();
    return () => binding.isCurrent() && authentication() && generation === clientConnection.captureGeneration();
  }

  /** :param clearUser: 是否进入未知身份状态。:returns: 本次认证意图，后来的意图使它失效。 */
  function begin(clearUser: boolean): () => boolean {
    assertAuthenticationScope(binding.isCurrent);
    revision++;
    initialization = null;
    if (clearUser) user.value = null;
    return currentScope();
  }

  /** :param next: 已验证身份或明确未登录。:returns: 原子提交本地认证，丢弃此前请求。 */
  function commit(next: AuthUser | null): void {
    revision++;
    initialization = null;
    user.value = next;
    initialized.value = true;
  }

  /** :returns: 共用在途启动探测；过期探测安静退出，不回写新登录或初始化标志。 */
  async function ensureInitialized(): Promise<void> {
    if (initialized.value) return;
    if (initialization) return initialization;
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
    initialization = operation;
    try { await operation; }
    finally { if (initialization === operation) initialization = null; }
  }

  /** :returns: 当前主机探测结果。:raises Error: 原网络/认证错误或明确归属失效。 */
  async function probeCurrentServer(): Promise<boolean> {
    const current = begin(true);
    initialized.value = false;
    const operation = (async () => {
      try { await api.fetchCsrfToken(); }
      catch (error) { if (!current()) throw error; }
      assertAuthenticationScope(current);
      try {
        const payload = await api.fetchAuthStatus();
        assertAuthenticationScope(current);
        commit(payload.authenticated ? payload.user : null);
        return user.value !== null;
      } catch (error) {
        if (current()) initialized.value = true;
        throw error;
      }
    })();
    // 路由的安静初始化复用显式探测；显式调用者仍收到原错误。
    const shared = operation.then(() => undefined, () => undefined);
    initialization = shared;
    try { return await operation; }
    finally { if (initialization === shared) initialization = null; }
  }

  /** :param name: 用户名。:param password: 仅发往本次主机。:returns: 本次登录身份，旧链路不再提交。 */
  async function login(name: string, password: string): Promise<AuthUser> {
    const current = begin(true);
    initialized.value = true;
    try { await api.fetchCsrfToken(); }
    catch (error) { if (!current()) throw error; }
    assertAuthenticationScope(current);
    const payload = await api.login({ username: name, password });
    assertAuthenticationScope(current);
    commit(payload.user);
    return payload.user;
  }

  /** :returns: 退出完成；网络失败仍只清本次本地身份，不声称服务端已撤销。 */
  async function logout(): Promise<void> {
    const current = begin(false);
    try { await api.logout(); }
    finally { if (current()) commit(null); }
  }

  /** :param currentPassword: 当前密码。:param newPassword: 新密码。:returns: 当前归属密码修改完成。 */
  async function changePassword(currentPassword: string, newPassword: string): Promise<void> {
    const current = currentScope();
    assertAuthenticationScope(current);
    await api.changePassword({ current_password: currentPassword, new_password: newPassword });
    assertAuthenticationScope(current);
    // 服务端重签身份Cookie；即使用户ID不变，之前的响应也不能再处理401。
    revision++;
  }

  /** :returns: 当前401触发的本地失效，不发送网络或重放业务请求。 */
  function clearLocal(): void { commit(null); }

  /** :returns: 保持旧Store reset接口；清空未知主机的本地身份并使旧探测失效。 */
  function $reset(): void {
    revision++;
    initialization = null;
    user.value = null;
    initialized.value = false;
  }

  return { user, initialized, isAuthenticated, username, ensureInitialized, probeCurrentServer, login, logout, changePassword, clearLocal, $reset };
});
