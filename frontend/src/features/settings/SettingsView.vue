<script setup lang="ts">
/**
 * 设置中心容器：品牌头 + 设置分组 Tab。
 * 各设置分组拆入 tabs/ 子组件，避免单文件继续堆积运行态、显示器、设备电源与开发诊断逻辑。
 */
import { tabsKeyboard as vTabsKeyboard } from '@/design-system/tabsKeyboard';

import { computed, ref } from 'vue';
import { useI18n } from 'vue-i18n';
import { TabPane as ATabPane } from 'ant-design-vue/es/tabs';
import ATabs from 'ant-design-vue/es/tabs';

import FPageHeader from '@/design-system/FPageHeader.vue';
import { useBreakpoint } from '@/composables/useBreakpoint';
import { getConnectionPorts } from '@/services/api';
import RuntimeSettingsTab from './tabs/RuntimeSettingsTab.vue';
import DisplaySettingsTab from './tabs/DisplaySettingsTab.vue';
import DevicePowerSettingsTab from './tabs/DevicePowerSettingsTab.vue';
import DevSettingsTab from './tabs/DevSettingsTab.vue';
import AccountSettingsTab from './tabs/AccountSettingsTab.vue';

type SettingsTab = 'runtime' | 'display' | 'devices' | 'account' | 'dev';

const { t } = useI18n();

const activeTab = ref<SettingsTab>('runtime');
const { isMobile } = useBreakpoint();
const connectionPorts = getConnectionPorts();
const portsCaption = computed(() => t('settings.ports', connectionPorts));
</script>

<template>
  <div class="settings-view">
    <FPageHeader title="设置" description="管理运行服务、显示器、设备电源和当前账户。">
      <template #context><p class="settings-view__brand-caption">{{ portsCaption }}</p></template>
    </FPageHeader>

    <a-tabs v-tabs-keyboard v-model:activeKey="activeTab" :tab-position="isMobile ? 'top' : 'left'" type="line" :aria-label="t('settings.tabsAria')" class="console-settings-tabs">
      <a-tab-pane key="runtime" :tab="t('settings.tabRuntime')">
        <RuntimeSettingsTab />
      </a-tab-pane>
      <a-tab-pane key="display" :tab="t('settings.tabDisplay')">
        <DisplaySettingsTab />
      </a-tab-pane>
      <a-tab-pane key="devices" :tab="t('settings.tabDevices')">
        <!-- 电源无本地表单草稿；离开页签释放交互归属，已发请求仍由共用Store收口。 -->
        <DevicePowerSettingsTab v-if="activeTab === 'devices'" />
      </a-tab-pane>
      <a-tab-pane key="account" :tab="t('settings.tabAccount')">
        <AccountSettingsTab />
      </a-tab-pane>
      <a-tab-pane key="dev" :tab="t('settings.tabDev')">
        <DevSettingsTab />
      </a-tab-pane>
    </a-tabs>
  </div>
</template>

<style src="./SettingsView.css"></style>
