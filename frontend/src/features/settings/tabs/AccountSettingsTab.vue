<script setup lang="ts">
/** 账户密码表单：原生提交、输入名称、错误关联及请求归属隔离。 */
import { InputPassword as AInputPassword } from 'ant-design-vue/es/input';

import { computed, ref, useId } from 'vue';
import { useI18n } from 'vue-i18n';
import { useRouter } from 'vue-router';
import AAlert from 'ant-design-vue/es/alert';
import AButton from 'ant-design-vue/es/button';
import ACard from 'ant-design-vue/es/card';
import AForm from 'ant-design-vue/es/form';
import { FormItem as AFormItem } from 'ant-design-vue/es/form';
import AInput from 'ant-design-vue/es/input';

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
    <a-card :title="t('settings.accountTitle')">
      <p class="settings-view__hint">{{ t('settings.accountSignedIn', { username: auth.username }) }}</p>
      <a-form layout="vertical" :model="{ currentPassword, newPassword, confirmPassword }" @finish="save">
        <a-form-item :label="t('settings.currentPassword')" required>
          <a-input-password v-model:value="currentPassword"   autocomplete="current-password"
            :disabled="saving || loggingOut" v-bind="{ 'aria-label': t('settings.currentPassword'), 'aria-describedby': errorMessage && !mismatch ? errorId : undefined }" />
        </a-form-item>
        <a-form-item :label="t('settings.newPassword')" required>
          <a-input-password v-model:value="newPassword"   autocomplete="new-password"
            :disabled="saving || loggingOut" v-bind="{ 'aria-label': t('settings.newPassword'), 'aria-describedby': errorMessage && !mismatch ? `${hintId} ${errorId}` : hintId }" />
          <template #extra><span :id="hintId">{{ t('settings.passwordHint') }}</span></template>
        </a-form-item>
        <a-form-item :label="t('settings.confirmPassword')" required>
          <a-input-password v-model:value="confirmPassword"   autocomplete="new-password"
            :disabled="saving || loggingOut" v-bind="{ 'aria-label': t('settings.confirmPassword'), 'aria-invalid': mismatch ? 'true' : undefined, 'aria-describedby': errorMessage ? errorId : undefined }" />
        </a-form-item>
        <a-alert v-if="errorMessage" :id="errorId" type="error" :message="t('settings.passwordChangeFail')"><template #description>{{ errorMessage }}</template></a-alert>
        <a-button type="primary" html-type="submit" :disabled="!canSave" :loading="saving">
          {{ t('settings.changePassword') }}
        </a-button>
      </a-form>
    </a-card>

    <a-card :title="t('settings.sessionTitle')">
      <p class="settings-view__hint">{{ t('settings.sessionHint') }}</p>
      <a-button  :disabled="saving || loggingOut" :loading="loggingOut" @click="logout">
        {{ t('auth.logoutAction') }}
      </a-button>
    </a-card>
  </section>
</template>
