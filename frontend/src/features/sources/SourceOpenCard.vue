<script setup lang="ts">
/** 打开到小卡片；请求受理与实际播放分开，旧主机/源结果不能覆盖新选择。SCP-cv 2026-10-12。 */
import { computed, ref, watch } from 'vue';
import AButton from 'ant-design-vue/es/button';
import AModal from 'ant-design-vue/es/modal';
import { useSessionStore } from '@/stores/sessions';
import { useAuthStore } from '@/stores/auth';
import { clientConnection } from '@/platform/connection';
import type { MediaSourceItem, SessionSnapshot } from '@/services/api';
import { isConfirmedPlayback } from '../display/playbackObservation';
const props = defineProps<{ source: MediaSourceItem | null }>();
const open = defineModel<boolean>('open', { required: true });
const sessions = useSessionStore(), auth = useAuthStore();
const pending = ref<number | null>(null), errors = ref<Record<number, string>>({});
const receipts = ref<Record<number, { snapshot: SessionSnapshot; connection: number; user: number | undefined }>>({});
let sequence = 0;
watch(() => [props.source?.id, open.value], () => { sequence++; pending.value = null; errors.value = {}; receipts.value = {}; });
/** :param id: 明确逻辑目标。:returns: 仅发送一次OPEN，记录代次用于实际状态确认。 */
async function openTo(id: number): Promise<void> {
  const source = props.source;
  if (!source?.is_available || pending.value !== null) return;
  const own = sequence, connection = clientConnection.captureGeneration(), user = auth.user?.id;
  pending.value = id; errors.value[id] = '';
  try {
    const snapshot = await sessions.openSource(id, source.id, true);
    if (own === sequence && connection === clientConnection.captureGeneration() && user === auth.user?.id && snapshot)
      receipts.value[id] = { snapshot, connection, user };
  } catch (error) {
    if (own === sequence && connection === clientConnection.captureGeneration() && user === auth.user?.id)
      errors.value[id] = error instanceof Error ? error.message : '打开请求失败';
  } finally { if (own === sequence) pending.value = null; }
}
const statuses = computed(() => Object.fromEntries([1, 2].map(id => {
  if (errors.value[id]) return [id, errors.value[id]];
  if (pending.value === id) return [id, '正在提交…'];
  const receipt = receipts.value[id], current = sessions.byWindowId(id);
  if (!receipt) return [id, current?.player_online ? '可用' : '播放器离线'];
  if (receipt.connection !== clientConnection.captureGeneration() || receipt.user !== auth.user?.id) return [id, '主机或账号已更换'];
  if (!current || current.source_id !== props.source?.id || current.desired_generation !== receipt.snapshot.desired_generation) return [id, '已被后续操作替换'];
  if (!current.player_online) return [id, '请求已受理，播放器离线'];
  if (current.error_message) return [id, '打开失败：' + current.error_message];
  return [id, isConfirmedPlayback(current) ? '播放器已确认播放' : '请求已受理，等待实际输出'];
})));
</script>
<template>
  <a-modal :footer="null" v-model:open="open"  title="打开到…" class="source-open-card" style="width:min(440px,calc(100vw - 32px))">
    <p class="source-open-card__name">{{ source?.name }}</p>
    <p class="source-open-card__hint">{{ source?.path }}</p>
    <div class="source-open-card__targets">
      <div v-for="id in [1,2]" :key="id">
        <a-button block type="primary"  :loading="pending === id"
          :disabled="pending !== null || !source?.is_available || !sessions.byWindowId(id)?.player_online" @click="openTo(id)">窗口 {{ id }}</a-button>
        <p role="status">{{ statuses[id] }}</p>
      </div>
    </div>
  </a-modal>
</template>
<style scoped>
.source-open-card__name{font-weight:600;margin:0 0 6px}.source-open-card__hint{font-size:12px;color:var(--colorNeutralForeground3);overflow-wrap:anywhere}
.source-open-card__targets{display:grid;grid-template-columns:1fr 1fr;gap:12px;margin-top:16px}.source-open-card__targets p{font-size:12px;color:var(--colorNeutralForeground2);overflow-wrap:anywhere;height:48px;overflow:auto;margin:8px 0 0}
</style>
