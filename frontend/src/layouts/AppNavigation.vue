<!-- 文件描述：桌面分组导航与手机Pad滑动玻璃底栏。项目：SCP-cv；文件：AppNavigation.vue；作者：Qintsg；日期：2026-10-28。 -->
<script setup lang="ts">
import { computed } from 'vue';
import { useI18n } from 'vue-i18n';
import { RouterLink, useRouter } from 'vue-router';
import AMenu, { MenuItem as AMenuItem, ItemGroup as AMenuItemGroup } from 'ant-design-vue/es/menu';
import MoreSheet from './MoreSheet.vue';
import { DESKTOP_SECONDARY_NAV, MOBILE_TAB_BAR } from './navItems';
import type { NavItemDef } from './types';
import FIcon from '@/design-system/FIcon.vue';
import { useTheme } from '@/composables/useTheme';
import LiquidBottomNavigation from './glass/LiquidBottomNavigation.vue';

const props = defineProps<{
  compact: boolean; navVariant: 'bottom' | 'rail' | 'drawer'; primaryItems: NavItemDef[];
  moreOpen: boolean; isActive: (path: string) => boolean;
}>();
const emit = defineEmits<{
  (event: 'update:moreOpen', value: boolean): void;
}>();
const { t } = useI18n();
const router = useRouter();
const { dark } = useTheme();
const all = computed(() => [...props.primaryItems, ...DESKTOP_SECONDARY_NAV]);
const selected = computed(() => all.value.filter(item => props.isActive(item.path)).map(item => item.path));
const activeBottomIndex = computed(() => {
  const index = MOBILE_TAB_BAR.findIndex(item => props.isActive(item.path));
  return props.moreOpen || index < 0 ? 4 : index;
});
/** :param index: 松手或键盘选择的底栏入口。:returns: 实际页面导航完成。 */
async function selectBottom(index: number): Promise<void> {
  const path = MOBILE_TAB_BAR[index]!.path;
  if (path === '/more') emit('update:moreOpen', true);
  else await router.push(path);
}
const groups = computed(() => [
  { key: 'operate', label: '现场操作', children: props.primaryItems.filter(item =>
    ['/dashboard', '/display/big-left', '/display/big-right', '/display/window-3', '/display/window-4', '/devices'].includes(item.path)) },
  { key: 'content', label: '内容资源', children: props.primaryItems.filter(item =>
    ['/sources', '/background-audio', '/scenarios'].includes(item.path)) },
  { key: 'system', label: '系统', children: DESKTOP_SECONDARY_NAV },
]);
/** :param key: 已知菜单键。:returns: 导航到原路由，不另发业务命令。 */
function navigate({ key, domEvent }: { key: string | number; domEvent?: Event }): void {
  // 鼠标链接由RouterLink处理（含修饰键新标签页）；Ant菜单键盘事件保留显式导航。
  if (domEvent?.type === 'click' && domEvent.target instanceof Element && domEvent.target.closest('a[href]')) return;
  if (all.value.some(item => item.path === key)) void router.push(String(key));
}
</script>
<template>
  <nav v-if="!compact" class="console-navigation" :aria-label="t('app.primaryNav')">
    <a-menu mode="inline" :theme="dark ? 'dark' : 'light'" :selected-keys="selected" @click="navigate">
      <a-menu-item-group v-for="group in groups" :key="group.key" :title="group.label">
        <a-menu-item v-for="item in group.children" :key="item.path" :title="item.label">
          <template #icon><FIcon :name="item.icon" :size="19" /></template>
          <RouterLink :to="item.path" :aria-current="isActive(item.path) ? 'page' : undefined">{{ item.label }}</RouterLink>
        </a-menu-item>
      </a-menu-item-group>
    </a-menu>
  </nav>
  <LiquidBottomNavigation v-else :items="MOBILE_TAB_BAR" :active-index="activeBottomIndex" :label="t('app.primaryNav')" :select="selectBottom" />
  <MoreSheet :open="moreOpen" @update:open="value => emit('update:moreOpen', value)" />
</template>
