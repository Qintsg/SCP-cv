<!-- 直播URI默认隐藏且可明确查看；只编辑原值，不将脱敏占位符发送给后端。 -->
<script setup lang="ts">
import { ref, watch } from 'vue';
import { InputPassword as AInputPassword } from 'ant-design-vue/es/input';
import AButton from 'ant-design-vue/es/button';
const props = withDefaults(defineProps<{ label: string; placeholder?: string; disabled?: boolean; active?: boolean }>(), { active: true });
const value = defineModel<string>('value', { required: true });
const visible = ref(false);
watch(() => props.active, () => { visible.value = false; }, { flush: 'sync' });
</script>
<template>
  <div class="stream-address-input">
    <a-input-password v-model:value="value" :visible="visible" :visibility-toggle="false" :disabled="disabled"
      :placeholder="placeholder" :aria-label="label" autocomplete="off" autocapitalize="off" spellcheck="false" />
    <a-button size="small" :disabled="disabled" :aria-pressed="visible" @click="visible = !visible">{{ visible ? '隐藏地址' : '显示地址' }}</a-button>
  </div>
  <p class="stream-address-hint">支持 rtsp://用户名:密码@主机:端口/路径。凭据中的 @、#、:、% 等字符请百分号编码。</p>
</template>
<style scoped>
.stream-address-input{display:flex;gap:var(--spacingHorizontalS);align-items:center}
.stream-address-input>:first-child{flex:1;min-width:0}.stream-address-input>:last-child{flex-shrink:0}
.stream-address-hint{margin:var(--spacingVerticalXS) 0 0;color:var(--colorNeutralForeground3);font-size:var(--fontSizeBase200);overflow-wrap:anywhere}
@media(max-width:640px){.stream-address-input :deep(input),.stream-address-input :deep(button){min-height:44px}}
</style>
