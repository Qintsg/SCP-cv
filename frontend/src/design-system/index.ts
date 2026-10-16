/*
 * 设计系统出口：仅暴露项目级薄包装与主题适配层。
 * 页面直接使用Ant Design Vue；本模块只暴露业务级反馈和图标。
 */
export { default as FIcon } from './FIcon.vue';
export { default as FDialogHost } from './FDialogHost.vue';
export { default as FToastHost } from './FToastHost.vue';

export type { FluentIconName } from './icons';
export { ICON_MAP, resolveIcon, loadedIconCount } from './icons';

export { consoleTheme } from './theme/ant-theme';
