/** 真实Vue目录和外壳交互：清除条件、失败反馈、路由滚动与焦点，无实体流量。 */
import { visibleText } from './dom-test-server.mjs';

import assert from 'node:assert/strict';
import test, { before, after, afterEach } from 'node:test';
import { setImmediate as nextTurn } from 'node:timers/promises';
import { Window } from 'happy-dom';
import { createDomTestServer } from './dom-test-server.mjs';
const browser = new Window({url:'http://localhost:5173/sources'});
const names=['window','document','navigator','history','location','HTMLElement','HTMLInputElement','HTMLTextAreaElement','Element','Node','SVGElement','Document','MutationObserver','ResizeObserver','Image','InputEvent','KeyboardEvent','PointerEvent','getComputedStyle','requestAnimationFrame','cancelAnimationFrame'];
const previous=new Map(names.map(name=>[name,Object.getOwnPropertyDescriptor(globalThis,name)]));
for(const name of names)Object.defineProperty(globalThis,name,{configurable:true,writable:true,value:typeof browser[name]==='function'&&/^[a-z]/.test(name)?browser[name].bind(browser):browser[name]});
const originalFetch=globalThis.fetch;
let server,vue,createPinia,routerModule,i18n,Sources,Shell,useSources,app;
before(async()=>{
 [vue,{createPinia},routerModule,server]=await Promise.all([import('vue'),import('pinia'),import('vue-router'),createDomTestServer()]);
 ({i18n}=await server.ssrLoadModule('/src/locales/index.ts'));
 ({default:Sources}=await server.ssrLoadModule('/src/features/sources/SourcesView.vue'));
 ({default:Shell}=await server.ssrLoadModule('/src/layouts/AppShell.vue'));
 ({useSourceStore:useSources}=await server.ssrLoadModule('/src/stores/sources.ts'));
});
/** :returns: Vue与Promise渲染完成，不依赖几何或实体状态。 */
async function flush(){await nextTurn();await vue.nextTick();await nextTurn();await vue.nextTick();}
afterEach(async()=>{app?.unmount();app=undefined;await flush();browser.document.body.replaceChildren();await browser.happyDOM.abort();globalThis.fetch=originalFetch;});
after(async()=>{await server?.close();await browser.happyDOM.close();for(const[name,descriptor]of previous){if(descriptor)Object.defineProperty(globalThis,name,descriptor);else delete globalThis[name];}});
/** :param shell: 是否挂真实路由外壳。:returns: 独立store、DOM和内存路由。 */
async function mount(shell=false){
 const pinia=createPinia();const store=useSources(pinia);
 store.folders=[{id:1,name:'测试目录',parent_id:null,relative_path:'/测试目录'}];store.currentFolderId=1;
 store.sources=[{id:1,name:'测试图片',source_type:'image',folder_id:1,path:'/测试目录/a.png',is_available:true,is_protected:false,metadata:{},uri:'',file_size:1}];
 const router=routerModule.createRouter({history:routerModule.createMemoryHistory(),routes:[{path:'/sources',component:Sources},{path:'/other',component:{render:()=>vue.h('h1','另一个页面')}},{path:'/:pathMatch(.*)*',component:{render:()=>null}}]});
 app=vue.createApp(shell?Shell:Sources);app.use(pinia);app.use(i18n);app.use(router);await router.push('/sources');await router.isReady();
 const root=browser.document.createElement('div');browser.document.body.append(root);app.mount(root);await flush();return{store,router,root};
}
test('类型筛选无匹配可清除，保留目录并把键盘焦点还给搜索框',async()=>{
 let writes=0;globalThis.fetch=async()=>{writes++;throw new Error('不应访问网络');};
 const{store,root}=await mount();store.setCategory('video');store.setSearchKeyword('无匹配');await flush();
 assert.match(root.textContent,/没有匹配的媒体/);
 const clear=[...root.querySelectorAll('button')].find(button=>visibleText(button.textContent) === visibleText('清除筛选'));assert.ok(clear);clear.click();await flush();
 assert.equal(store.category,'all');assert.equal(store.searchKeyword,'');assert.equal(store.currentFolderId,1);assert.equal(writes,0);
 assert.equal(browser.document.activeElement?.getAttribute('aria-label'),'搜索媒体名称或路径');assert.match(root.textContent,/测试图片/);
});
test('刷新失败显示读取失败，不伪装成目录为空',async()=>{
 globalThis.fetch=async()=>{throw new Error('本轮读取失败');};const{root}=await mount();
 root.querySelector('button[aria-label="刷新媒体文件"]').click();await flush();await flush();
 assert.match(root.textContent,/媒体列表读取失败/);assert.doesNotMatch(root.textContent,/当前目录没有媒体文件/);
});
test('目录变化后焦点留在新路径，目录卡片移除不丢失键盘位置',async()=>{
 globalThis.fetch=async()=>{throw new Error('不应访问网络');};const{store,root}=await mount();store.currentFolderId=null;await flush();
 assert.equal(browser.document.activeElement,root.querySelector('.sources-view__path'));assert.equal(browser.document.activeElement.textContent,'/');
});
test('切换页面重置主内容滚动并定位焦点，不发业务请求',async()=>{
 let calls=0;globalThis.fetch=async()=>{calls++;throw new Error('不应访问网络');};const{root,router}=await mount(true);
 const main=root.querySelector('#main-content');main.scrollTop=320;await router.push('/other');await flush();
 assert.equal(main.scrollTop,0);assert.equal(browser.document.activeElement,main);assert.match(main.textContent,/另一个页面/);assert.equal(calls,0);
});
