<script setup lang="ts">
/** 主机地址协议选择；网页默认HTTP，封装端保留原HTTPS认证限制。SCP-cv 2026-10-12。 */
import { tagColor } from '@/design-system/tagColor';

import { computed, onScopeDispose, ref, watch } from 'vue';
import { useI18n } from 'vue-i18n';
import { onBeforeRouteLeave, useRouter } from 'vue-router';
import AAlert from 'ant-design-vue/es/alert';
import AButton from 'ant-design-vue/es/button';
import ACard from 'ant-design-vue/es/card';
import AForm from 'ant-design-vue/es/form';
import { FormItem as AFormItem } from 'ant-design-vue/es/form';
import AInput from 'ant-design-vue/es/input';
import ASwitch from 'ant-design-vue/es/switch';
import ATag from 'ant-design-vue/es/tag';

import FPageHeader from '@/design-system/FPageHeader.vue';
import { getClientBuildTarget } from '@/platform';
import { clientConnection, ConnectionSwitchCancelledError, normalizeServerProfile, StaleConnectionResponseError } from '@/platform/connection';
import { RequestScopeChangedError } from '@/services/authenticationScope';
import { useAuthStore } from '@/stores/auth';
import { useRuntimeStore } from '@/stores/runtime';
import { useSessionStore } from '@/stores/sessions';
import { deriveConnectionLayerSummary } from './serverConnectionState';

type ProbeState = 'idle' | 'checking' | 'reachable' | 'failed';

const { t } = useI18n();
const router = useRouter();
const auth = useAuthStore();
const runtime = useRuntimeStore();
const sessions = useSessionStore();
const isWeb = getClientBuildTarget() === 'web';

const origin = ref(runtime.serverProfile?.origin ?? '');
const displayName = ref(runtime.serverProfile?.displayName ?? '');
const allowInsecureDevelopment = ref(Boolean(runtime.serverProfile?.allowInsecureDevelopment));
const probeState = ref<ProbeState>('idle');
const probeAttempted = ref(false);
const errorMessage = ref('');
let active = true, intent = 0;
onScopeDispose(() => { active = false; intent++; });
/** :returns: 在全局初始化守卫之前释放旧探测；只看已确认路由会把后退挂在旧CSRF上。 */
function invalidateIntent(): void { intent++; probeState.value = 'idle'; probeAttempted.value = false; errorMessage.value = ''; }
onBeforeRouteLeave(invalidateIntent);
watch(() => router.currentRoute.value.fullPath, invalidateIntent, { flush: 'sync' });

const layerSummary = computed(() => deriveConnectionLayerSummary(runtime.sseStatus, sessions.sessions));
const currentProfileLabel = computed(() => runtime.serverProfile?.displayName || t('serverConnection.notConfigured'));

const controlLabel = computed(() => {
  if (probeState.value === 'checking') return t('serverConnection.controlChecking');
  if (probeState.value === 'failed' && probeAttempted.value) return t('serverConnection.controlOffline');
  switch (layerSummary.value.control) {
    case 'connected': return t('serverConnection.controlConnected');
    case 'connecting': return t('serverConnection.controlConnecting');
    case 'reconnecting': return t('serverConnection.controlReconnecting');
    default: return probeState.value === 'reachable'
      ? t('serverConnection.controlReachable')
      : t('serverConnection.controlNotChecked');
  }
});

const controlTagType = computed<'success' | 'warning' | 'error' | 'default'>(() => {
  if (layerSummary.value.control === 'connected') return 'success';
  if (probeState.value === 'failed' && probeAttempted.value) return 'error';
  if (probeState.value === 'checking' || layerSummary.value.control !== 'offline') return 'warning';
  return probeState.value === 'reachable' ? 'success' : 'default';
});

const playerLabel = computed(() => layerSummary.value.player === 'online'
  ? t('serverConnection.playerOnline', { count: layerSummary.value.onlinePlayerCount })
  : t('serverConnection.playerOffline'));

/** :returns: 规范化用户地址并检查实际控制连接；不改变明确协议或静默降级。 */
async function connect(): Promise<void> {
  if (!active || probeState.value === 'checking') return;
  const operation = ++intent, path = router.currentRoute.value.fullPath;
  const current = () => active && operation === intent && path === router.currentRoute.value.fullPath;
  errorMessage.value = '';
  probeAttempted.value = false;
  probeState.value = 'checking';
  try {
    const profile = normalizeServerProfile({ origin: origin.value, displayName: displayName.value, allowInsecureDevelopment: allowInsecureDevelopment.value });
    if (!isWeb && profile.origin.startsWith('http:') && !profile.allowInsecureDevelopment) throw new Error(t('serverConnection.packagedHttpsRequired'));
    const switched = await runtime.switchServer(profile, current);
    if (!current() || !switched.isCurrent()) return;
    origin.value = runtime.serverProfile?.origin ?? origin.value;
    displayName.value = runtime.serverProfile?.displayName ?? displayName.value;
    allowInsecureDevelopment.value = Boolean(runtime.serverProfile?.allowInsecureDevelopment);
    // 探测会创建新认证意图；先核切换凭据，再由探测自己的完成凭据接管，不拿旧Auth寿命误判自身。
    const canProbe = () => current() && switched.generation === clientConnection.captureGeneration() && !clientConnection.isSwitching;
    let committed = () => false;
    probeAttempted.value = true;
    const authenticated = await auth.probeCurrentServer({ canContinue: canProbe, onCommitted: owns => { committed = owns; } });
    if (!canProbe() || !committed()) return;
    probeState.value = 'reachable';
    if (authenticated) {
      await router.replace('/dashboard');
    }
  } catch (error) {
    if (!current() || error instanceof RequestScopeChangedError || error instanceof StaleConnectionResponseError || error instanceof ConnectionSwitchCancelledError) return;
    probeState.value = 'failed';
    errorMessage.value = error instanceof Error ? error.message : t('serverConnection.connectFailed');
  } finally { if (current() && probeState.value === 'checking') probeState.value = 'idle'; }
}

/** :returns: 明确前往当前主机登录，不携带账号秘密。 */
function openLogin(): void {
  void router.push('/login');
}
</script>

<template>
  <main class="min-h-dvh bg-canvas px-4 py-8 text-foreground sm:px-6 lg:px-8">
    <div class="mx-auto flex w-full max-w-3xl flex-col gap-5">
      <FPageHeader :title="t('serverConnection.title')" meta="SCP-cv" />

      <section class="grid gap-3 sm:grid-cols-2" :aria-label="t('serverConnection.layerStatus')">
        <a-card size="small" :title="t('serverConnection.controlLayer')">
          <a-tag :color="tagColor(controlTagType)" >{{ controlLabel }}</a-tag>
        </a-card>
        <a-card size="small" :title="t('serverConnection.playerLayer')">
          <a-tag :color="tagColor(layerSummary.player === 'online' ? 'success' : 'error')" >{{ playerLabel }}</a-tag>
        </a-card>
      </section>

      <a-card :title="t('serverConnection.formTitle')">
        <p class="mt-0 text-sm text-foreground-muted">
          {{ t('serverConnection.currentProfile', { profile: currentProfileLabel }) }}
        </p>
        <a-form layout="vertical" :model="{ origin, displayName, allowInsecureDevelopment }" @finish="connect">
          <a-form-item :label="t('serverConnection.origin')" required>
            <a-input
              v-model:value="origin"
              inputmode="url"
              v-bind="{ 'aria-label': t('serverConnection.origin') }"
              autocomplete="url"
              :disabled="probeState === 'checking'"
              :placeholder="t('serverConnection.originPlaceholder')"
            />
          </a-form-item>
          <a-form-item :label="t('serverConnection.displayName')">
            <a-input
              v-model:value="displayName"
              v-bind="{ 'aria-label': t('serverConnection.displayName') }"
              :disabled="probeState === 'checking'"
              :placeholder="t('serverConnection.displayNamePlaceholder')"
            />
          </a-form-item>
          <p class="mb-4 text-sm text-foreground-muted">{{ t(isWeb ? 'serverConnection.protocolHint' : 'serverConnection.packagedHttpsRequired') }}</p>
          <div v-if="!isWeb" class="mb-4 flex min-h-11 items-center justify-between gap-4 rounded-lg border border-stroke px-3 py-2">
            <div>
              <p class="m-0 font-medium">{{ t('serverConnection.insecureDevelopment') }}</p>
              <p class="m-0 text-sm text-foreground-muted">{{ t('serverConnection.insecureDevelopmentHint') }}</p>
            </div>
            <a-switch v-model:checked="allowInsecureDevelopment" :disabled="probeState === 'checking'" />
          </div>
          <a-alert v-if="errorMessage" type="error" :message="t('serverConnection.connectFailed')"><template #description>
            {{ errorMessage }}
          </template></a-alert>
          <a-alert v-else-if="probeState === 'reachable' && !auth.isAuthenticated" type="success" :message="t('serverConnection.hostReachable')"><template #description>
            {{ t('serverConnection.loginRequired') }}
          </template></a-alert>
          <div class="mt-5 flex flex-col gap-3 sm:flex-row">
            <a-button type="primary" html-type="submit" :loading="probeState === 'checking'" :disabled="probeState === 'checking'">
              {{ t('serverConnection.connect') }}
            </a-button>
            <a-button v-if="probeState === 'reachable' && !auth.isAuthenticated" @click="openLogin">
              {{ t('serverConnection.openLogin') }}
            </a-button>
            <a-button v-if="auth.isAuthenticated"  @click="router.push('/dashboard')">
              {{ t('serverConnection.backToConsole') }}
            </a-button>
          </div>
        </a-form>
      </a-card>

    </div>
  </main>
</template>
