<!-- D4后两路输出仅前端预留；不挂载会话控制器或创建任何原生播放器。 -->
<script setup lang="ts">
import { useRoute, RouterLink } from 'vue-router';
import { computed } from 'vue';
import ACard from 'ant-design-vue/es/card';
import ATag from 'ant-design-vue/es/tag';
import FPageHeader from '@/design-system/FPageHeader.vue';
import FIcon from '@/design-system/FIcon.vue';
const route = useRoute();
const number = computed(() => route.path.endsWith('4') ? 4 : 3);
</script>
<template><section class="reserved-window"><FPageHeader :title="`窗口 ${number}`" /><a-card><div class="reserved-window__content"><FIcon name="desktop_24_regular" :size="36" /><a-tag>预留输出</a-tag><h2>暂未接入播放控制</h2><p>对应 D4 的第 {{ number }} 路显示输出；当前不创建播放器，也不会发送播放命令。</p><RouterLink to="/dashboard">返回仪表盘</RouterLink></div></a-card></section></template>
<style scoped>.reserved-window{display:grid;gap:24px}.reserved-window__content{display:flex;flex-direction:column;align-items:center;gap:12px;padding:48px 16px;text-align:center;color:var(--console-muted)}.reserved-window__content h2,.reserved-window__content p{margin:0}.reserved-window__content h2{font-size:18px;color:var(--console-text)}</style>
