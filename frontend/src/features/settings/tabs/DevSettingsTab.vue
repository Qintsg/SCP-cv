<script setup lang="ts">
/**
 * 设置中心开发诊断 Tab。
 * 展示前后端端口、API 端点、日志路径与环境变量来源。
 */
import { computed } from 'vue';
import { useI18n } from 'vue-i18n';
import { NCard } from 'naive-ui';

import { buildBackendUrl, getConnectionPorts } from '@/services/api';
import PresentationSettings from './PresentationSettings.vue';

const { t } = useI18n();

// 端点与请求共用地址生成器，避免保存主机配置后仍指向打包时的地址。
const apiEndpoints = computed(() => ['/api/sources/', '/api/scenarios/', '/api/sessions/',
  '/api/runtime/', '/api/video-wall/layout/', '/api/events/'].map(buildBackendUrl));
const connectionPorts = getConnectionPorts();
const portsCaption = computed(() =>
  t('settings.ports', connectionPorts),
);
</script>

<template>
  <section class="settings-view__grid">
    <PresentationSettings />
    <n-card :title="t('settings.apiEndpoints')">
      <ul class="settings-view__api-list">
        <li v-for="endpoint in apiEndpoints" :key="endpoint">
          <a :href="endpoint" target="_blank" rel="noopener noreferrer"><code>{{ endpoint }}</code></a>
        </li>
      </ul>
    </n-card>

    <n-card :title="t('settings.portsSummary')">
      <p class="settings-view__hint">{{ portsCaption }}</p>
    </n-card>

    <n-card :title="t('settings.logPath')">
      <p class="settings-view__hint">{{ t('settings.logPathHint') }}</p>
    </n-card>

    <n-card :title="t('settings.envVars')">
      <p class="settings-view__hint">{{ t('settings.envVarsHint') }}</p>
    </n-card>

  </section>
</template>
