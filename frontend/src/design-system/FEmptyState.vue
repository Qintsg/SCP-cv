<!-- 无内容与无匹配的公共反馈；恢复动作由页面明确提供，不自动修改数据。 -->
<script setup lang="ts">
defineProps<{ title: string; description?: string }>();
</script>
<template>
  <section class="empty-state" aria-live="polite">
    <h3>{{ title }}</h3><p v-if="description">{{ description }}</p>
    <div v-if="$slots.default" class="empty-state__actions"><slot /></div>
  </section>
</template>
<style scoped>
.empty-state{padding:var(--spacingVerticalXXXL) var(--spacingHorizontalL);text-align:center;grid-column:1/-1;width:100%}.empty-state h3{margin:0;font-size:var(--fontSizeBase400);font-weight:var(--fontWeightSemibold)}.empty-state p{color:var(--colorNeutralForeground2);margin:var(--spacingVerticalS) auto 0;max-width:54ch;overflow-wrap:anywhere}.empty-state__actions{display:flex;justify-content:center;flex-wrap:wrap;gap:var(--spacingHorizontalS);margin-top:var(--spacingVerticalL)}
</style>
