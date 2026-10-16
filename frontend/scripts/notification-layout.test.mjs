/** Ant通知真实DOM合同：持久错误、手动关闭、重试动作及弹层层级；几何另由浏览器验收。 */
import { visibleText } from './dom-test-server.mjs';

import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { setImmediate as nextTurn } from 'node:timers/promises';
import test,{before,after,afterEach}from'node:test';
import {Window}from'happy-dom';
import {createDomTestServer,waitForDom}from'./dom-test-server.mjs';
const browser=new Window({url:'http://localhost:5173/sources'});
const names=['window','document','navigator','HTMLElement','HTMLInputElement','HTMLTextAreaElement','Element','Node','SVGElement',
 'Document','MutationObserver','ResizeObserver','Image','InputEvent','getComputedStyle','requestAnimationFrame','cancelAnimationFrame'];
const old=new Map(names.map(n=>[n,Object.getOwnPropertyDescriptor(globalThis,n)]));
for(const n of names)Object.defineProperty(globalThis,n,{value:typeof browser[n]==='function'&&/^[a-z]/.test(n)?browser[n].bind(browser):browser[n],configurable:true,writable:true});
let vue,createPinia,server,Host,Drawer,ControlDrawer,useStore,app,pinia,open;
before(async()=>{
 [vue,{createPinia},server]=await Promise.all([import('vue'),import('pinia'),createDomTestServer()]);
 ({default:Host}=await server.ssrLoadModule('/src/design-system/FToastHost.vue'));
 ({default:Drawer}=await server.ssrLoadModule('/src/features/sources/AddSourceDrawer.vue'));
 ({default:ControlDrawer}=await server.ssrLoadModule('/src/design-system/ControlDrawer.vue'));
 ({useToastStore:useStore}=await server.ssrLoadModule('/src/composables/useToast.ts'));
});
afterEach(async()=>{useStore?.(pinia)?.clear();app?.unmount();app=undefined;browser.document.body.replaceChildren();await browser.happyDOM.abort();});
after(async()=>{await server?.close();await browser.happyDOM.close();for(const[n,d]of old){if(d)Object.defineProperty(globalThis,n,d);else delete globalThis[n];}});
/** :returns: 两轮响应式与通知入队完成。 */
async function flush(){await vue.nextTick();await nextTurn();await vue.nextTick();await nextTurn();}
/** :param width: 视口宽度。:param action: 可选重试。:returns: 业务通知store。 */
async function mount(width,action){
 browser.happyDOM.setWindowSize({width,height:844});pinia=createPinia();const store=useStore(pinia);
 store.error('保存失败','当前目录已存在同名媒体',action?{label:'重试',onTrigger:action}:undefined);
 open=vue.ref(false);const {createI18n}=await import('vue-i18n');
 app=vue.createApp({render:()=>vue.h('div',[vue.h(Host),vue.h(Drawer,{open:open.value})])});
 app.use(pinia);app.use(createI18n({legacy:false,locale:'zh',messages:{zh:{}},missingWarn:false,fallbackWarn:false}));
 const host=browser.document.createElement('div');browser.document.body.append(host);app.mount(host);await flush();return store;
}
test('手机通知放顶部、错误持久且原错误内容完整',async()=>{
 const store=await mount(390);assert.equal(store.items[0].duration,0);
 const container=browser.document.querySelector('.ant-notification-topRight');assert.ok(container);
 assert.match(container.textContent,/保存失败/);assert.match(container.textContent,/当前目录已存在同名媒体/);
});
test('桌面通知放右下，不丢失Ant主题上下文宿主',async()=>{
 await mount(1440);assert.ok(browser.document.querySelector('.ant-notification-bottomRight'));
 assert.equal(browser.document.querySelectorAll('.console-toast').length,1);
});
test('持久错误可通过真实关闭入口移除',async()=>{
 const store=await mount(390);const close=browser.document.querySelector('.ant-notification-notice-close');assert.ok(close);
 close.click();await flush();assert.equal(store.items.length,0);
});
test('重试动作只执行一次并关闭原通知',async()=>{
 let count=0;const store=await mount(390,async()=>{count++;});
 const retry=[...browser.document.querySelectorAll('button')].find(b=>visibleText(b.textContent) === visibleText('重试'));assert.ok(retry);
 retry.click();await flush();assert.equal(count,1);assert.equal(store.items.length,0);
});
test('通知层低于新表单，抽屉不再使用会遮住Select的7500层级',async()=>{
 const store=await mount(1440);open.value=true;await flush();await waitForDom(()=>browser.document.querySelector('[role="dialog"]'));
 assert.ok(browser.document.querySelector('[role="dialog"]'));assert.equal(store.items.length,1);
 const css=readFileSync(new URL('../src/styles/console.css',import.meta.url),'utf8');
 assert.match(css,/\.ant-notification\s*\{\s*z-index:1900/);
 const drawer=readFileSync(new URL('../src/features/sources/AddSourceDrawer.vue',import.meta.url),'utf8');
 assert.doesNotMatch(drawer,/:z-index="7500"/);
});
test('手机通知安全区、底栏留白和弹层44px由统一样式声明，真实几何另验',()=>{
 const css=readFileSync(new URL('../src/styles/console.css',import.meta.url),'utf8');
 assert.match(css,/safe-area-inset-top/);assert.match(css,/safe-area-inset-bottom/);assert.match(css,/max-height:calc\(100dvh - 156px/);
 assert.match(css,/min-height:44px/);
 assert.match(css,/\.ant-drawer \.ant-drawer-close[^}]*min-height:44px; min-width:44px/);
});

test('编辑关闭与预览打开交错时，旧离场不能把新弹层焦点抢回背景',async()=>{
 const editing=vue.ref(false),preview=vue.ref(false);
 app=vue.createApp({render:()=>vue.h('div',[
  vue.h('button',{id:'open-editor',onClick:()=>{editing.value=true;}},'新建'),
  vue.h(ControlDrawer,{open:editing.value,title:'编辑'},()=>vue.h('button',{id:'save-preview',onClick:()=>{
   editing.value=false;preview.value=true;
  }},'保存并预览')),
  vue.h(ControlDrawer,{open:preview.value,title:'预览'},()=>vue.h('button','编辑预设')),
 ])});
 const root=browser.document.createElement('div');browser.document.body.append(root);app.mount(root);
 const opener=root.querySelector('#open-editor');opener.focus();opener.click();await flush();
 await waitForDom(()=>browser.document.querySelector('[role="dialog"][aria-label="编辑"]'));
 const save=browser.document.querySelector('#save-preview');save.focus();save.click();await flush();
 await waitForDom(()=>browser.document.querySelector('[role="dialog"][aria-label="编辑"]')?.getAttribute('aria-hidden')==='true');
 const current=browser.document.querySelector('[role="dialog"][aria-label="预览"]');
 assert.ok(current?.contains(browser.document.activeElement),'焦点应留在新预览，不回到背景新建按钮');
 assert.equal(browser.document.querySelector('[aria-label="编辑"]').getAttribute('data-scp-dialog-open'),'false');
});
