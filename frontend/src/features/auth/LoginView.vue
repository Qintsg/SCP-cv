<script setup lang="ts">
/**
 * 登录页：username + password 表单。
 *  - 单页 Card 布局，居中 360px；移动端撑满；
 *  - 表单走 Pinia auth store；
 *  - 登录成功后跳转 query.redirect 指定路径，否则回 /dashboard。
 */
import { InputPassword as AInputPassword } from 'ant-design-vue/es/input';

import { computed, onScopeDispose, ref, useId, watch } from 'vue';
import { useI18n } from 'vue-i18n';
import { onBeforeRouteLeave, onBeforeRouteUpdate, RouterLink, useRoute, useRouter } from 'vue-router';
import AAlert from 'ant-design-vue/es/alert';
import AButton from 'ant-design-vue/es/button';
import ACard from 'ant-design-vue/es/card';
import AForm from 'ant-design-vue/es/form';
import { FormItem as AFormItem } from 'ant-design-vue/es/form';
import AInput from 'ant-design-vue/es/input';

import { useToast } from '@/composables/useToast';
import { useAuthStore } from '@/stores/auth';
import { useRuntimeStore } from '@/stores/runtime';
import { clientConnection, ConnectionSwitchInProgressError, StaleConnectionResponseError } from '@/platform/connection';
import { RequestScopeChangedError } from '@/services/authenticationScope';
import FIcon from '@/design-system/FIcon.vue';
import FPageHeader from '@/design-system/FPageHeader.vue';

const { t } = useI18n();
const route = useRoute();
const router = useRouter();
const toast = useToast();
const auth = useAuthStore();
const runtime = useRuntimeStore();

const username = ref('');
const password = ref('');
const submitting = ref(false);
const checkingConnection = ref(false);
const errorMessage = ref('');
const errorId = useId();
let active = true, intent = 0;

/** :returns: 清旧意图与界面密码，不假称撤销已发出的HTTP或擦除JS字符串内存。 */
function invalidateIntent(): void { intent++; password.value = ''; submitting.value = false; checkingConnection.value = false; errorMessage.value = ''; }
onBeforeRouteLeave(invalidateIntent);
onBeforeRouteUpdate(invalidateIntent);
watch(() => [route.fullPath, runtime.connectionGeneration, runtime.serverProfile?.origin], invalidateIntent, { flush: 'sync' });
const removeGenerationWatch = clientConnection.onGenerationChanged(invalidateIntent);
onScopeDispose(() => { active = false; invalidateIntent(); removeGenerationWatch(); });

const redirectTarget = computed<string>(() => {
  const raw = route.query.redirect;
  if (typeof raw === 'string' && raw.startsWith('/') && raw !== '/login') return raw;
  return '/dashboard';
});

/** :returns: 检查当前主机并恢复已登录的会话。 */
async function retryConnection(): Promise<void> {
  if (checkingConnection.value || submitting.value) return;
  const operation = ++intent, path = route.fullPath, generation = clientConnection.captureGeneration();
  checkingConnection.value = true;
  try {
    await auth.retryInitialization();
    if (active && operation === intent && path === route.fullPath && generation === clientConnection.captureGeneration() && auth.isAuthenticated)
      await router.replace(redirectTarget.value);
  } finally {
    if (active && operation === intent) checkingConnection.value = false;
  }
}

/** :returns: 单次冻结提交；只有本页本次主机及完成认证仍有效才反馈、导航或解除忙态。 */
async function submit(): Promise<void> {
  if (!active || submitting.value || checkingConnection.value) return;
  errorMessage.value = '';
  if (!username.value.trim() || !password.value) {
    errorMessage.value = t('auth.loginFail');
    return;
  }
  const operation = ++intent, path = route.fullPath, generation = clientConnection.captureGeneration();
  const projected = runtime.connectionGeneration, origin = clientConnection.profile?.origin, projectedOrigin = runtime.serverProfile?.origin;
  const current = () => active && operation === intent && path === route.fullPath && generation === clientConnection.captureGeneration()
    && projected === runtime.connectionGeneration && origin === clientConnection.profile?.origin && projectedOrigin === runtime.serverProfile?.origin;
  const name = username.value.trim(), secret = password.value, destination = redirectTarget.value;
  let committed = () => false;
  submitting.value = true;
  try {
    const user = await auth.login(name, secret, { canContinue: current, onCommitted: owns => { committed = owns; } });
    if (!current()) return;
    password.value = '';
    if (!committed()) return;
    toast.success(t('auth.loginSuccess', { name: user.username }));
    await router.replace(destination);
  } catch (error) {
    if (!current()) return;
    if (error instanceof RequestScopeChangedError || error instanceof StaleConnectionResponseError) { password.value = ''; return; }
    if (error instanceof ConnectionSwitchInProgressError) password.value = '';
    errorMessage.value = error instanceof Error ? error.message : t('auth.loginFail');
  } finally {
    if (current()) submitting.value = false;
  }
}
</script>

<template>
  <main class="login-view">
    <div class="login-view__brand">
      <span class="login-view__brand-mark" aria-hidden="true">
        <FIcon name="tv_24_regular" :size="28" />
      </span>
    </div>
    <a-card class="login-view__card"
      :body-style="{ padding: '28px', paddingTop: '8px', overflow: 'visible' }"
      >
      <FPageHeader :title="t('auth.pageTitle')" :description="t('auth.pageSubtitle')" />
      <a-alert v-if="auth.initializationError" type="error" show-icon message="无法连接播放主机" class="login-view__connection-error">
        <template #description>{{ auth.initializationError }}</template>
        <template #action><a-button size="small" :loading="checkingConnection" @click="retryConnection">重新连接</a-button></template>
      </a-alert>
      <a-form layout="vertical" :model="{ username, password }" @finish="submit">
        <a-form-item :label="t('auth.username')" required>
          <a-input
            v-model:value="username"
            :placeholder="t('auth.usernamePlaceholder')"
            :disabled="submitting || checkingConnection"
            autocomplete="username"
            v-bind="{ 'aria-label': t('auth.username'), 'aria-describedby': errorMessage ? errorId : undefined }"
          />
        </a-form-item>
        <a-form-item :label="t('auth.password')" required>
          <a-input-password
            v-model:value="password"

            :placeholder="t('auth.passwordPlaceholder')"
            :disabled="submitting || checkingConnection"
            autocomplete="current-password"
            v-bind="{ 'aria-label': t('auth.password'), 'aria-describedby': errorMessage ? errorId : undefined }"
          />
        </a-form-item>
        <a-alert v-if="errorMessage" :id="errorId" type="error" :message="t('auth.loginFail')"><template #description>
          {{ errorMessage }}
        </template></a-alert>
        <a-button
          html-type="submit"
          type="primary"
          block
          :loading="submitting"
          :disabled="submitting || checkingConnection"
          class="login-view__submit"
        >
          {{ submitting ? t('auth.submitting') : t('auth.submit') }}
        </a-button>
      </a-form>
      <p class="login-view__hint">{{ t('auth.defaultHint') }}</p>
      <p class="login-view__server-link">
        <RouterLink to="/connect">{{ t('auth.changeServer') }}</RouterLink>
      </p>
    </a-card>
  </main>
</template>

<style scoped>
.login-view {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  min-height: 100vh;
  min-height: 100dvh;
  padding: var(--spacingVerticalXXL) var(--spacingHorizontalL);
  background: var(--colorBrandBackground2);
  background-image: radial-gradient(ellipse at 50% 30%,
    var(--colorBrandBackground2) 0%,
    var(--colorNeutralBackground2) 60%,
    var(--colorNeutralBackground1) 100%);
  background-repeat: no-repeat;
  background-attachment: fixed;
}

.login-view__brand {
  margin-bottom: var(--spacingVerticalL);
}

.login-view__brand-mark {
  display: inline-grid;
  place-items: center;
  width: 56px;
  height: 56px;
  border-radius: var(--borderRadius3XLarge);
  background: var(--colorBrandBackground);
  color: var(--colorNeutralForegroundOnBrand);
}

.login-view__card {
  width: min(460px, 100%);
  box-shadow: var(--shadow28);
}

.login-view__subtitle {
  margin: 0 0 var(--spacingVerticalL);
  color: var(--colorNeutralForeground2);
  font-size: var(--fontSizeBase300);
  line-height: var(--lineHeightBase300);
}

.login-view__submit {
  margin-top: var(--spacingVerticalM);
  height: 44px;
  font-size: 16px;
}

.login-view__connection-error { margin-bottom: var(--spacingVerticalM); }

.login-view__hint {
  margin: var(--spacingVerticalL) 0 0;
  color: var(--colorNeutralForeground3);
  font-size: var(--fontSizeBase200);
  text-align: left;
  line-height: 1.65;
  max-width: 380px;
}

.login-view__server-link {
  margin: var(--spacingVerticalM) 0 0;
  text-align: center;
}

@media (max-width: 767px) {
  .login-view__submit {
    height: 48px;
    margin-top: var(--spacingVerticalS);
  }
}

/* 收紧 Ant Design Vue 卡片标题与内容间距 */
.login-view__card :deep(.ant-card-head-title) {
  font-size: 20px;
  font-weight: 600;
}
</style>
