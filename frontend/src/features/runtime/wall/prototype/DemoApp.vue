<!-- 输入原型A二级/B面板/C抽屉；页面内所有视频与状态均为本机模拟。 -->
<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref, watch } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import AButton from 'ant-design-vue/es/button';
import AInput from 'ant-design-vue/es/input';
import AConfigProvider from 'ant-design-vue/es/config-provider';
import ASegmented from 'ant-design-vue/es/segmented';
import ASlider from 'ant-design-vue/es/slider';
import ACheckbox from 'ant-design-vue/es/checkbox';
import FIcon from '@/design-system/FIcon.vue';
import { consoleTheme } from '@/design-system/theme/ant-theme';
import logo from '@/assets/sspu-ailab-transparent.png';
import InputPicker from './InputPicker.vue';
import PreviewTile from './PreviewTile.vue';
import { cameras, workstations, inputFor, matches, multicast } from './catalog';
import { streamStats } from './demoStreams';
import TvAndScenario from './TvAndScenario.vue';
import { dispatchDemo, outputMemory } from './demoOutputs';
interface Block { id: string; ip: number; column: number; row: number; columns: number; rows: number }
const route = useRoute(), router = useRouter();
const variant = computed(() => ['A','B','C'].includes(String(route.query.variant)) ? String(route.query.variant) : 'A');
const isDev = import.meta.env.DEV;
const page = ref(['camera','scenario'].includes(String(route.query.page)) ? String(route.query.page) : 'wall'), dark = ref(route.query.theme === 'dark'), mode = ref(String(route.query.mode || 'split')), target = ref('left'), ratio = ref(5), applied = ref('');
const folder = ref('workstation'), search = ref(''), reachable = ref(true), camera = ref(cameras.some(item => item.ip === Number(route.query.camera)) ? Number(route.query.camera) : 85), cameraAction = ref(''), preset = ref(1);
const left = ref(63), right = ref(64), auxiliary = ref(65);
const blocks = computed<Block[]>(() => {
  if (mode.value === 'single') return [{ id: 'left', ip: left.value, column: 0, row: 0, columns: 10, rows: 5 }];
  const result = [{ id: 'left', ip: left.value, column: 0, row: 0, columns: ratio.value, rows: 5 },
    { id: 'right', ip: right.value, column: ratio.value, row: 0, columns: 10 - ratio.value, rows: mode.value === 'custom' ? 3 : 5 }];
  if (mode.value === 'custom') result.push({ id: 'auxiliary', ip: auxiliary.value, column: ratio.value, row: 3, columns: 10 - ratio.value, rows: 2 });
  return result;
});
const selected = computed(() => blocks.value.find(block => block.id === target.value) || blocks.value[0]!);
const layoutKey = computed(() => JSON.stringify(blocks.value));
outputMemory.last.wall ??= layoutKey.value;
const candidates = computed(() => (folder.value === 'camera' ? cameras : workstations).filter(item => matches(item, search.value) && (!reachable.value || item.available)));
const variantNames: Record<string, string> = { A: '二级菜单', B: '分类面板', C: '侧边抽屉' };
/** :param ip: 规范来源。:returns: 只替换选中区域，稳定身份和其它视频不变。 */
function choose(ip: number): void { if (selected.value.id === 'left') left.value = ip; else if (selected.value.id === 'right') right.value = ip; else auxiliary.value = ip; applied.value = ''; }
/** :param block: 稳定布局块。:returns: 纯CSS几何，不参与任何媒体订阅依赖。 */
function position(block: Block): Record<string, string> { return { left: `${block.column * 10}%`, top: `${block.row * 20}%`, width: `${block.columns * 10}%`, height: `${block.rows * 20}%` }; }
/** :param step: 变体方向。:returns: URL可分享，仅改变选择器形态。 */
function cycle(step: number): void { const keys = ['A','B','C']; void router.replace({ query: { ...route.query, variant: keys[(keys.indexOf(variant.value) + step + 3) % 3] } }); }
/** :param event: 只接受非输入／菜单区的切变体快捷键。:returns: 不抢占搜索键盘。 */
function keyboard(event: KeyboardEvent): void { if (event.target instanceof Element && event.target.closest('input,textarea,[contenteditable],button,[data-demo-menu]')) return; if (event.key === 'ArrowLeft') cycle(-1); if (event.key === 'ArrowRight') cycle(1); }
/** :param action: 演示动作。:returns: 只改变本地反馈，不发送PTZ帧。 */
function motion(action: string): void { cameraAction.value = action; }
watch(dark, value => { document.documentElement.dataset.theme = value ? 'dark' : 'light'; }, { immediate: true });
watch(mode, () => { if (!blocks.value.some(block => block.id === target.value)) target.value = 'left'; applied.value = ''; });
onMounted(() => { document.addEventListener('keydown', keyboard); });
onBeforeUnmount(() => { document.removeEventListener('keydown', keyboard); });
</script>
<template>
  <a-config-provider :theme="consoleTheme(dark)">
    <div class="demo-shell">
      <aside class="demo-sidebar"><a href="?variant=A" class="demo-brand"><img :src="logo" alt="SSPU AILAB" /><span>SCP-cv<small>28#108 · 现场控制台</small></span></a><small class="demo-nav-heading">现场操作</small><button type="button" :class="{ active: page === 'wall' }" @click="page = 'wall'"><FIcon name="desktop_24_regular" :size="20" /><span>仪表盘</span></button><button type="button" :class="{ active: page === 'camera' }" @click="page = 'camera'"><FIcon name="camera_24_regular" :size="20" /><span>摄像机与音量</span></button><small class="demo-nav-heading">内容资源</small><button type="button" @click="page = 'wall'; folder = 'workstation'"><FIcon name="folder_24_regular" :size="20" /><span>媒体源</span></button><button type="button" :class="{ active: page === 'scenario' }" @click="page = 'scenario'"><FIcon name="folder_24_regular" :size="20" /><span>预案</span></button><div class="demo-sidebar-foot">上海第二工业大学<br /><span>仅本机交互原型</span></div></aside>
      <div class="demo-workspace"><header class="demo-topbar"><span>28#108 <b>/</b> {{ page === 'camera' ? '摄像机与音量' : '仪表盘' }}</span><div><span class="demo-prototype-badge">Demo · 不连接设备</span><a-button type="text" aria-label="切换明暗主题" @click="dark = !dark">{{ dark ? '浅色' : '深色' }}</a-button></div></header>
        <main class="demo-main">
          <div class="demo-page-title"><div><h1>{{ page === 'camera' ? '摄像机' : page === 'scenario' ? '预案' : '仪表盘' }}</h1></div></div>
          <template v-if="page === 'wall'">
            <div class="demo-output-grid"><article v-for="ip in [63,64,65,66]" :key="ip" class="demo-output"><div><strong>窗口 {{ ip - 62 }}</strong><span class="demo-ip">IP{{ ip }}</span></div><PreviewTile :ip="ip" :label="`窗口 ${ip - 62}`" /></article></div>
            <div class="demo-columns"><div class="demo-wall-column"><section class="demo-panel demo-layout-panel"><div class="demo-panel-title"><h2><FIcon name="desktop_24_regular" :size="21" />大屏布局</h2><span>32:9 · 50格</span></div>
              <div class="demo-layout-controls"><a-segmented v-model:value="mode" :options="[{ value: 'single', label: '单屏' }, { value: 'split', label: '双屏' }, { value: 'custom', label: '自定义' }]" aria-label="显示模式" /><div class="demo-current-target"><span>{{ selected.id === 'left' ? mode === 'single' ? '全屏' : '大屏左' : selected.id === 'right' ? '大屏右' : '右下区域' }}</span><InputPicker :ip="selected.ip" :variant="variant" :initial-open="route.query.menu === 'open'" @choose="choose" /></div></div>
              <div class="demo-wall" role="group" aria-label="大屏布局演示"><div class="demo-wall-grid" /><button v-for="block in blocks" :key="block.id" type="button" class="demo-wall-block" :class="{ selected: target === block.id }" :style="position(block)" :aria-label="`选择${block.id}区域`" @click="target = block.id"><PreviewTile :ip="block.ip" :label="`${inputFor(block.ip).name} · IP${block.ip}`" /><span v-if="mode === 'custom'" class="demo-block-size">{{ block.columns }} × {{ block.rows }}</span></button></div>
              <div class="demo-geometry"><div v-if="mode !== 'single'"><label>左侧宽度 <strong>{{ ratio * 10 }}%</strong></label><a-slider v-model:value="ratio" :min="2" :max="8" :step="1" aria-label="左侧比例" /></div></div>
              <div class="demo-apply"><span role="status">{{ applied }}</span><a-button type="primary" @click="applied = dispatchDemo('wall', layoutKey, true)">应用布局 <span aria-hidden="true">→</span></a-button></div>
            </section><TvAndScenario page="wall" :variant="variant" :layout="layoutKey" /></div>
            <section class="demo-panel demo-library"><div class="demo-panel-title"><h2><FIcon name="folder_24_regular" :size="21" />媒体源</h2><span>内置目录</span></div><div class="demo-folder-tabs"><button type="button" :class="{ active: folder === 'workstation' }" @click="folder = 'workstation'"><FIcon name="folder_24_regular" :size="17" />工作站 <small>24</small></button><button type="button" :class="{ active: folder === 'camera' }" @click="folder = 'camera'"><FIcon name="camera_24_regular" :size="17" />Camera <small>6</small></button></div><div class="demo-library-tools"><span>/ {{ folder === 'workstation' ? '工作站' : 'Camera' }}</span><a-input v-model:value="search" placeholder="搜索名称或 IP" aria-label="搜索目录" allow-clear /><a-checkbox v-model:checked="reachable">只看可达（模拟）</a-checkbox></div>
              <div class="demo-library-list"><button v-for="item in candidates" :key="item.ip" type="button" :disabled="!item.available" :class="{ active: selected.ip === item.ip }" @click="choose(item.ip)"><span class="demo-device-name"><FIcon :name="item.group === 'camera' ? 'camera_24_regular' : 'desktop_24_regular'" :size="19" /><span><strong>{{ item.name }}</strong><small>{{ item.aliases.join(' / ') || (item.group === 'camera' ? '摄像机输入' : '工作站输入') }}</small></span></span><span class="demo-ip">IP{{ item.ip }}</span><span class="demo-row-use">选用</span></button><p v-if="!candidates.length" class="demo-no-results">没有匹配的输入</p></div><div class="demo-library-foot">{{ candidates.length }} 项 · 点击放入当前选中区域</div>
            </section></div>
          </template>
          <template v-else-if="page === 'camera'">
            <div class="demo-camera-tabs"><a-button v-for="item in cameras" :key="item.ip" :type="camera === item.ip ? 'primary' : 'default'" @click="camera = item.ip; cameraAction = ''">{{ item.name }} <small>IP{{ item.ip }}</small></a-button></div>
            <div class="demo-camera-grid"><section class="demo-panel demo-camera-preview"><div class="demo-panel-title"><h2>{{ inputFor(camera).name }}</h2><span>IP{{ camera }} · 当前摄像机预览</span></div><PreviewTile :ip="camera" :label="inputFor(camera).name" /></section><section class="demo-panel demo-camera-controls"><h2>云台控制</h2><p v-if="camera === 76" class="demo-help">监控01为固定监控，仅提供预览。</p><template v-else><div class="demo-ptz"><a-button @pointerdown="motion('向上（本地演示）')" @pointerup="motion('已停止（本地演示）')" @pointerleave="motion('已停止（本地演示）')">↑</a-button><a-button @pointerdown="motion('向左（本地演示）')" @pointerup="motion('已停止（本地演示）')" @pointerleave="motion('已停止（本地演示）')">←</a-button><a-button @click="motion('已停止（本地演示）')">停止</a-button><a-button @pointerdown="motion('向右（本地演示）')" @pointerup="motion('已停止（本地演示）')" @pointerleave="motion('已停止（本地演示）')">→</a-button><a-button @pointerdown="motion('向下（本地演示）')" @pointerup="motion('已停止（本地演示）')" @pointerleave="motion('已停止（本地演示）')">↓</a-button></div><div class="demo-zoom"><a-button @click="motion('拉远（本地演示）')">− 拉远</a-button><a-button @click="motion('拉近（本地演示）')">＋ 拉近</a-button></div><h3>预设位置</h3><a-segmented v-model:value="preset" :options="[1,2,3,4,5,6]" aria-label="预设位置" /><div class="demo-preset-actions"><a-button type="primary" @click="motion(`调用位置 ${preset}（本地演示）`)">调用位置</a-button><a-button @click="motion(`保存位置 ${preset}（本地演示）`)">保存位置</a-button></div></template><p v-if="cameraAction" class="demo-help" role="status">{{ cameraAction }}</p></section></div>
          </template>
          <TvAndScenario :page="page === 'wall' ? 'hidden' : page" :variant="variant" :layout="layoutKey" /><details class="demo-diagnostics"><summary>诊断</summary><footer class="demo-session-info"><span><i />{{ streamStats.producers }} 条本地演示流 <b>／</b>{{ streamStats.consumers }} 个画面复用</span><span>累计创建 {{ streamStats.created }} · 已释放 {{ streamStats.closed }}</span><span class="demo-multicast">当前组播示意 {{ multicast(page === 'camera' ? camera : selected.ip) }}</span><span>模拟发送 {{ outputMemory.writes }} 次</span></footer></details>
        </main>
      </div>
      <nav v-if="isDev" class="demo-variant-switcher" aria-label="原型风格切换"><button type="button" aria-label="上一个方案" @click="cycle(-1)">←</button><span><small>选源方案</small>{{ variant }} · {{ variantNames[variant] }}</span><button type="button" aria-label="下一个方案" @click="cycle(1)">→</button></nav>
    </div>
  </a-config-provider>
</template>
