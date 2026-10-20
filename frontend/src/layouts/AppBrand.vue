<!-- 品牌首页入口：整块可导航，透明外底Logo与主题呈现不触发业务操作。 -->
<script setup lang="ts">
import { RouterLink } from 'vue-router';
import brandLogo from '@/assets/sspu-ailab-transparent.png';
defineProps<{ collapsed: boolean }>();
</script>
<template>
  <RouterLink to="/dashboard" aria-label="返回仪表盘" class="console-brand" :class="{ 'console-brand--collapsed': collapsed }">
    <img :src="brandLogo" alt="SSPU AILAB" class="console-brand__logo" />
    <div v-if="!collapsed"><strong>SCP-cv</strong><small>28#108 · 现场控制台</small></div>
  </RouterLink>
</template>
