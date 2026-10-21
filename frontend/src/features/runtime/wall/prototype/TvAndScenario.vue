<!-- 电视单路选源与预案去重的可交互内存示意；不包含真实协议发送。 -->
<script setup lang="ts">
import { ref } from 'vue';
import AButton from 'ant-design-vue/es/button';
import ASegmented from 'ant-design-vue/es/segmented';
import InputPicker from './InputPicker.vue';
import { inputFor } from './catalog';
import { dispatchDemo, outputMemory } from './demoOutputs';
const props = defineProps<{ page: string; variant: string; layout: string }>();
const choices = ref({ left: 63, right: 64 }), scene = ref({ wall: 'keep', left: 'keep', right: 'keep' });
const sceneChoices = ref({ left: 63, right: 64 }), feedback = ref<Record<string, string>>({});
const names = { wall: '大屏', left: '电视左', right: '电视右' };
/** :param target: 单台电视。:returns: 手动相同来源也可模拟重发。 */
function manual(target: 'left' | 'right'): void { feedback.value = { [target]: dispatchDemo(target, String(choices.value[target]), true) }; }
/** :returns: 逐输出比较，不将未配置或未变的输出重新发送。 */
function activate(): void { feedback.value = { wall: dispatchDemo('wall', scene.value.wall === 'keep' ? null : props.layout, false), left: dispatchDemo('left', scene.value.left === 'keep' ? null : String(sceneChoices.value.left), false), right: dispatchDemo('right', scene.value.right === 'keep' ? null : String(sceneChoices.value.right), false) }; }
</script>
<template>
  <section v-if="page === 'wall'" class="demo-tv-grid">
    <article v-for="target in (['left','right'] as const)" :key="target" class="demo-panel demo-tv-card"><div><h2>{{ names[target] }}</h2><span v-if="feedback[target]" class="demo-subtle-result">{{ feedback[target] }}</span></div><div class="demo-tv-source"><InputPicker :variant="variant" :ip="choices[target]" @choose="choices[target] = $event" /><a-button @click="manual(target)">切换</a-button></div></article>
  </section>
  <section v-else-if="page === 'scenario'" class="demo-panel demo-scenario">
    <div class="demo-panel-title"><h2>预案编辑</h2><a-button type="primary" @click="activate">模拟调用</a-button></div>
    <div class="demo-scenario-row"><strong>大屏布局</strong><a-segmented v-model:value="scene.wall" :options="[{value:'keep',label:'保持'},{value:'set',label:'当前布局'}]" /><span>{{ feedback.wall }}</span></div>
    <div v-for="target in (['left','right'] as const)" :key="target" class="demo-scenario-row"><strong>{{ names[target] }}</strong><a-segmented v-model:value="scene[target]" :options="[{value:'keep',label:'保持'},{value:'set',label:'切换输入'}]" /><InputPicker v-if="scene[target] === 'set'" :variant="variant" :ip="sceneChoices[target]" @choose="sceneChoices[target] = $event" /><span>{{ feedback[target] }}</span></div>
    <div class="demo-last-outputs"><h3>上次下发 <small>模拟记录</small></h3><div><span>大屏</span><code>{{ outputMemory.last.wall ? '已记录布局' : '未知' }}</code></div><div v-for="target in (['left','right'] as const)" :key="target"><span>{{ names[target] }}</span><code>{{ outputMemory.last[target] === null ? '未知' : `${inputFor(Number(outputMemory.last[target])).name} · IP${outputMemory.last[target]}` }}</code></div></div>
  </section>
</template>
