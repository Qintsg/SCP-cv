<script setup lang="ts">
/**
 * 设置中心开发诊断 Tab。
 * 展示前后端端口、API 端点、日志路径与环境变量来源。
 */
import { computed } from 'vue';
import { useI18n } from 'vue-i18n';
import ACard from 'ant-design-vue/es/card';

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
  <section class="developer-workspace">
    <PresentationSettings />
    <a-card :title="t('settings.apiEndpoints')">
      <ul class="settings-view__api-list">
        <li v-for="endpoint in apiEndpoints" :key="endpoint">
          <a :href="endpoint" target="_blank" rel="noopener noreferrer"><code>{{ endpoint }}</code></a>
        </li>
      </ul>
    </a-card>

    <a-card :title="t('settings.portsSummary')">
      <p class="settings-view__hint">{{ portsCaption }}</p>
    </a-card>

    <a-card :title="t('settings.logPath')">
      <p class="settings-view__hint">{{ t('settings.logPathHint') }}</p>
    </a-card>

    <a-card :title="t('settings.envVars')">
      <p class="settings-view__hint">{{ t('settings.envVarsHint') }}</p>
    </a-card>

  </section>
</template>
<style scoped>
.developer-workspace{display:grid;grid-template-columns:repeat(2,minmax(0,1fr));gap:20px;align-items:start}
.developer-workspace>:first-child{grid-column:1/-1}.developer-workspace :deep(.ant-card-body){overflow-wrap:anywhere}
.developer-workspace :deep(.presentation-settings__components){grid-template-columns:repeat(2,minmax(0,1fr));gap:12px 16px}
.developer-workspace :deep(.presentation-settings__components li){border:1px solid var(--console-border);padding:12px;border-radius:var(--borderRadiusMedium)}
@media(max-width:767px){.developer-workspace,.developer-workspace :deep(.presentation-settings__components){grid-template-columns:minmax(0,1fr)}}
</style>
