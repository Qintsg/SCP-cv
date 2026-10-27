<!-- D4后两路输出仅前端预留；不挂载会话控制器或创建任何原生播放器。 -->
<script setup lang="ts">
import { useRoute, RouterLink } from 'vue-router';
import { computed } from 'vue';
import ACard from 'ant-design-vue/es/card';
import ATag from 'ant-design-vue/es/tag';
import FPageHeader from '@/design-system/FPageHeader.vue';
import ClientStreamPreview from '@/features/preview/ClientStreamPreview.vue';
import { useMobileControls } from '@/composables/useMobileControls';
const mobileControls = useMobileControls();
const route = useRoute();
const number = computed(() => route.path.endsWith('4') ? 4 : 3);
</script>
<template><section class="reserved-window"><FPageHeader :title="`窗口 ${number}`"><template #actions><a-tag>IP{{ number + 62 }}</a-tag></template></FPageHeader><a-card><div v-if="!mobileControls" class="reserved-window__preview"><ClientStreamPreview :input="{kind:number===3?'window_3':'window_4'}" quality="high" :label="`窗口${number}预览`" /></div><div class="reserved-window__content"><span>播放控制预留</span><RouterLink to="/dashboard">返回仪表盘</RouterLink></div></a-card></section></template>
<style scoped>.reserved-window{display:grid;gap:24px}.reserved-window__preview{aspect-ratio:16/9;max-height:calc(100vh - 260px)}.reserved-window__content{display:flex;align-items:center;justify-content:space-between;gap:12px;margin-top:16px;color:var(--console-muted);font-size:12px}</style>
