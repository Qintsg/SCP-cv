/*
 * 应用 Shell 导航条目集合：与设计稿 §3.2 严格对齐。
 *  - 仪表盘 / 窗口1 / 窗口2 / 媒体源 / 背景音乐 / 预案 / 设置
 *  - 逻辑窗口1/2始终可达，不按墙面布局隐藏；路由名称保留兼容，不代表物理左右
 *  - 移动端底部 TabBar 5 项：首页 / 显控 / 媒体源 / 预案 / 更多
 */
import { t } from '@/locales';

import type { NavItemDef } from './types';

/** 桌面NavPane全量条目，逻辑窗口不按墙面模式过滤。 */
export const DESKTOP_PRIMARY_NAV: NavItemDef[] = [
  { path: '/dashboard', label: t('nav.dashboard'), icon: 'home_24_regular', iconSelected: 'home_24_filled' },
  { path: '/display/big-left', label: t('videoWall.window1'), icon: 'tv_24_regular', iconSelected: 'tv_24_filled' },
  { path: '/display/big-right', label: t('videoWall.window2'), icon: 'tv_24_regular', iconSelected: 'tv_24_filled' },
  { path: '/sources', label: t('nav.sources'), icon: 'library_24_regular', iconSelected: 'library_24_filled' },
  { path: '/background-audio', label: t('nav.backgroundAudio'), icon: 'music_note_2_24_regular' },
  { path: '/devices', label: t('nav.devices'), icon: 'camera_24_regular' },
  { path: '/scenarios', label: t('nav.scenarios'), icon: 'layer_24_regular', iconSelected: 'layer_24_filled' },
];

/** 桌面次级（设置）入口；视觉上独立分组。 */
export const DESKTOP_SECONDARY_NAV: NavItemDef[] = [
  { path: '/settings', label: t('nav.settings'), icon: 'settings_24_regular', iconSelected: 'settings_24_filled' },
];

/** 移动端底部 TabBar 5 项；「显控」默认指向 big-left，由页面内 SegmentedControl 切窗口。 */
export const MOBILE_TAB_BAR: NavItemDef[] = [
  { path: '/dashboard', label: t('nav.home'), icon: 'home_24_regular', iconSelected: 'home_24_filled' },
  { path: '/display/big-left', label: t('nav.display'), icon: 'tv_24_regular', iconSelected: 'tv_24_filled' },
  { path: '/sources', label: t('nav.sources'), icon: 'library_24_regular', iconSelected: 'library_24_filled' },
  { path: '/scenarios', label: t('nav.scenarios'), icon: 'layer_24_regular', iconSelected: 'layer_24_filled' },
  { path: '/more', label: t('nav.more'), icon: 'more_horizontal_24_regular' },
];

/**
 * :param path: 既有兼容路由。
 * :param _isDoubleScreen: 旧调用兼容参数，不决定逻辑窗口是否存在。
 * :param _isCustom: 旧调用兼容参数，不冒称物理左右映射。
 * :returns: 当前逻辑窗口标题。
 */
export function resolveDisplayLabel(path: string, _isDoubleScreen: boolean, _isCustom = false): string {
  if (path === '/display/big-left') return t('videoWall.window1');
  if (path === '/display/big-right') return t('videoWall.window2');
  return '';
}
