<!-- 文件描述：按真实按钮坐标滑动的折射镜片与单次导航底栏。项目：SCP-cv；文件：LiquidBottomNavigation.vue；作者：Qintsg；日期：2026-10-30。 -->
<script setup lang="ts">
import { onBeforeUnmount, onMounted, ref, watch } from 'vue';
import 'simple-liquid-glass/web-component';
import FIcon from '@/design-system/FIcon.vue';
import type { NavItemDef } from '../types';
import { createNavigationMotion, type NavigationFrame } from './navigationMotion';
import { navigationPosition } from './navigationGeometry';
import './liquidNavigation.css';

const props = defineProps<{
  items: NavItemDef[]; activeIndex: number; label: string; select: (index: number) => Promise<void>;
}>();
const root = ref<HTMLElement>();
let motion: ReturnType<typeof createNavigationMotion> | undefined;
let observer: ResizeObserver | undefined;
let pointer: number | undefined;
let width = 0;
const reducedMotion = matchMedia('(prefers-reduced-motion: reduce)');
const reducedTransparency = matchMedia('(prefers-reduced-transparency: reduce), (forced-colors: active)');
const effectMode = ref(reducedTransparency.matches ? 'off' : 'svg');

/** :param frame: 连续镜片状态。:returns: 局部CSS绘制，不触发Vue业务渲染。 */
function paint(frame: NavigationFrame): void {
  const element = root.value;
  if (!element || !width) return;
  const cell = width / props.items.length;
  const scaleX = 1 + frame.pressed * .08 + frame.stretch * .16;
  const scaleY = 1 + frame.pressed * .14 - frame.stretch * .07;
  element.style.setProperty('--lens-x', `${frame.position * cell}px`);
  element.style.setProperty('--lens-scale-x', String(scaleX));
  element.style.setProperty('--lens-scale-y', String(scaleY));
  element.style.setProperty('--lens-press', String(frame.pressed));
  const progress = frame.position / (props.items.length - 1);
  element.style.setProperty('--glass-light-x', `${8 + progress * 84}%`);
  element.style.setProperty('--glass-parallax', `${(progress - .5) * frame.pressed * 4}px`);
  const cutLeft = (frame.position * cell + cell * (1 - scaleX) / 2) / width * 100;
  const cutRight = 100 - cutLeft - scaleX / props.items.length * 100;
  element.style.setProperty('--lens-clip', `inset(-12px ${cutRight}% -12px ${cutLeft}% round 30px)`);
  element.style.setProperty('--lens-left', `${cutLeft}%`);
  element.style.setProperty('--lens-right', `${100 - cutRight}%`);
}
/** :returns: 测量未变形布局并取消尺寸改变前的手势。 */
function measure(): void {
  const element = root.value!;
  const style = getComputedStyle(element);
  const inset = parseFloat(style.paddingLeft);
  width = element.clientWidth - inset - parseFloat(style.paddingRight);
  element.style.setProperty('--glass-inset', `${inset}px`);
  element.style.setProperty('--nav-count', String(props.items.length));
  cancel(); motion?.paint();
}
/** :param clientX: 指针水平位置。:returns: 当前入口连续位置。 */
function position(clientX: number): number {
  const slots = [...root.value!.querySelectorAll<HTMLButtonElement>('.liquid-nav__button')].map(button => button.getBoundingClientRect());
  return navigationPosition(clientX, slots);
}
/** :param event: 原生主指针事件。 */
function down(event: PointerEvent): void {
  if (!event.isPrimary || event.button !== 0 || pointer !== undefined) return;
  measure();
  if (!motion?.begin(position(event.clientX), event.timeStamp)) return;
  pointer = event.pointerId;
  root.value!.setPointerCapture(pointer);
}
/** :param event: 当前捕获的指针。 */
function move(event: PointerEvent): void {
  if (event.pointerId === pointer) motion?.move(position(event.clientX), event.timeStamp);
}
/** :param event: 松手事件。:returns: 完成单次导航。 */
async function up(event: PointerEvent): Promise<void> {
  if (event.pointerId !== pointer) return;
  motion?.move(position(event.clientX), event.timeStamp);
  pointer = undefined;
  root.value!.releasePointerCapture(event.pointerId);
  await motion?.release();
}
/** :returns: 取消手势；解除捕获后不会触发选择。 */
function cancel(): void {
  const captured = pointer; pointer = undefined;
  if (captured !== undefined && root.value?.hasPointerCapture(captured)) root.value.releasePointerCapture(captured);
  motion?.cancel();
}
/** :param event: 指针取消或捕获丢失。 */
function pointerCancelled(event: PointerEvent): void {
  if (event.pointerId === pointer) cancel();
}
/** :param index: 键盘激活的入口。:param event: 浏览器真实click。 */
async function click(index: number, event: MouseEvent): Promise<void> {
  // 指针操作在pointerup完成；键盘与辅助工具的click没有指针点击计数。
  if (event.detail === 0) await motion?.activate(index);
}
/** :returns: 系统减弱动态偏好改变后立即同步状态。 */
function preferenceChanged(): void {
  effectMode.value = reducedTransparency.matches ? 'off' : 'svg';
  cancel(); motion?.sync();
}
watch(() => props.activeIndex, () => motion?.sync());
onMounted(() => {
  motion = createNavigationMotion({ count: props.items.length, activeIndex: () => props.activeIndex,
    reducedMotion: () => reducedMotion.matches, paint, select: index => props.select(index) });
  measure(); observer = new ResizeObserver(measure); observer.observe(root.value!);
  window.addEventListener('resize', measure);
  window.addEventListener('orientationchange', measure);
  window.visualViewport?.addEventListener('resize', measure);
  window.visualViewport?.addEventListener('scroll', measure);
  reducedMotion.addEventListener('change', preferenceChanged);
  reducedTransparency.addEventListener('change', preferenceChanged);
});
onBeforeUnmount(() => {
  observer?.disconnect(); reducedMotion.removeEventListener('change', preferenceChanged);
  reducedTransparency.removeEventListener('change', preferenceChanged);
  window.removeEventListener('resize', measure);
  window.removeEventListener('orientationchange', measure);
  window.visualViewport?.removeEventListener('resize', measure);
  window.visualViewport?.removeEventListener('scroll', measure);
  cancel(); motion?.destroy();
});
</script>

<template>
  <nav ref="root" class="app-shell__bottom liquid-nav" :aria-label="label"
    @pointerdown="down" @pointermove="move" @pointerup="up" @pointercancel="pointerCancelled" @lostpointercapture="pointerCancelled">
    <div class="liquid-nav__track" aria-hidden="true"></div>
    <div class="liquid-nav__icons liquid-nav__icons--base" aria-hidden="true">
      <div v-for="item in items" :key="item.path" class="liquid-nav__visual">
        <FIcon :name="item.icon" :size="22" /><span>{{ item.label }}</span>
      </div>
    </div>
    <liquid-glass class="liquid-nav__lens" renderer="svg" :effect-mode="effectMode" :reveal.attr="'immediate'" radius="30" lens="rim"
      lens-strength="0.7" scale="26" displace="1.2" blur="0.3" frost="0.015" saturation="155"
      border-color="rgba(255,255,255,.6)" aria-hidden="true" />
    <div class="liquid-nav__icons liquid-nav__icons--color" aria-hidden="true">
      <div v-for="item in items" :key="item.path" class="liquid-nav__visual">
        <FIcon :name="item.iconSelected || item.icon" :size="22" /><span>{{ item.label }}</span>
      </div>
    </div>
    <button v-for="(item,index) in items" :key="item.path" class="app-shell__bottom-item liquid-nav__button"
      :class="{ 'app-shell__bottom-item--active': activeIndex === index }" :aria-label="item.label"
      :aria-current="activeIndex === index ? 'page' : undefined" :aria-haspopup="item.path === '/more' ? 'dialog' : undefined"
      type="button" @click="event => click(index,event)">
      <span class="liquid-nav__accessible">{{ item.label }}</span>
    </button>
  </nav>
</template>
