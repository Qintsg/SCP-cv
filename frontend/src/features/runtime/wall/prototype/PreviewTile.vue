<!-- 本机共享动态样片；只监听源IP，布局尺寸不是连接依赖。 -->
<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref, watch } from 'vue';
import { useRoute } from 'vue-router';
import { acquire } from './demoStreams';
import { inputFor } from './catalog';
const props = defineProps<{ ip: number; label?: string }>();
const video = ref<HTMLVideoElement>(), first = ref<number | null>(null), serial = ref(0), reused = ref(false), error = ref('');
const route = useRoute();
const state = computed(() => error.value || route.query.preview === 'failed' ? 'failed' : route.query.preview === 'retry' || first.value === null ? 'retry' : 'live');
let release: (() => void) | undefined, generation = 0, mounted = false;
/** :returns: 仅真正换源时重新挂载该消费者；先取得新引用再放旧引用。 */
function attach(): void {
  if (!mounted || !video.value) return;
  const ticket = ++generation, start = performance.now(); first.value = null; error.value = '';
  if (!inputFor(props.ip).available) { release?.(); release = undefined; video.value.srcObject = null; error.value = '无法连接'; return; }
  try {
    const next = acquire(props.ip); const old = release; release = next.release; serial.value = next.serial; reused.value = next.reused;
    video.value.srcObject = next.stream; old?.();
    video.value.onloadeddata = () => { if (generation === ticket) first.value = Math.round(performance.now() - start); };
    void video.value.play().catch(() => { if (generation === ticket) error.value = '浏览器暂停了演示视频'; });
  } catch { error.value = '当前浏览器不支持本机演示流'; }
}
watch(() => props.ip, attach);
onMounted(() => { mounted = true; attach(); });
onBeforeUnmount(() => { generation++; if (video.value) { video.value.pause(); video.value.srcObject = null; } release?.(); });
</script>
<template>
  <div class="demo-preview" :class="`demo-preview--${state}`" :data-input="ip" :data-source-serial="serial" :data-first-frame-ms="first" :title="state === 'live' ? undefined : state === 'retry' ? '正在重连' : error || '无法连接'">
    <video ref="video" muted autoplay playsinline :aria-label="`${label || `IP${ip}`}本机演示画面`" />
  </div>
</template>
