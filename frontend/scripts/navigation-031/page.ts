// 文件描述：031真实导航外壳的只读几何验收页面。
// 项目：SCP-cv；文件：page.ts；作者：Qintsg；日期：2026-10-30。
import { createApp, h, onMounted } from 'vue';
import { createPinia } from 'pinia';
import { createMemoryHistory, createRouter, useRoute } from 'vue-router';
import AConfigProvider from 'ant-design-vue/es/config-provider';
import AppShell from '../../src/layouts/AppShell.vue';
import { i18n } from '../../src/locales';
import { consoleTheme } from '../../src/design-system/theme/ant-theme';
import { useTheme } from '../../src/composables/useTheme';
import { bindAppHeight } from '../../src/composables/useAppHeight';
import { getClientBuildTarget } from '../../src/platform';
import { navigationPosition, type NavigationSlotGeometry } from '../../src/layouts/glass/navigationGeometry';
import 'ant-design-vue/dist/reset.css';
import '../../src/styles/base.css';
import '../../src/styles/tailwind.css';
import '../../src/styles/experience.css';
import '../../src/styles/console.css';

const parameters = new URL(location.href).searchParams;
const path = parameters.get('route') || '/dashboard';
const requestedTheme = parameters.get('theme') === 'dark' ? 'dark' : 'light';
const pinia = createPinia();
const content = {
  setup() {
    const route = useRoute();
    return () => h('section', { class: 'navigation-031-content' }, [
      h('h1', '导航布局验收'), h('p', route.path),
      h('div', { class: 'navigation-031-colors', 'aria-hidden': 'true' }),
    ]);
  },
};
const router = createRouter({ history: createMemoryHistory(), routes: [{ path: '/:pathMatch(.*)*', component: content }] });
const root = {
  setup() {
    const theme = useTheme(); theme.mode.value = requestedTheme;
    onMounted(() => { Object.assign(window, { navigation031: { snapshot } }); });
    return () => h(AConfigProvider, { theme: consoleTheme(theme.dark.value) }, () => h(AppShell));
  },
};
const app = createApp(root).use(pinia).use(i18n).use(router);
await router.push(path); await router.isReady(); app.mount('#app');
const releaseHeight = bindAppHeight();
window.addEventListener('pagehide', () => { releaseHeight(); app.unmount(); }, { once: true });

/** :param element: 实际页面元素。:returns: 有限纯值矩形，不序列化原生DOM对象。 */
function rectangle(element: Element | null) {
  if (!element) return null;
  const value = element.getBoundingClientRect();
  return { left: value.left, right: value.right, top: value.top, bottom: value.bottom, width: value.width, height: value.height };
}

/** :returns: 当前真实布局、命中几何、SVG材质及系统偏好；不发送导航或设备请求。 */
function snapshot() {
  const shell = document.querySelector<HTMLElement>('.console-shell');
  const nav = document.querySelector<HTMLElement>('.liquid-nav');
  const buttons = [...(nav?.querySelectorAll<HTMLElement>('.liquid-nav__button') ?? [])];
  const bounds = buttons.map(button => rectangle(button)!);
  const slots: NavigationSlotGeometry[] = bounds.map(button => ({ left: button.left, right: button.right }));
  const lens = nav?.querySelector<HTMLElement>('liquid-glass');
  const lensSurface = lens?.shadowRoot?.querySelector('.lg-glass');
  const hits = bounds.flatMap((button, index) => [.05, .25, .5, .75, .95].map(fraction => {
    const clientX = button.left + button.width * fraction, clientY = button.top + button.height / 2;
    const actual = document.elementFromPoint(clientX, clientY)?.closest('.liquid-nav__button');
    const position = navigationPosition(clientX, slots);
    return { expected: index, actual: buttons.indexOf(actual as HTMLElement), position, rounded: Math.round(position), fraction };
  }));
  const centers = bounds.map((button, index) => ({ expected: index, position: navigationPosition((button.left + button.right) / 2, slots) }));
  const active = buttons.findIndex(button => button.getAttribute('aria-current') === 'page');
  return {
    mode: getClientBuildTarget(), width: innerWidth, height: innerHeight, theme: document.documentElement.dataset.theme,
    coarse: matchMedia('(any-pointer: coarse)').matches,
    mobile: shell?.dataset.mobileControls, tablet: shell?.dataset.tabletControls, touchNavigation: shell?.dataset.touchNavigation,
    sidebar: rectangle(document.querySelector('.console-sider')), nav: rectangle(nav), buttons: bounds, active,
    centers, hits, overflow: document.documentElement.scrollWidth > innerWidth,
    links: [...document.querySelectorAll<HTMLAnchorElement>('.console-navigation a[href]')].map(link => link.getAttribute('href')),
    glass: lens ? {
      rect: rectangle(lens), strategy: lens.dataset.glassStrategy, effect: lens.getAttribute('effect-mode'),
      revealMethod: typeof (lens as unknown as { reveal: unknown }).reveal,
      displacement: lens.shadowRoot?.querySelectorAll('feDisplacementMap').length ?? 0,
      filter: lensSurface ? getComputedStyle(lensSurface).backdropFilter : null,
      activeColor: nav?.querySelector('.liquid-nav__icons--color') ? getComputedStyle(nav.querySelector('.liquid-nav__icons--color')!).color : null,
      trackFilter: nav?.querySelector('.liquid-nav__track') ? getComputedStyle(nav.querySelector('.liquid-nav__track')!).backdropFilter : null,
      rootTransform: nav ? getComputedStyle(nav).transform : null,
    } : null,
    reducedMotion: matchMedia('(prefers-reduced-motion: reduce)').matches,
    reducedTransparency: matchMedia('(prefers-reduced-transparency: reduce)').matches,
  };
}
