<!-- 两台电视各自单源；只恢复逻辑记账，手动应用始终允许重发。 -->
<script setup lang="ts">
import { onBeforeUnmount, onMounted, ref } from 'vue';
import AAlert from 'ant-design-vue/es/alert';
import AButton from 'ant-design-vue/es/button';
import ACard from 'ant-design-vue/es/card';
import FIcon from '@/design-system/FIcon.vue';
import InputSourcePicker from '@/features/runtime/inputs/InputSourcePicker.vue';
import { useRequestContext } from '@/composables/useRequestContext';
import { useAuthStore } from '@/stores/auth';
import { clientConnection } from '@/platform/connection';
import { displayRoutingApi, type TelevisionTarget } from '@/services/displayRoutingApi';
import type { VideoWallMappingItem } from '@/services/apiTypes';
const auth = useAuthStore();
const targets = [{id:'tv_left',label:'电视左'},{id:'tv_right',label:'电视右'}] as const;
const choices = ref<Record<TelevisionTarget, VideoWallMappingItem['input']>>({tv_left:{kind:'window_1'},tv_right:{kind:'window_2'}});
const sending = ref<TelevisionTarget | null>(null), loading = ref(false), error = ref(''), loaded = ref(false);
let request = 0, alive = true;
const capture = useRequestContext(() => {
  const ticket=++request; sending.value=null; loading.value=false; loaded.value=false; error.value=''; choices.value={tv_left:{kind:'window_1'},tv_right:{kind:'window_2'}};
  queueMicrotask(() => { if(alive && ticket===request && auth.isAuthenticated && !clientConnection.isSwitching) void load(); });
});
/** :returns: 无物理副作用的发送记录读取；过期结果不得覆盖当前选择。 */
async function load(): Promise<void> {
  if (!alive || !auth.isAuthenticated || clientConnection.isSwitching || sending.value || loading.value) return;
  const scope = capture(), ticket = ++request, current = () => scope() && ticket === request;
  loading.value = true; error.value = '';
  try {
    const response = await displayRoutingApi.get(current); if (!current()) return;
    if (!response.outputs?.tv_left || !response.outputs.tv_right) throw new Error('电视配置读取失败');
    for (const target of targets) { const input = response.outputs[target.id].last_sent?.input; if (input) choices.value[target.id] = {...input}; }
    loaded.value = true;
  } catch (failure) { if (current()) error.value = failure instanceof Error ? failure.message : '电视配置读取失败'; }
  finally { if (current()) loading.value = false; }
}
/** :param target: 冻结目标。:returns: 手动允许同源重发；失败不重试，不把写出说成出画。 */
async function apply(target: TelevisionTarget): Promise<void> {
  if (sending.value || loading.value || !auth.isAuthenticated || clientConnection.isSwitching) return;
  const scope = capture(), ticket = ++request, current = () => scope() && ticket === request;
  const input = {...choices.value[target]}; sending.value = target; error.value = '';
  try { await displayRoutingApi.apply(target,input,current); }
  catch (failure) { if (current()) error.value = failure instanceof Error ? failure.message : '电视内容发送失败'; }
  finally { if (current()) sending.value = null; }
}
onMounted(() => { void load(); });
onBeforeUnmount(() => { alive=false; request++; });
</script>
<template>
  <a-card title="电视内容" class="television-routing" :aria-busy="loading || !!sending">
    <template #extra><a-button type="text" size="small" aria-label="重新读取电视配置" :disabled="!!sending" :loading="loading" @click="load"><template #icon><FIcon name="arrow_clockwise_20_regular" :size="14" /></template></a-button></template>
    <div v-for="target in targets" :key="target.id" class="television-routing__row">
      <label :for="`television-${target.id}`">{{ target.label }}</label>
      <div class="television-routing__controls"><InputSourcePicker :id="`television-${target.id}`" :value="choices[target.id]" :disabled="loading || !!sending" :aria-label="`${target.label}输入来源`" @choose="choices[target.id]=$event" />
        <a-button :loading="sending===target.id" :disabled="loading || !!sending && sending!==target.id" :aria-label="`应用${target.label}内容`" @click="apply(target.id)"><FIcon name="arrow_right_24_regular" :size="16" /></a-button></div>
    </div>
    <a-alert v-if="error" type="error" :message="error" role="alert" /><a-button v-if="error && !loaded" size="small" @click="load">重试读取</a-button>
  </a-card>
</template>
<style scoped>
.television-routing__row{display:grid;grid-template-columns:45px minmax(0,1fr);align-items:center;gap:8px;margin-bottom:12px}.television-routing__row:last-of-type{margin-bottom:0}.television-routing__row>label{font-size:12px;font-weight:600;color:var(--console-muted)}.television-routing__controls{display:grid;grid-template-columns:minmax(0,1fr) 34px;gap:8px}.television-routing__controls>.ant-btn{padding:0;display:grid;place-items:center}.television-routing .ant-alert{margin-top:12px}
@media(max-width:767px){.television-routing__controls{grid-template-columns:minmax(0,1fr) 44px}.television-routing__controls>.ant-btn{min-height:44px}}
</style>
