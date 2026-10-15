<script setup lang="ts">
/** 账户密码表单：原生提交、输入名称、错误关联及请求归属隔离。 */
import { computed, ref, useId } from 'vue';
import { useI18n } from 'vue-i18n';
import { useRouter } from 'vue-router';
import { NAlert, NButton, NCard, NForm, NFormItem, NInput } from 'naive-ui';

import { useToast } from '@/composables/useToast';
import { useAuthStore } from '@/stores/auth';
import { useSettingsRequestContext } from '../useSettingsRequestContext';

const { t } = useI18n();
const router = useRouter();
const auth = useAuthStore();
const toast = useToast();

const currentPassword = ref('');
const newPassword = ref('');
const confirmPassword = ref('');
const saving = ref(false);
const loggingOut = ref(false);
const errorMessage = ref('');
const mismatch = ref(false);
const errorId = useId();
const hintId = useId();
const canSave = computed(() => Boolean(currentPassword.value && newPassword.value && confirmPassword.value) && !saving.value && !loggingOut.value);
const captureContext = useSettingsRequestContext(() => {
  clearPasswords();
  errorMessage.value = '';
  mismatch.value = false;
  saving.value = false;
});

/** :returns: 清除本次表单中的敏感字段。 */
function clearPasswords(): void {
  currentPassword.value = '';
  newPassword.value = '';
  confirmPassword.value = '';
}

/** :returns: 验证并提交一次密码修改；归属改变后不发布旧反馈。 */
async function save(): Promise<void> {
  if (!canSave.value) return;
  errorMessage.value = '';
  mismatch.value = false;
  if (newPassword.value !== confirmPassword.value) {
    errorMessage.value = t('settings.passwordMismatch');
    mismatch.value = true;
    return;
  }
  const current = captureContext();
  const oldPassword = currentPassword.value;
  const nextPassword = newPassword.value;
  saving.value = true;
  try {
    await auth.changePassword(oldPassword, nextPassword);
    if (current()) {
      clearPasswords();
      toast.success(t('settings.passwordChanged'));
    }
  } catch (error) {
    if (current()) errorMessage.value = error instanceof Error ? error.message : t('settings.passwordChangeFail');
  } finally { if (current()) saving.value = false; }
}

/** :returns: 密码提交完成后才允许显式退出当前账户。 */
async function logout(): Promise<void> {
  if (saving.value || loggingOut.value) return;
  loggingOut.value = true;
  try {
    await auth.logout();
    toast.success(t('auth.logoutOk'));
    await router.replace('/login');
  } catch (error) {
    toast.error(t('auth.logoutFail'), error instanceof Error ? error.message : t('common.retry'));
  } finally {
    loggingOut.value = false;
  }
}
</script>

<template>
  <section class="settings-view__grid">
    <n-card :title="t('settings.accountTitle')">
      <p class="settings-view__hint">{{ t('settings.accountSignedIn', { username: auth.username }) }}</p>
      <n-form @submit.prevent="save">
        <n-form-item :label="t('settings.currentPassword')" required>
          <n-input v-model:value="currentPassword" type="password" show-password-on="click" autocomplete="current-password"
            :disabled="saving || loggingOut" :input-props="{ 'aria-label': t('settings.currentPassword'), 'aria-describedby': errorMessage && !mismatch ? errorId : undefined }" />
        </n-form-item>
        <n-form-item :label="t('settings.newPassword')" required>
          <n-input v-model:value="newPassword" type="password" show-password-on="click" autocomplete="new-password"
            :disabled="saving || loggingOut" :input-props="{ 'aria-label': t('settings.newPassword'), 'aria-describedby': errorMessage && !mismatch ? `${hintId} ${errorId}` : hintId }" />
          <template #feedback><span :id="hintId">{{ t('settings.passwordHint') }}</span></template>
        </n-form-item>
        <n-form-item :label="t('settings.confirmPassword')" required>
          <n-input v-model:value="confirmPassword" type="password" show-password-on="click" autocomplete="new-password"
            :disabled="saving || loggingOut" :input-props="{ 'aria-label': t('settings.confirmPassword'), 'aria-invalid': mismatch ? 'true' : undefined, 'aria-describedby': errorMessage ? errorId : undefined }" />
        </n-form-item>
        <n-alert v-if="errorMessage" :id="errorId" type="error" :title="t('settings.passwordChangeFail')">{{ errorMessage }}</n-alert>
        <n-button type="primary" attr-type="submit" :disabled="!canSave" :loading="saving">
          {{ t('settings.changePassword') }}
        </n-button>
      </n-form>
    </n-card>

    <n-card :title="t('settings.sessionTitle')">
      <p class="settings-view__hint">{{ t('settings.sessionHint') }}</p>
      <n-button secondary :disabled="saving || loggingOut" :loading="loggingOut" @click="logout">
        {{ t('auth.logoutAction') }}
      </n-button>
    </n-card>
  </section>
</template>
