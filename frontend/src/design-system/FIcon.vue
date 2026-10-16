<script setup lang="ts">
/**
 * Fluent UI System Icons 图标组件（DESIGN.md §9 与生态包的关系 / §7 无障碍）。
 * 通过原生span和既有矢量图标渲染，不依赖组件库包装：
 *   - SVG 矢量图，颜色继承 currentColor，与 Fluent 2 色彩 token 自动联动；
 *   - 装饰性图标对辅助技术隐藏（aria-hidden），非装饰必须传 ariaLabel；
 *   - 尺寸通过 :size 显式或随父元素 font-size 缩放。
 */
import { computed } from 'vue';

import { resolveIcon, type FluentIconName } from './icons';

interface FIconProps {
  /** 图标名 token，详见 icons.ts 中的 ICON_MAP。 */
  name: FluentIconName | string;
  /** 显式像素尺寸；不传时由 n-icon 默认 1em 随父元素 font-size。 */
  size?: number | string;
  /** 显式颜色；不传时继承 currentColor。 */
  color?: string;
  /** 装饰性图标，跳过屏幕阅读器；非装饰必须传 ariaLabel。 */
  decorative?: boolean;
  /** 屏幕阅读器可读名称；与 decorative 互斥。 */
  ariaLabel?: string;
}

const props = withDefaults(defineProps<FIconProps>(), {
  size: undefined,
  color: undefined,
  decorative: true,
  ariaLabel: undefined,
});

const iconComponent = computed(() => resolveIcon(props.name));
const computedSize = computed(() => {
  if (props.size === undefined) return undefined;
  return typeof props.size === 'number' ? props.size : props.size;
});
</script>

<template>
  <span
    class="f-icon"
    :style="{ fontSize: typeof computedSize === 'number' ? `${computedSize}px` : computedSize, color }"
    :aria-hidden="decorative ? 'true' : undefined"
    :aria-label="decorative ? undefined : ariaLabel"
    :role="decorative ? undefined : 'img'"
  ><component :is="iconComponent" /></span>
</template>

<style scoped>
.f-icon {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  width: 1em;
  height: 1em;
  flex-shrink: 0;
  color: inherit;
  line-height: 1;
  user-select: none;
}
.f-icon :deep(svg) { width: 1em; height: 1em; fill: currentColor; }
</style>
