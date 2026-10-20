<!-- Ant分组导航：按现场操作/内容资源/系统组织，小屏保留五个稳定底部入口。 -->
<script setup lang="ts">
import { computed } from 'vue';
import { useI18n } from 'vue-i18n';
import { RouterLink, useRouter } from 'vue-router';
import AMenu, { MenuItem as AMenuItem, ItemGroup as AMenuItemGroup } from 'ant-design-vue/es/menu';
import MoreSheet from './MoreSheet.vue';
import { DESKTOP_SECONDARY_NAV, MOBILE_TAB_BAR } from './navItems';
import type { NavItemDef } from './types';
import FIcon from '@/design-system/FIcon.vue';

const props = defineProps<{
  compact: boolean; navVariant: 'bottom' | 'rail' | 'drawer'; primaryItems: NavItemDef[];
  moreOpen: boolean; isActive: (path: string) => boolean;
}>();
const emit = defineEmits<{
  (event: 'update:moreOpen', value: boolean): void;
  (event: 'bottomClick', path: string, value: MouseEvent): void;
}>();
const { t } = useI18n();
const router = useRouter();
const all = computed(() => [...props.primaryItems, ...DESKTOP_SECONDARY_NAV]);
const selected = computed(() => all.value.filter(item => props.isActive(item.path)).map(item => item.path));
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
    <a-menu mode="inline" theme="dark" :selected-keys="selected" @click="navigate">
      <a-menu-item-group v-for="group in groups" :key="group.key" :title="group.label">
        <a-menu-item v-for="item in group.children" :key="item.path" :title="item.label">
          <template #icon><FIcon :name="item.icon" :size="19" /></template>
          <RouterLink :to="item.path" :aria-current="isActive(item.path) ? 'page' : undefined">{{ item.label }}</RouterLink>
        </a-menu-item>
      </a-menu-item-group>
    </a-menu>
  </nav>
  <nav v-else class="app-shell__bottom" :aria-label="t('app.primaryNav')">
    <a v-for="item in MOBILE_TAB_BAR" :key="item.path" :href="item.path" class="app-shell__bottom-item"
      :class="{ 'app-shell__bottom-item--active': isActive(item.path) }" :aria-current="isActive(item.path) ? 'page' : undefined"
      @click.prevent="event => emit('bottomClick', item.path, event)">
      <FIcon :name="(isActive(item.path) && item.iconSelected) || item.icon" :size="22" /><span>{{ item.label }}</span>
    </a>
  </nav>
  <MoreSheet :open="moreOpen" @update:open="value => emit('update:moreOpen', value)" />
</template>
