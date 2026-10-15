<!-- 公共页面标题：紧凑说明、上下文和明确操作，不携带业务请求。 -->
<script setup lang="ts">
defineProps<{ title: string; description?: string; meta?: string }>();
</script>
<template>
  <header class="page-header">
    <div class="page-header__copy">
      <p v-if="meta" class="page-header__meta">{{ meta }}</p>
      <h1 class="page-header__title">{{ title }}</h1>
      <p v-if="description" class="page-header__description">{{ description }}</p>
      <div v-if="$slots.context" class="page-header__context"><slot name="context" /></div>
    </div>
    <div v-if="$slots.actions" class="page-header__actions"><slot name="actions" /></div>
  </header>
</template>
<style scoped>
.page-header{display:flex;justify-content:space-between;align-items:center;gap:var(--spacingHorizontalXL);padding-block:var(--spacingVerticalS) var(--spacingVerticalL);border-bottom:1px solid var(--colorNeutralStroke2)}
.page-header__copy{min-width:0;flex:1}.page-header__title{margin:0;font-size:var(--fontSizeBase600);line-height:var(--lineHeightBase600);font-weight:var(--fontWeightSemibold);overflow-wrap:anywhere}
.page-header__description{margin:var(--spacingVerticalS) 0 0;color:var(--colorNeutralForeground2);max-width:72ch;overflow-wrap:anywhere}
.page-header__meta{margin:0 0 var(--spacingVerticalXS);font-size:var(--fontSizeBase200);color:var(--colorBrandForeground1)}
.page-header__context,.page-header__actions{display:flex;align-items:center;flex-wrap:wrap;gap:var(--spacingHorizontalS)}.page-header__context{margin-top:var(--spacingVerticalM)}.page-header__actions{max-width:100%;min-width:0}
@media(max-width:767px){.page-header{align-items:stretch;flex-direction:column;gap:var(--spacingVerticalM)}.page-header__actions{width:100%}.page-header__actions :deep(.n-input){flex:1;min-width:0}.page-header__title{font-size:var(--fontSizeBase500)}}
</style>
