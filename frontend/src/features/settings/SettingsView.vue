<script setup lang="ts">
/**
 * 设置中心容器：品牌头 + 设置分组 Tab。
 * 各设置分组拆入 tabs/ 子组件，避免单文件继续堆积运行态、显示器、设备电源与开发诊断逻辑。
 */
import { computed, ref } from 'vue';
import { useI18n } from 'vue-i18n';
import { NTabPane, NTabs } from 'naive-ui';

import FPageHeader from '@/design-system/FPageHeader.vue';
import { getConnectionPorts } from '@/services/api';
import RuntimeSettingsTab from './tabs/RuntimeSettingsTab.vue';
import DisplaySettingsTab from './tabs/DisplaySettingsTab.vue';
import DevicePowerSettingsTab from './tabs/DevicePowerSettingsTab.vue';
import DevSettingsTab from './tabs/DevSettingsTab.vue';
import AccountSettingsTab from './tabs/AccountSettingsTab.vue';

type SettingsTab = 'runtime' | 'display' | 'devices' | 'account' | 'dev';

const { t } = useI18n();

const activeTab = ref<SettingsTab>('runtime');
const connectionPorts = getConnectionPorts();
const portsCaption = computed(() => t('settings.ports', connectionPorts));
</script>

<template>
  <div class="settings-view">
    <FPageHeader title="设置" description="管理运行服务、显示器、设备电源和当前账户。">
      <template #context><p class="settings-view__brand-caption">{{ portsCaption }}</p></template>
    </FPageHeader>

    <n-tabs v-model:value="activeTab" type="line" :aria-label="t('settings.tabsAria')">
      <n-tab-pane name="runtime" :tab="t('settings.tabRuntime')">
        <RuntimeSettingsTab />
      </n-tab-pane>
      <n-tab-pane name="display" :tab="t('settings.tabDisplay')">
        <DisplaySettingsTab />
      </n-tab-pane>
      <n-tab-pane name="devices" :tab="t('settings.tabDevices')">
        <DevicePowerSettingsTab />
      </n-tab-pane>
      <n-tab-pane name="account" :tab="t('settings.tabAccount')">
        <AccountSettingsTab />
      </n-tab-pane>
      <n-tab-pane name="dev" :tab="t('settings.tabDev')">
        <DevSettingsTab />
      </n-tab-pane>
    </n-tabs>
  </div>
</template>

<style src="./SettingsView.css"></style>
