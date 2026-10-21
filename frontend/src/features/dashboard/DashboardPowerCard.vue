<!-- 现场电源沿用确认/意图锁；按钮内进度仅表示帧发送，不推断实体电源。 -->
<script setup lang="ts">
import { useI18n } from 'vue-i18n';
import AButton from 'ant-design-vue/es/button';
import ACard from 'ant-design-vue/es/card';
import FIcon from '@/design-system/FIcon.vue';
import { useDevicePowerActions } from '@/composables/useDevicePowerActions';
const {t}=useI18n();
const {powerOnSplice,powerOffSplice,toggleTv,isPending,isLoading}=useDevicePowerActions('dashboard');
</script>
<template>
  <a-card :title="t('dashboard.powerTitle')" class="dashboard-power">
    <div class="dashboard-power__row dashboard-power__row--wall"><strong>{{ t('dashboard.splice') }}</strong><div class="dashboard-power__pair">
      <a-button type="primary" class="dashboard-power__action" :aria-label="t('dashboard.powerOn')" :class="{'is-sending':isLoading('splice_screen','on')}" :disabled="isPending('splice_screen')" :aria-busy="isLoading('splice_screen','on')" @click="powerOnSplice"><FIcon name="power_24_regular" :size="16" /><span>{{ isLoading('splice_screen','on') ? '发送中' : t('dashboard.powerOn') }}</span><span v-if="isLoading('splice_screen','on')" class="dashboard-power__progress" role="progressbar" aria-label="拼接屏开机指令发送中" /></a-button>
      <a-button danger class="dashboard-power__action" :aria-label="t('dashboard.powerOff')" :class="{'is-sending':isLoading('splice_screen','off')}" :disabled="isPending('splice_screen') && !isLoading('splice_screen','off')" :aria-disabled="isPending('splice_screen')" :aria-busy="isLoading('splice_screen','off')" @click="powerOffSplice"><FIcon name="plug_disconnected_24_regular" :size="16" /><span>{{ isLoading('splice_screen','off') ? '发送中' : t('dashboard.powerOff') }}</span><span v-if="isLoading('splice_screen','off')" class="dashboard-power__progress" role="progressbar" aria-label="拼接屏关机指令发送中" /></a-button>
    </div></div>
    <div v-for="target in [{id:'tv_left',label:t('dashboard.tvLeft')},{id:'tv_right',label:t('dashboard.tvRight')}] as const" :key="target.id" class="dashboard-power__row"><strong>{{ target.label }}</strong><a-button class="dashboard-power__toggle" :disabled="isPending(target.id)" :loading="isLoading(target.id,'toggle')" @click="toggleTv(target.id,target.label)"><template #icon><FIcon name="arrow_swap_24_regular" :size="16" /></template>{{ t('dashboard.toggleState') }}</a-button></div>
  </a-card>
</template>
<style scoped>
.dashboard-power__row{display:grid;grid-template-columns:55px minmax(0,1fr);align-items:center;gap:12px;margin-top:10px}.dashboard-power__row:first-child{margin-top:0}.dashboard-power__row strong{font-size:12px;color:var(--console-text);font-weight:600}.dashboard-power__pair{display:grid;grid-template-columns:repeat(2,minmax(0,1fr));gap:8px}.dashboard-power__action{position:relative;overflow:hidden;display:flex;align-items:center;justify-content:center;gap:5px;min-height:36px;padding:4px 8px}.dashboard-power__action>span:not(.dashboard-power__progress),.dashboard-power__action :deep(svg){position:relative;z-index:1}.dashboard-power__action.is-sending{cursor:progress}.dashboard-power__action.is-sending:disabled{color:var(--console-primary);border-color:var(--console-primary);background:var(--console-brand-tint)}.dashboard-power__progress{position:absolute;inset:0;pointer-events:none;background:color-mix(in srgb,currentColor 10%,transparent)}.dashboard-power__progress::before{content:'';position:absolute;inset:0;width:45%;background:color-mix(in srgb,currentColor 20%,transparent);animation:power-send 1.25s ease-in-out infinite}.dashboard-power__toggle{width:100%;min-height:34px;font-size:12px}.dashboard-power__toggle :deep(.ant-btn-icon){display:inline-flex;align-items:center}@keyframes power-send{from{transform:translateX(-110%)}to{transform:translateX(230%)}}@media(prefers-reduced-motion:reduce){.dashboard-power__progress::before{animation:none;width:100%;opacity:.5}}@media(max-width:767px){.dashboard-power__action,.dashboard-power__toggle{min-height:44px}}
</style>
