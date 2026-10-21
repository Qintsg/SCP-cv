/** 电源/静音回归共用真实Vue、Pinia、Ant与HTTP边界；所有请求均在内存截断。 */
import { before, after, afterEach } from 'node:test';
import { setImmediate as nextTurn } from 'node:timers/promises';
import { Window } from 'happy-dom';
import { createDomTestServer, visibleText, prepareSliderGeometry, browserKeyCode } from '../dom-test-server.mjs';
export { visibleText } from '../dom-test-server.mjs';
export const browser = new Window({ url: 'http://localhost:5173/dashboard' });
const names = ['window','document','navigator','history','location','HTMLElement','HTMLInputElement','Element','Node','SVGElement','Document','MutationObserver','ResizeObserver','Image','getComputedStyle','requestAnimationFrame','cancelAnimationFrame'];
const globals = new Map(names.map(name => [name, Object.getOwnPropertyDescriptor(globalThis,name)]));
for (const name of names) Object.defineProperty(globalThis,name,{configurable:true,writable:true,value:typeof browser[name]==='function'&&/^[a-z]/.test(name)?browser[name].bind(browser):browser[name]});
const originalFetch = globalThis.fetch;
let server,vue,createPinia,disposePinia,setActivePinia,createRouter,createMemoryHistory,RouterView,i18n,Dashboard,PowerSettings,FullSettings,DialogHost,useAuth,useRuntime,useDevices,useToasts,useDialog,apiModule;
export let client,calls,auth,runtime,devices,toasts,dialog,app,router,pending,running;
let pinia,respond;
before(async()=>{
 [vue,{createPinia,disposePinia,setActivePinia},{createRouter,createMemoryHistory,RouterView},server]=await Promise.all([import('vue'),import('pinia'),import('vue-router'),createDomTestServer()]);
 ({i18n}=await server.ssrLoadModule('/src/locales/index.ts'));
 ({default:Dashboard}=await server.ssrLoadModule('/src/features/dashboard/DashboardView.vue'));
 ({default:PowerSettings}=await server.ssrLoadModule('/src/features/settings/tabs/DevicePowerSettingsTab.vue'));
 ({default:FullSettings}=await server.ssrLoadModule('/src/features/settings/SettingsView.vue'));
 ({default:DialogHost}=await server.ssrLoadModule('/src/design-system/FDialogHost.vue'));
 ({useAuthStore:useAuth}=await server.ssrLoadModule('/src/stores/auth.ts'));
 ({useRuntimeStore:useRuntime}=await server.ssrLoadModule('/src/stores/runtime.ts'));
 ({useDeviceStore:useDevices}=await server.ssrLoadModule('/src/stores/devices.ts'));
 ({useToastStore:useToasts}=await server.ssrLoadModule('/src/composables/useToast.ts'));
 ({useDialogStore:useDialog}=await server.ssrLoadModule('/src/composables/useDialog.ts'));
 ({clientConnection:client}=await server.ssrLoadModule('/src/platform/connection.ts'));
 apiModule=await server.ssrLoadModule('/src/services/api.ts');
});
/** :param value: 假HTTP载荷。:param status: HTTP状态。:returns: 正式API解析的Response。 */
export function json(value,status=200){return new Response(JSON.stringify(value),{status,headers:{'Content-Type':'application/json'}});}
/** :returns: 总在清理时释放，避免用未完成测试冒充产品失败。 */
export function gate(){let resolve;const promise=new Promise(done=>{resolve=done;});const result={promise,resolve};pending.push(result);return result;}
/** :param promise: 原操作。:returns: 立即消费拒绝、保留原结果的断言凭据。 */
export function run(promise){const result=promise.then(value=>({value}),error=>({error}));running.push(result);return result;}
/** :returns: Vue与当前已完成网络的微任务收口，不假称在途请求完成。 */
export async function flush(){await vue.nextTick();await nextTurn();await vue.nextTick();}
/** :returns: 独立主机/认证/设备状态与原HTTP边界。 */
export function setup(){
 pending=[];running=[];calls=[];
 client.restoreProfile({origin:browser.location.origin,displayName:'内存主机',allowInsecureDevelopment:true});
 pinia=createPinia();setActivePinia(pinia);auth=useAuth(pinia);auth.user={id:1,username:'memory',is_staff:true,is_superuser:true};auth.initialized=true;
 runtime=useRuntime(pinia);runtime.systemVolume.level=50;devices=useDevices(pinia);toasts=useToasts(pinia);dialog=useDialog(pinia);
 apiModule.clearApiSessionState();apiModule.registerUnauthorizedHandler(null);
 respond=async(path,options)=>json(path==='/api/video-wall/layout/'?{layout:{draft:null}}
  :path==='/api/auth/csrf/'?{csrfToken:'memory-token'}:path==='/api/auth/status/'?{authenticated:true,user:{id:1,username:'memory'}}
   :path==='/api/volume/'?volume(JSON.parse(options.body||'{}')):{success:true});
 globalThis.fetch=(url,options={})=>{const path=new URL(String(url),browser.location.href).pathname;calls.push({url:String(url),path,method:options.method||'GET',body:options.body});return respond(path,options);};
}
/** :param handler: 有界网络替身。:returns: 替换当前网络响应；内部模块仍是真实代码。 */
export function respondWith(handler){respond=handler;}
/** :param data: 本次音量字段。:returns: 无实际声卡确认的快照。 */
export function volume(data={}){return{volume:{level:data.level??50,muted:data.muted??false,system_synced:false,backend:'simulation'}};}
/** :param kind: 主页面。:returns: 正式页面、路由及真实Ant危险确认宿主。 */
export async function mount(kind='dashboard'){
 setup();router=createRouter({history:createMemoryHistory(),routes:[{path:'/dashboard',component:Dashboard},{path:'/power',component:PowerSettings},{path:'/full-settings',component:FullSettings},{path:'/other',component:{render:()=>vue.h('p','其它页面')}},{path:'/display/:id',component:{render:()=>vue.h('p','输出页')}}]});
 await router.push(kind==='dashboard'?'/dashboard':kind==='fullSettings'?'/full-settings':'/power');const root=browser.document.createElement('div');browser.document.body.append(root);
 app=vue.createApp({render:()=>vue.h(vue.Fragment,[vue.h(RouterView),vue.h(DialogHost)])});app.use(pinia).use(i18n).use(router);app.mount(root);await flush();toasts.clear();
}
/** :param change: 归属边界。:returns: 通过真实公开Store/路由动作使旧环境失效。 */
export async function invalidate(change){
 if(change==='host')client.restoreProfile({origin:'https://new-memory.invalid',displayName:'新主机'});
 else if(change==='account'||change==='relogin')auth.user={id:change==='account'?2:1,username:'new-session'};
 else if(change==='passwordRevision')await auth.changePassword('memory-old','memory-new');
 else if(change==='reset')devices.$reset();
 else if(change==='route')await router.push('/other');
 else if(change==='unmount'){app?.unmount();app=undefined;}
 await flush();
}
/** :param caption: 已渲染按钮文案。:param root: 当前区域。:returns: 实际原生button。 */
export function button(caption,root=browser.document){return [...root.querySelectorAll('button')].find(node=>visibleText(node.textContent)===visibleText(caption)||node.getAttribute('aria-label')===caption);}
/** :returns: 当前首页真实静音开关。 */
export function muteSwitch(){return browser.document.querySelector('[role="switch"][aria-label="启用系统静音"]');}
/** :returns: 系统音量的真实键盘输入，不调用内部处理器。 */
export async function decrease(){const handle=browser.document.querySelector('[role="slider"][aria-label="系统音量"]');prepareSliderGeometry(handle);handle.focus();for(const type of ['keydown','keyup']){handle.dispatchEvent(new browser.KeyboardEvent(type,{key:'ArrowLeft',keyCode:browserKeyCode('ArrowLeft'),which:browserKeyCode('ArrowLeft'),bubbles:true,cancelable:true}));await flush();}}
/** :param handler: 仅接当前HTTP401的正式回调。:returns: 不修改错误/认证逻辑的注册。 */
export function onUnauthorized(handler){apiModule.registerUnauthorizedHandler(handler);}
/** :param path: 真实模块路径。:returns: 共用测试服务器实例，避免生成第二套占用表。 */
export function loadModule(path){return server.ssrLoadModule(path);}
afterEach(async()=>{app?.unmount();app=undefined;for(const hold of pending||[])hold.resolve(json(volume()));await Promise.all(running||[]);await flush();apiModule.registerUnauthorizedHandler(null);if(pinia)disposePinia(pinia);browser.document.body.replaceChildren();await browser.happyDOM.abort();globalThis.fetch=originalFetch;});
after(async()=>{await server.close();await browser.happyDOM.close();for(const[name,descriptor]of globals){if(descriptor)Object.defineProperty(globalThis,name,descriptor);else delete globalThis[name];}});
