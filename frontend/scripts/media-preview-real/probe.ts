// 文件描述：真实媒体接口的只读预览宿主。项目：SCP-cv；文件：probe.ts；作者：Qintsg；日期：2026-10-30。
import { createApp, h, ref, nextTick } from 'vue';
import { createPinia } from 'pinia';
import { createRouter, createMemoryHistory } from 'vue-router';
import ConfigProvider from 'ant-design-vue/es/config-provider';
import { api } from '@/services/api';
import { useAuthStore } from '@/stores/auth';
import { useSourceStore } from '@/stores/sources';
import { useScreenShareStore } from '@/features/screen-share/screenShareStore';
import { useScreenShare } from '@/features/screen-share/useScreenShare';
import SourcePreviewDrawer from '@/features/sources/SourcePreviewDrawer.vue';
import { getPreviewPool } from '@/features/preview/previewPool';
import { consoleTheme } from '@/design-system/theme/ant-theme';
import { i18n } from '@/locales';
import 'ant-design-vue/dist/reset.css';
import '@/styles/base.css';
import '@/styles/tailwind.css';
import '@/styles/console.css';

const pinia = createPinia(), auth = useAuthStore(pinia), sources = useSourceStore(pinia);
await auth.ensureInitialized(); await sources.refresh();
const parameters = new URL(location.href).searchParams, sourceId = Number(parameters.get('id'));
const source = sources.findById(sourceId); if (!source) throw new Error('真实来源不存在。');
const open = ref(true), ready = ref(false), selected = ref(source), mounted = ref(true);
const Observer = { setup() { const sharing = useScreenShare(() => 1); return () => h('output', sharing.view.value.phase); } };
const shares = useScreenShareStore(pinia), owner = shares.forWindow(1);
const root = { setup() { return () => h(ConfigProvider, { theme: consoleTheme(false) }, () => [
  h('main', { style: 'padding:20px' }, [h('h1', '媒体预览验收'), mounted.value ? h(Observer) : null]),
  h(SourcePreviewDrawer, { source: selected.value, open: open.value, 'onUpdate:open': value => { open.value = value; } }),
]); } };
const app = createApp(root).use(pinia).use(i18n);
const router = createRouter({ history: createMemoryHistory(), routes: [{ path: '/:pathMatch(.*)*', component: { render: () => null } }] });
app.use(router); await router.push('/sources'); await router.isReady(); app.mount('#app');
// 测试宿主自行执行组件寿命检查，浏览器驱动只读取结果，不模拟输入。
const checks = [];
for (let index = 0; index < 10; index++) {
  mounted.value = false; await nextTick(); await router.push(`/check-${index}`);
  if (shares.forWindow(1) !== owner) throw new Error('页面卸载重建了共享所有者。');
  mounted.value = true; await nextTick(); checks.push(index);
}
window.media030 = { checks, diagnostics: () => getPreviewPool().diagnostics, close: () => { open.value = false; },
  source: source.id, name: source.name, page: 1, ready: true };
ready.value = true;
// 按真实资源就绪状态自动翻到第二页，再关闭；不发送任何播放器控制。
let turn = 0;
const timer = setInterval(async () => {
  turn++;
  let instance = document.querySelector('.presentation-preview, .pdf-preview')?.__vueParentComponent;
  while (instance && !['PresentationSourcePreview', 'PdfSourcePreview'].includes(instance.type.__name)) instance = instance.parent;
  if (parameters.get('page') === '2' && instance && !window.media030.turned &&
    (instance.setupState.pages?.length > 1 || instance.setupState.total > 1)) {
    instance.devtoolsRawSetupState.page.value = 2; window.media030.turned = true;
  }
  if (turn > 120) clearInterval(timer);
}, 500);
window.addEventListener('pagehide', () => { clearInterval(timer); shares.disposeAll(); app.unmount(); }, { once: true });
