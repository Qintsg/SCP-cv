<!-- 本机预览显式连接：临时令牌只在当前标签页保存，不发送给播放主机。 -->
<script setup lang="ts">
import { ref } from 'vue';
import ACard from 'ant-design-vue/es/card';
import AInput from 'ant-design-vue/es/input';
import AButton from 'ant-design-vue/es/button';
import { configurePreview, previewToken } from '@/features/preview/previewAddress';
import { getClientBuildTarget } from '@/platform';
const token = ref(previewToken()), connected = ref(!!token.value);
const supported = getClientBuildTarget() === 'web' && ['http:', 'https:'].includes(window.location.protocol);
const command = `node tools/client-preview/server.mjs --origin=${window.location.origin}`;
/** :returns: 仅写当前标签页临时配置，是否拉流成功由卡片自己报告。 */
function connect(): void { configurePreview(token.value); connected.value = true; token.value = ''; }
/** :returns: 撤销本标签页所有预览连接，不操作D4或系统服务。 */
function disconnect(): void { configurePreview(''); token.value = ''; connected.value = false; }
</script>
<template><a-card title="本机直播预览"><div class="client-preview-settings">
  <p>预览由正在使用网页的这台电脑拉取，不经过 D4。需要在本机安装 FFmpeg 并启动预览工具。</p>
  <p v-if="!supported">当前封装客户端尚未接入本机预览，请通过浏览器使用。</p>
  <code>{{ command }}</code><p>将工具给出的临时连接码填入；仅当前标签页有效，预览不播放声音。</p>
  <a-input v-model:value="token" type="password" autocomplete="off" aria-label="本机预览临时连接码" placeholder="临时连接码" />
  <div><a-button type="primary" :disabled="!supported || !/^[a-f0-9]{48}$/.test(token)" @click="connect">连接本机预览</a-button><a-button :disabled="!connected" @click="disconnect">断开</a-button></div>
</div></a-card></template>
<style scoped>.client-preview-settings{display:grid;gap:12px}.client-preview-settings p{font-size:12px;color:var(--console-muted);margin:0;line-height:1.7}.client-preview-settings code{font-size:12px;background:var(--console-subtle);padding:10px;overflow-wrap:anywhere}.client-preview-settings>div{display:flex;gap:8px;flex-wrap:wrap}</style>
