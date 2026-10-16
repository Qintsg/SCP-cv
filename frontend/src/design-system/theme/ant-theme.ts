/** Ant Design现场控制台主题；组件和任务工作区共享同一色阶与字号。 */
import theme from 'ant-design-vue/es/theme';
import type { ThemeConfig } from 'ant-design-vue/es/config-provider/context';

/** :param dark: 当前解析后的明暗主题。:returns: Ant原生算法与控制台令牌。 */
export function consoleTheme(dark: boolean): ThemeConfig {
  return {
    algorithm: dark ? theme.darkAlgorithm : theme.defaultAlgorithm,
    token: {
      colorPrimary: '#1677ff', colorInfo: '#1677ff',
      colorBgLayout: dark ? '#101722' : '#f3f6fb',
      colorBgContainer: dark ? '#182231' : '#ffffff',
      colorText: dark ? '#e6edf7' : '#182338',
      colorTextSecondary: dark ? '#a3b1c6' : '#52627a',
      colorBorderSecondary: dark ? '#2c3a4f' : '#e2e8f0',
      fontFamily: '"Segoe UI", "Microsoft YaHei", "Noto Sans SC", sans-serif',
      fontSize: 14, borderRadius: 6, controlHeight: 36,
      wireframe: false, zIndexPopupBase: 2000,
    },
    components: { Card: { paddingLG: 20 }, Modal: { paddingContentHorizontalLG: 24 } },
  };
}
