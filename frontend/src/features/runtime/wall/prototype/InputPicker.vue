<!-- 三种可丢弃选源变体；统一内存选源事件，不调用设备或媒体API。 -->
<script setup lang="ts">
import { computed, ref, watch } from 'vue';
import AButton from 'ant-design-vue/es/button';
import AInput from 'ant-design-vue/es/input';
import APopover from 'ant-design-vue/es/popover';
import ADrawer from 'ant-design-vue/es/drawer';
import FIcon from '@/design-system/FIcon.vue';
import { inputs, inputFor, matches, type DemoInput } from './catalog';
const props = defineProps<{ ip: number; variant: string; initialOpen?: boolean }>();
const emit = defineEmits<{ choose: [ip: number] }>();
const open = ref(!!props.initialOpen), other = ref(true), query = ref(''), group = ref('all'), custom = ref(false), customIp = ref('');
const secondary = computed(() => inputs.filter(item => item.group !== 'site' && matches(item, query.value) && (group.value === 'all' || item.group === group.value)));
const quick = [{ ip: 63, label: '窗口 1' }, { ip: 64, label: '窗口 2' }, { ip: 81, label: 'IP81 · 现场' }, { ip: 65, label: '窗口 3' }, { ip: 66, label: '窗口 4' }];
const label = computed(() => quick.find(item => item.ip === props.ip)?.label || inputFor(props.ip).name);
const windowWidth = window.innerWidth;
/** :param item: 明确选中的模拟输入。:returns: 只更新本地目标。 */
function choose(item: DemoInput): void { if (!item.available) return; emit('choose', item.ip); open.value = false; }
/** :returns: 自定义末段只影响内存演示，不进行网络访问。 */
function chooseCustom(): void { const ip = Number(customIp.value); if (!Number.isInteger(ip) || ip < 1 || ip > 254 || ip === 75) return; emit('choose', ip); open.value = false; }
watch(() => props.variant, () => { open.value = false; });
</script>
<template>
  <a-popover v-if="variant !== 'C'" v-model:open="open" trigger="click" placement="bottomLeft" :overlay-style="{ maxWidth: 'calc(100vw - 24px)' }" overlay-class-name="prototype-picker-overlay">
    <template #content>
      <div class="demo-picker" :class="{ 'demo-picker--panel': variant === 'B' }" data-demo-menu>
        <nav class="demo-picker-roots" aria-label="输入类别">
          <button v-for="item in quick" :key="item.ip" type="button" :class="{ active: ip === item.ip }" @click="choose(inputFor(item.ip))"><span>{{ item.label }}</span><small>IP{{ item.ip }}</small></button>
          <button type="button" @click="custom = !custom; other = false"><span>自定义 IP</span><FIcon name="add_24_regular" :size="16" /></button>
          <button type="button" class="demo-other-entry" :class="{ active: other }" @click="other = true; custom = false"><span>其他输入</span><FIcon name="chevron_right_24_regular" :size="16" /></button>
        </nav>
        <section v-if="other" class="demo-picker-secondary">
          <div class="demo-picker-search"><a-input v-model:value="query" placeholder="搜索名称或 IP，如 65" aria-label="搜索其他输入" allow-clear /><div class="demo-filter-tabs"><button v-for="item in [{ value: 'all', label: '全部' }, { value: 'workstation', label: '工作站' }, { value: 'camera', label: '摄像机' }]" :key="item.value" type="button" :class="{ active: group === item.value }" @click="group = item.value">{{ item.label }}</button></div></div>
          <div class="demo-picker-list" role="group" aria-label="其他输入列表">
            <button v-for="item in secondary" :key="item.ip" type="button" :disabled="!item.available" :class="{ active: ip === item.ip }" @click="choose(item)"><span class="demo-device-name"><FIcon :name="item.group === 'camera' ? 'camera_24_regular' : 'desktop_24_regular'" :size="17" /><span>{{ item.name }}<small v-if="item.aliases.length">{{ item.aliases.join(' / ') }}</small></span></span><span class="demo-ip">IP{{ item.ip }} <span v-if="!item.available" class="demo-offline">离线*</span><span v-else-if="ip === item.ip">✓</span></span></button>
            <div v-if="!secondary.length" class="demo-no-results">没有匹配的输入</div>
          </div><div class="demo-picker-foot">{{ secondary.length }} 项</div>
        </section>
        <section v-if="custom" class="demo-custom-ip"><strong>自定义输入</strong><p>192.168.5.<a-input v-model:value="customIp" placeholder="90" aria-label="自定义IP末段" @pressEnter="chooseCustom" /></p><a-button type="primary" :disabled="!/^\d{1,3}$/.test(customIp) || +customIp < 1 || +customIp > 254 || +customIp === 75" @click="chooseCustom">使用此输入</a-button></section>
      </div>
    </template>
    <a-button block class="demo-source-trigger" @click="other = true; custom = false"><span>{{ label }} <small>IP{{ ip }}</small></span><FIcon name="chevron_down_24_regular" :size="16" /></a-button>
  </a-popover>
  <template v-else>
    <a-button block class="demo-source-trigger" @click="open = true"><span>{{ label }} <small>IP{{ ip }}</small></span><FIcon name="chevron_right_24_regular" :size="16" /></a-button>
    <a-drawer :open="open" title="选择输入来源" :width="Math.min(520, 0.94 *  windowWidth)" @close="open = false">
      <div class="demo-drawer-quick"><a-button v-for="item in quick" :key="item.ip" :type="ip === item.ip ? 'primary' : 'default'" @click="choose(inputFor(item.ip))">{{ item.label }}</a-button></div>
      <h3>其他输入</h3><a-input v-model:value="query" placeholder="搜索工作站、摄像机或 IP" aria-label="抽屉搜索输入" allow-clear />
      <div class="demo-drawer-list"><button v-for="item in secondary" :key="item.ip" type="button" :disabled="!item.available" :class="{ active: ip === item.ip }" @click="choose(item)"><span>{{ item.name }}</span><span class="demo-ip">IP{{ item.ip }}</span></button></div>
    </a-drawer>
  </template>
</template>
