<!-- 预案两台电视各选单路输入；保持不会夹带旧输入，不新增原生播放窗口。 -->
<script setup lang="ts">
import ARadio from 'ant-design-vue/es/radio';
import { RadioGroup as ARadioGroup } from 'ant-design-vue/es/radio';
import InputSourcePicker from '@/features/runtime/inputs/InputSourcePicker.vue';
import FIcon from '@/design-system/FIcon.vue';
import type { ScenarioAdvanced, VideoWallMappingItem } from '@/services/apiTypes';
import { setTelevisionInput } from './scenarioTelevisions';
const value = defineModel<ScenarioAdvanced>({ required: true });
const targets = [{ id: 'tv_left', name: '电视左' }, { id: 'tv_right', name: '电视右' }] as const;
/** :param target: 单台电视。:param state: 明确保持/切换。:returns: 保持时去掉来源，防止旧值恢复。 */
function setState(target: 'tv_left' | 'tv_right', state: unknown): void {
  if (state !== 'keep' && state !== 'set') return;
  const previous = value.value.televisions?.[target];
  value.value = { ...value.value, televisions: { ...value.value.televisions,
    [target]: state === 'keep' ? { state: 'keep' } : { state: 'set', input: { ...(previous?.input || { kind: 'window_1' }) } } } };
}
/** :param target: 当前目标。:param input: 明确选择。:returns: 更新高级值，保留另一台/摄像机/音量。 */
function choose(target: 'tv_left' | 'tv_right', input: VideoWallMappingItem['input']): void {
  value.value = { ...value.value, televisions: setTelevisionInput(value.value.televisions, target, input) };
}
</script>
<template>
  <section class="scenario-televisions" aria-label="预案电视显示">
    <h3><FIcon name="desktop_24_regular" :size="16" />电视显示</h3>
    <div v-for="target in targets" :key="target.id" class="scenario-television" role="group" :aria-label="target.name+'预案配置'">
      <div class="scenario-television__heading"><span>{{ target.name }}</span><a-radio-group :value="value.televisions?.[target.id]?.state || 'keep'" :aria-label="target.name+'显示操作'" @update:value="setState(target.id,$event)"><a-radio value="keep">保持</a-radio><a-radio value="set">切换</a-radio></a-radio-group></div>
      <InputSourcePicker v-if="value.televisions?.[target.id]?.state==='set'" :value="value.televisions?.[target.id]?.input || {kind:'window_1'}" :aria-label="target.name+'预案输入'" @choose="choose(target.id,$event)" />
    </div>
  </section>
</template>
<style scoped>
.scenario-televisions{padding:14px;border:1px solid var(--console-border);border-radius:8px;margin:16px 0;background:var(--console-surface)}
h3{display:flex;align-items:center;gap:8px;margin:0 0 14px;font-size:13px;font-weight:600;color:var(--console-text)}
.scenario-television+.scenario-television{border-top:1px solid var(--console-border);margin-top:12px;padding-top:12px}
.scenario-television__heading{display:flex;align-items:center;justify-content:space-between;gap:10px;margin-bottom:8px;min-height:32px}.scenario-television__heading>span{font-size:13px;font-weight:500;color:var(--console-text)}
.scenario-television :deep(.ant-radio-wrapper){margin-inline-end:10px;font-size:12px}
@media(max-width:640px){.scenario-television__heading{min-height:44px}.scenario-television :deep(.ant-radio-wrapper){min-height:44px;align-items:center}.scenario-television :deep(.input-source-trigger){min-height:44px}}
</style>
