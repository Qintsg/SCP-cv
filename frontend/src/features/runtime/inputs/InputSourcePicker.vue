<!-- 正式方案A：其他输入二级菜单可搜索滚动；只返回输入身份，不发送设备指令。 -->
<script setup lang="ts">
import { computed, ref, useId, watch } from 'vue';
import AButton from 'ant-design-vue/es/button';
import AInput from 'ant-design-vue/es/input';
import APopover from 'ant-design-vue/es/popover';
import FIcon from '@/design-system/FIcon.vue';
import { useRequestContext } from '@/composables/useRequestContext';
import { useInputCatalogStore } from '@/stores/inputCatalog';
import type { VideoWallMappingItem } from '@/services/apiTypes';
import { inputLabel, validIp } from '../wall/layoutModel';
const props = defineProps<{ value: VideoWallMappingItem['input']; disabled?: boolean; ariaLabel?: string }>();
const emit = defineEmits<{ choose: [input: VideoWallMappingItem['input']] }>();
const catalog = useInputCatalogStore();
const menuId = `input-source-${useId()}`;
const open = ref(false), other = ref(false), custom = ref(false), query = ref(''), group = ref('all'), ip = ref('');
useRequestContext(() => { open.value=false; other.value=false; custom.value=false; query.value=''; group.value='all'; ip.value=''; });
const quick = [{kind:'window_1',label:'窗口 1',suffix:63},{kind:'window_2',label:'窗口 2',suffix:64},{kind:'laptop',label:'IP81',suffix:81},{kind:'window_3',label:'窗口 3',suffix:65},{kind:'window_4',label:'窗口 4',suffix:66}] as const;
const entries = computed(() => catalog.items.filter(item => item.group !== 'site' && (group.value === 'all' || item.group === group.value)
  && `${item.name} IP${item.ip_suffix} ${item.aliases.join(' ')}`.toLowerCase().includes(query.value.trim().toLowerCase())));
const label = computed(() => quick.find(item => item.kind === props.value.kind)?.label || catalog.items.find(item => item.preview_input.kind === props.value.kind && (item.preview_input.ip_address || '') === (props.value.ip_address || ''))?.name || inputLabel(props.value));
const suffix = computed(() => quick.find(item => item.kind === props.value.kind)?.suffix || catalog.items.find(item => item.preview_input.ip_address === props.value.ip_address)?.ip_suffix);
/** :param input: 本地明确选源。:returns: 仅更新父表单，应用由调用方确认。 */
function choose(input: VideoWallMappingItem['input']): void { if (props.disabled) return; emit('choose', { ...input }); open.value = false; }
/** :returns: 进入二级菜单时才进行有界服务探测，不持续拉取所有媒体。 */
function showOther(): void { other.value = true; custom.value = false; void catalog.load(true); }
/** :returns: 规范IPv4才提交；不允许凭据或原始URL。 */
function chooseCustom(): void { if (validIp(ip.value.trim())) choose({kind:'ip_stream',ip_address:ip.value.trim()}); }
/** :param field: 本地字段。:param event: Ant原生change。:returns: 非受控输入避免Ant4.2.6延后回调在认证卸载后读取空ref。 */
function updateText(field: 'ip' | 'query', event: Event): void {
  if (!(event.target instanceof HTMLInputElement)) return;
  if (field === 'ip') ip.value=event.target.value; else query.value=event.target.value;
}
watch(open, value => {
  other.value=false; custom.value=false; query.value=''; group.value='all';
  ip.value=value ? props.value.ip_address || '' : '';
  if(value) void catalog.load();
});
watch(() => props.disabled, value => { if (value) open.value = false; });
</script>
<template>
  <a-popover v-model:open="open" trigger="click" placement="bottomLeft" :overlay-style="{maxWidth:'calc(100vw - 24px)'}" overlay-class-name="input-source-popover">
    <template #content><div :id="menuId" class="input-source-menu" data-input-menu @keydown.esc="open=false">
      <nav class="input-source-roots" aria-label="输入类别"><button v-for="item in quick" :key="item.kind" type="button" :disabled="disabled" :class="{active:value.kind===item.kind}" @click="choose({kind:item.kind,ip_address:''})"><span>{{ item.label }}</span><small>IP{{ item.suffix }}</small></button><button type="button" :disabled="disabled" @click="custom=true;other=false"><span>自定义 IP</span><FIcon name="add_24_regular" :size="16" /></button><button type="button" :disabled="disabled" :class="{active:other}" @click="showOther"><span>其他输入</span><FIcon name="chevron_right_24_regular" :size="16" /></button></nav>
      <section v-if="other" class="input-source-secondary"><div class="input-source-search"><a-input :default-value="query" placeholder="搜索名称或 IP" aria-label="搜索其他输入" allow-clear @change="updateText('query',$event)" /><div class="input-source-groups"><button v-for="item in [{value:'all',label:'全部'},{value:'workstation',label:'工作站'},{value:'camera',label:'摄像机'}]" :key="item.value" type="button" :class="{active:group===item.value}" @click="group=item.value">{{ item.label }}</button><a-button type="text" size="small" :loading="catalog.loading" aria-label="刷新输入目录" @click="catalog.load(true,true)"><template #icon><FIcon name="arrow_clockwise_24_regular" :size="14" /></template></a-button></div></div>
        <div class="input-source-list" role="group" aria-label="其他输入"><button v-for="item in entries" :key="item.id" type="button" :class="{active:item.preview_input.kind===value.kind&&(item.preview_input.ip_address||'')===(value.ip_address||'')}" @click="choose(item.preview_input)"><span><FIcon :name="item.group==='camera'?'camera_24_regular':'desktop_24_regular'" :size="17" />{{ item.name }}</span><small>IP{{ item.ip_suffix }}</small></button><span v-if="!entries.length" class="input-source-empty">{{ catalog.loading ? '正在读取' : catalog.error || '没有匹配的输入' }}</span></div>
      </section>
      <section v-if="custom" class="input-source-custom"><a-input :default-value="ip" placeholder="192.168.5.…" aria-label="自定义输入IPv4" @change="updateText('ip',$event)" @pressEnter="chooseCustom" /><a-button type="primary" :disabled="!validIp(ip.trim())" @click="chooseCustom">确定</a-button></section>
    </div></template>
    <a-button block class="input-source-trigger" :disabled="disabled" :aria-controls="menuId" :aria-expanded="open" :aria-label="ariaLabel || '选择输入来源'" @keydown.esc="open=false"><span>{{ label }}<small v-if="suffix && value.kind==='ip_stream'">IP{{ suffix }}</small></span><FIcon name="chevron_down_20_regular" :size="16" /></a-button>
  </a-popover>
</template>
<style scoped>
.input-source-trigger{display:flex;align-items:center;justify-content:space-between;text-align:left;gap:10px;min-width:0}.input-source-trigger>span{overflow:hidden;text-overflow:ellipsis;white-space:nowrap}.input-source-trigger small{font:11px Consolas,monospace;color:var(--console-muted);margin-left:8px}.input-source-menu{display:flex;max-width:calc(100vw - 40px);min-width:180px;min-height:306px}.input-source-roots{display:flex;flex-direction:column;width:170px;padding:3px;gap:2px}.input-source-roots>button{display:flex;align-items:center;justify-content:space-between;gap:18px;text-align:left;min-height:40px;padding:9px 10px;border:0;border-radius:4px;background:transparent;color:var(--console-text);font:inherit;font-size:12px;cursor:pointer}.input-source-roots>button:last-child{margin-top:auto}.input-source-roots small,.input-source-list small{font:11px Consolas,monospace;color:var(--console-muted)}.input-source-roots .active,.input-source-list .active,.input-source-groups .active{background:var(--console-brand-tint);color:var(--console-primary)}.input-source-secondary{width:296px;min-width:0;border-left:1px solid var(--console-border)}.input-source-search{padding:8px 10px;border-bottom:1px solid var(--console-border)}.input-source-groups{display:flex;align-items:center;gap:3px;margin-top:6px}.input-source-groups>button:not(.ant-btn){border:0;padding:4px 8px;border-radius:4px;background:transparent;color:var(--console-muted);font-size:10px;cursor:pointer}.input-source-groups>.ant-btn{margin-left:auto}.input-source-list{max-height:252px;overflow:auto;scrollbar-width:thin}.input-source-list>button{display:flex;align-items:center;justify-content:space-between;width:100%;padding:12px 13px;min-height:44px;border:0;border-bottom:1px solid var(--console-border);background:transparent;color:var(--console-text);cursor:pointer;font-size:12px;text-align:left}.input-source-list>button>span{display:flex;align-items:center;gap:9px}.input-source-list>button:hover,.input-source-roots>button:hover{background:var(--console-subtle)}.input-source-empty{display:block;padding:20px 12px;color:var(--console-muted);font-size:12px}.input-source-custom{display:grid;align-content:start;gap:12px;padding:12px;width:240px;border-left:1px solid var(--console-border)}button:focus-visible{outline:2px solid var(--console-primary);outline-offset:-2px}.input-source-menu button:disabled{opacity:.45;cursor:not-allowed}
@media(max-width:600px){.input-source-roots{width:122px}.input-source-roots>button{font-size:11px;padding:10px 7px;gap:4px;min-height:44px}.input-source-roots small{display:none}.input-source-secondary{width:calc(100vw - 174px)}.input-source-groups>button:not(.ant-btn){padding:4px 5px;font-size:9px}.input-source-list>button{padding:11px 8px}.input-source-list>button>span{gap:5px}.input-source-custom{width:calc(100vw - 174px)}}
</style>
