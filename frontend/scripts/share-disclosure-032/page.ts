/**
 * 文件描述：挂载实际共享折叠组件并读取原生展示状态。
 * 项目：SCP-cv；文件：page.ts；作者：Qintsg；日期：2026-11-05。
 */
import { createApp, h, nextTick, onMounted } from 'vue';
import { createPinia, disposePinia } from 'pinia';
import AConfigProvider from 'ant-design-vue/es/config-provider';
import ScreenShareDisclosure from '../../src/features/screen-share/ScreenShareDisclosure.vue';
import { useScreenShareStore } from '../../src/features/screen-share/screenShareStore';
import { i18n } from '../../src/locales';
import { useTheme } from '../../src/composables/useTheme';
import { consoleTheme } from '../../src/design-system/theme/ant-theme';
import 'ant-design-vue/dist/reset.css';
import '../../src/styles/base.css';
import '../../src/styles/experience.css';
import '../../src/styles/console.css';

const pinia = createPinia();
const requestedTheme = new URL(location.href).searchParams.get('theme') === 'dark' ? 'dark' : 'light';
const root = {
  setup() {
    const theme = useTheme(); theme.mode.value = requestedTheme;
    const shares = useScreenShareStore();
    const owner = shares.forWindow(1);
    let panel: Element | null = null;
    onMounted(() => {
      panel = document.querySelector('.screen-share');
      Object.assign(window, { shareDisclosure032: { snapshot, setOpen, focusSummary } });
    });
    /** :returns: 实际组件及Store身份、原生箭头与焦点样式。 */
    function snapshot() {
      const details = document.querySelector<HTMLDetailsElement>('.share-disclosure')!;
      const summary = details.querySelector<HTMLElement>('summary')!;
      const arrow = details.querySelector<HTMLElement>('.share-disclosure__chevron')!;
      const bounds = summary.getBoundingClientRect(), style = getComputedStyle(summary), container = getComputedStyle(details);
      return { width: innerWidth, theme: document.documentElement.dataset.theme,
        open: details.open, summaryHeight: bounds.height, summaryWidth: bounds.width,
        overflow: document.documentElement.scrollWidth > innerWidth,
        border: container.borderTopWidth, background: container.backgroundColor,
        arrowTransform: getComputedStyle(arrow).transform, arrowSvg: arrow.querySelectorAll('svg').length,
        focused: document.activeElement === summary, focusVisible: summary.matches(':focus-visible'),
        outlineStyle: style.outlineStyle, outlineWidth: style.outlineWidth,
        sameOwner: shares.forWindow(1) === owner, samePanel: document.querySelector('.screen-share') === panel,
        activeOwners: shares.activeOwners.length, phase: owner.view.value.phase,
      };
    }
    /** :param value: 原生展开属性。:returns: 等待实际Vue更新，不派发输入事件。 */
    async function setOpen(value: boolean) {
      document.querySelector<HTMLDetailsElement>('.share-disclosure')!.open = value;
      await nextTick();
    }
    /** :returns: 设置原生焦点，读取可见焦点样式，不模拟键盘事件。 */
    function focusSummary() { document.querySelector<HTMLElement>('.share-disclosure summary')!.focus(); }
    return () => h(AConfigProvider, { theme: consoleTheme(theme.dark.value) }, () =>
      h('main', { style: 'width:100%;min-width:0;max-width:1120px;margin:24px auto;padding:16px;box-sizing:border-box' }, [
        h(ScreenShareDisclosure, { windowId: 1, playerOnline: false }),
      ]));
  },
};
const app = createApp(root).use(pinia).use(i18n); app.mount('#app');
window.addEventListener('pagehide', () => { app.unmount(); disposePinia(pinia); }, { once: true });
