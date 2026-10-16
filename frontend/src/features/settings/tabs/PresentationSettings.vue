<script setup lang="ts">
/**
 * 文稿运行依赖的逐项只读静态诊断，明确区别环境检查与实际播放。
 * @Project : SCP-cv
 * @File : PresentationSettings.vue
 * @Author : Qintsg
 * @Date : 2026-10-03
 */
import { tagColor } from '@/design-system/tagColor';

import { computed, onBeforeUnmount, onMounted, ref, watch } from 'vue';
import AAlert from 'ant-design-vue/es/alert';
import ACard from 'ant-design-vue/es/card';
import ATag from 'ant-design-vue/es/tag';
import AButton from 'ant-design-vue/es/button';
import { api, type PresentationSettingsItem } from '@/services/api';
import { clientConnection } from '@/platform/connection';
import { useRuntimeStore } from '@/stores/runtime';
const runtime = useRuntimeStore();
const pending = ref(false);
const error = ref('');
const snapshot = ref<PresentationSettingsItem | null>(null);
const passed = computed(() => snapshot.value?.available === true && snapshot.value.dependency_status === 'installed');
const summary = computed(() => snapshot.value ? passed.value ? '静态检查通过' : '缺失或不兼容'
  : pending.value ? '检查中' : '待检查');
let active = true;
let sequence = 0;

/** :param requestSequence: 本卡片请求代次。:param generation: 请求所属主机代次。:returns: 是否仍可采用该读回。 */
function current(requestSequence: number, generation: number): boolean {
  return active && requestSequence === sequence && generation === clientConnection.captureGeneration();
}

// 主机切换是响应式用户接口；只使旧检查失效，不自动向新主机重放GET。
watch(() => runtime.connectionGeneration, () => {
  sequence++;
  snapshot.value = null;
  error.value = '';
  pending.value = false;
});
onBeforeUnmount(() => { active = false; sequence++; });

/** :param value: 未验证的HTTP对象。:returns: 是否为可检查字段的普通对象。 */
function record(value: unknown): value is Record<string, unknown> {
  return Boolean(value) && typeof value === 'object' && !Array.isArray(value);
}

/**
 * 结构不完整或结论矛盾时保持未知，不凭available字段单独宣称检查通过。
 * :param value: 设置接口返回的诊断对象。
 * :returns: 完整且一致的静态诊断。
 * :raises Error: 必需字段、组件身份或状态关系无效。
 */
function checkedSnapshot(value: unknown): PresentationSettingsItem {
  const invalid = () => new Error('文稿运行依赖诊断响应无效，未采用检查结果。');
  if (!record(value) || value.default_engine !== 'libreoffice' || typeof value.available !== 'boolean'
    || typeof value.dependency_status !== 'string' || !['installed', 'unavailable'].includes(value.dependency_status)
    || typeof value.detail !== 'string' || !Array.isArray(value.components) || value.components.length === 0
    || !record(value.capabilities)) throw invalid();
  const statuses: string[] = [];
  const ids = new Set<string>();
  for (const component of value.components) {
    if (!record(component) || typeof component.id !== 'string' || !component.id.trim()
      || ids.has(component.id) || typeof component.status !== 'string'
      || !['installed', 'unavailable'].includes(component.status)
      || typeof component.detail !== 'string' || typeof component.reason_code !== 'string') throw invalid();
    ids.add(component.id);
    statuses.push(component.status);
  }
  const capabilityKeys = ['slide_navigation', 'effect_navigation', 'media_pause', 'media_resume',
    'media_seek', 'media_volume', 'media_mute'];
  const capabilities = value.capabilities;
  if (capabilityKeys.some((key) => typeof capabilities[key] !== 'boolean')
    || value.available !== (value.dependency_status === 'installed')
    || value.available !== statuses.every((status) => status === 'installed')) throw invalid();
  return value as unknown as PresentationSettingsItem;
}

/** :returns: 当前主机实际配置读回，不将依赖存在当实际出画。 */
async function refresh(): Promise<void> {
  if (!active || pending.value) return;
  const requestSequence = ++sequence;
  const generation = clientConnection.captureGeneration();
  pending.value = true;
  error.value = '';
  // 新读取未完成或失败时是未知，不保留旧通过标签，也不伪造成新的缺依赖结论。
  snapshot.value = null;
  try {
    const response = await api.getPresentationSettings();
    if (current(requestSequence, generation)) {
      if (response.success !== true) throw new Error('文稿运行依赖诊断响应无效，未采用检查结果。');
      snapshot.value = checkedSnapshot(response.settings);
    }
  } catch (failure) {
    if (current(requestSequence, generation)) error.value = failure instanceof Error ? failure.message : '无法读取播放主机配置';
  } finally { if (current(requestSequence, generation)) pending.value = false; }
}
onMounted(refresh);
</script>
<template>
  <a-card title="演示文稿播放" class="presentation-settings">
    <a-tag :color="tagColor(passed ? 'success' : 'warning')">LibreOffice · {{ summary }}</a-tag>
    <p>{{ snapshot?.detail }}</p>
    <ul v-if="snapshot?.components?.length" class="presentation-settings__components" aria-label="运行依赖检查项目">
      <li v-for="component in snapshot.components" :key="component.id" :data-dependency-id="component.id">
        <div class="presentation-settings__component-heading">
          <strong>{{ component.id }}</strong>
          <a-tag :color="tagColor(component.status === 'installed' ? 'success' : 'warning')" >
            {{ component.status === 'installed' ? '静态检查通过' : '缺失或不兼容' }}
          </a-tag>
        </div>
        <p>{{ component.detail }}</p>
        <code v-if="component.reason_code">原因码：{{ component.reason_code }}</code>
      </li>
    </ul>
    <a-alert v-if="error" type="error"><template #description>{{ error }}</template></a-alert>
    <a-alert type="info"><template #description>仅进行静态检查，不证明原生引擎启动、文稿兼容或实际播放成功。统一媒体暂停、恢复、定位及窗口音量暂不可用。</template></a-alert>
    <a-button :loading="pending" :disabled="pending" @click="refresh">重新检查</a-button>
  </a-card>
</template>
<style scoped>
/** 能力说明和重新检查动作留出明确间距。 */
.presentation-settings :deep(.ant-alert) { margin-bottom: 12px; }
.presentation-settings__components {
  display: grid; gap: var(--spacingVerticalM); padding: 0;
  margin-bottom: var(--spacingVerticalL); list-style: none;
}
.presentation-settings__components li { min-width: 0; }
.presentation-settings__components li + li {
  border-top: var(--strokeWidthThin) solid var(--colorNeutralStroke2);
  padding-top: var(--spacingVerticalM);
}
.presentation-settings__component-heading {
  display: flex; flex-wrap: wrap; gap: var(--spacingHorizontalS); align-items: center;
}
.presentation-settings__components p { margin: var(--spacingVerticalS) 0; }
.presentation-settings__components strong, .presentation-settings__components code { overflow-wrap: anywhere; }
</style>
