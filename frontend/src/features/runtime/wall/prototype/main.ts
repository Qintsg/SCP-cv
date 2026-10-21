/** 独立原型入口，不导入Pinia、生产API或平台连接。 */
import { createApp, h } from 'vue';
import { createRouter, createWebHistory, RouterView } from 'vue-router';
import DemoApp from './DemoApp.vue';
import 'ant-design-vue/dist/reset.css';
import '@/styles/console.css';
import './prototype.css';

document.documentElement.dataset.theme = 'light';
const router = createRouter({ history: createWebHistory(), routes: [{ path: '/:pathMatch(.*)*', component: DemoApp }] });
const app = createApp({ render: () => h(RouterView) }).use(router);
void router.isReady().then(() => app.mount('#app'));
