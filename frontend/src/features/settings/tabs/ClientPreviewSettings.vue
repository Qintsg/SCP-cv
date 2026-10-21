<!-- 预览路径与只读诊断：Electron直拉和普通Web服务器预算分开，不把能力标签说成已出画。 -->
<script setup lang="ts">
import { onMounted, ref } from 'vue';
import ACard from 'ant-design-vue/es/card';
import AButton from 'ant-design-vue/es/button';
import { requestJson } from '@/services/api';
import { useRequestContext } from '@/composables/useRequestContext';
import { getNativePlatformAdapter } from '@/platform/native';
import { getPreviewPool } from '@/features/preview/previewPool';
interface PreviewStatus { lease_count: number; upstream_count: number; max_upstreams: number; egress_kbps: number }
const electron=Boolean(window.scpCvElectron || getNativePlatformAdapter()?.platform==='electron');
const nativeAvailable=Boolean(window.scpCvElectron?.preview || getNativePlatformAdapter()?.preview);
const status=ref<PreviewStatus>(), local=ref<{streams:number;decoders:number}>(), busy=ref(false), detail=ref('');
let request=0;
const capture=useRequestContext(()=>{request++;status.value=undefined;local.value=undefined;detail.value='';busy.value=false;});
/** :returns: 仅读取当前路径诊断；客户端不会为计数而启动流或向D4补读预算。 */
async function refresh():Promise<void>{
  if(busy.value)return;
  const scope=capture(),ticket=++request,current=()=>scope()&&ticket===request;busy.value=true;detail.value='';
  try{
    if(electron){if(!nativeAvailable)throw Error('直连组件不可用');local.value=getPreviewPool().diagnostics;}
    else{const result=await requestJson<PreviewStatus>('/api/stream-previews/status/',{},8000,{canConsume:current});if(current())status.value=result;}
  }catch{if(current())detail.value=electron?'本机直连组件不可用':'暂时无法读取预览状态';}
  finally{if(current())busy.value=false;}
}
onMounted(refresh);
</script>
<template><a-card title="直播预览"><div class="client-preview-settings">
  <p>{{electron?'连接方式：本机直拉 RTSP / RTMP':'连接方式：播放主机共享 FLV'}} · 无声预览</p>
  <div v-if="local" class="client-preview-settings__status"><span>{{local.streams}} 路本机流</span><span>{{local.decoders}} 个共享解码器</span></div>
  <div v-if="status" class="client-preview-settings__status"><span>{{status.upstream_count}} / {{status.max_upstreams}} 路服务端预览</span><span>{{status.lease_count}} 个租约</span><span>服务端出站预算 {{status.egress_kbps}} kbps</span></div>
  <p v-if="detail">{{detail}}</p><a-button :loading="busy" @click="refresh">刷新预览状态</a-button>
</div></a-card></template>
<style scoped>.client-preview-settings{display:grid;gap:12px}.client-preview-settings p{font-size:12px;color:var(--console-muted);margin:0;line-height:1.7}.client-preview-settings__status{display:flex;gap:8px 16px;flex-wrap:wrap;font-size:12px}.client-preview-settings>.ant-btn{justify-self:start}</style>
