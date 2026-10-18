/** 现场设备真实组件回归夹具：Vue/Pinia/Ant/API不替换，仅内存HTTP与300ms续租时钟。 */
import {before,after,afterEach} from 'node:test';
import {setImmediate as nextTurn} from 'node:timers/promises';
import {Window} from 'happy-dom';
import {createDomTestServer,visibleText,browserKeyCode} from '../dom-test-server.mjs';
export {visibleText};
export const browser=new Window({url:'http://localhost:5173/devices'});
const names=['window','document','navigator','HTMLElement','HTMLInputElement','Element','Node','SVGElement','Document','MutationObserver','ResizeObserver','Image','getComputedStyle','requestAnimationFrame','cancelAnimationFrame'];
const previous=new Map(names.map(name=>[name,Object.getOwnPropertyDescriptor(globalThis,name)]));
for(const name of names)Object.defineProperty(globalThis,name,{configurable:true,writable:true,value:typeof browser[name]==='function'&&/^[a-z]/.test(name)?browser[name].bind(browser):browser[name]});
const originalFetch=globalThis.fetch,originalInterval=globalThis.setInterval,originalClearInterval=globalThis.clearInterval;
let vue,createPinia,disposePinia,server,CameraPad,DevicesView,i18n,useAuth,useRuntime,apiModule,pinia,app,selected,respond,mountedKind;
export let auth,runtime,client,calls,pending,running;
const renewals=new Map();let timerId=-1;
export const cameras=['center1','center2'].map((id,index)=>({id,name:'中摄像机'+(index+1),path:'/Camera/'+id,source_id:index+1,movable:true,stream_configured:true,control_available:true,position_count:6,motion_intent:'idle',unavailable_reason:''}));
before(async()=>{
 [vue,{createPinia,disposePinia},server]=await Promise.all([import('vue'),import('pinia'),createDomTestServer()]);
 ({default:CameraPad}=await server.ssrLoadModule('/src/features/devices/CameraControlPad.vue'));
 ({default:DevicesView}=await server.ssrLoadModule('/src/features/devices/DevicesView.vue'));
 ({i18n}=await server.ssrLoadModule('/src/locales/index.ts'));
 ({useAuthStore:useAuth}=await server.ssrLoadModule('/src/stores/auth.ts'));
 ({useRuntimeStore:useRuntime}=await server.ssrLoadModule('/src/stores/runtime.ts'));
 ({clientConnection:client}=await server.ssrLoadModule('/src/platform/connection.ts'));
 apiModule=await server.ssrLoadModule('/src/services/api.ts');
});
/** :param payload: 内存数据。:param status: HTTP码。:returns: 正式解析器的响应。 */
export function json(payload,status=200){return new Response(JSON.stringify(payload),{status,headers:{'Content-Type':'application/json'}});}
/** :returns: 设备能力的最小测试投影，无任何真实设备地址。 */
export function venue(audio={}){return{success:true,venue:{cameras,audio:{screen_volume:60,computer_volume:25,handheld_volume:35,headset_volume:45,source:'ip81',...audio},simulation:true,server_time_ms:Date.now(),capabilities:{screen_volume:true,computer_volume:true,handheld_volume:true,headset_volume:true,hanging_volume:true,source:true,source_ip81:true,source_scp_cv:true,reason:''}}};}
/** :param path: 网络路径。:param options: 原请求。:returns: 默认正常响应，仅在内存。 */
function normalResponse(path,options){return json(path==='/api/venue/'?venue():path==='/api/auth/csrf/'?{csrfToken:'memory-only'}:path==='/api/sources/'?{sources:[]}:path==='/api/folders/'?{folders:[]}:path.includes('/cameras/')?{success:true,camera:{status:'sent_unconfirmed',detail:'本次摄像机请求已发送'}}:{success:true,audio:{status:'sent_unconfirmed',detail:'本次音频请求已发送'}});}
/** :param handler: 仅HTTP边界。:returns: 后续请求使用该处理器，不改组件/API。 */
export function respondWith(handler){respond=handler;}
/** :returns: 自动在清理时释放的真实请求门闩。 */
export function gate(){let resolve;const promise=new Promise(done=>{resolve=done;});const item={promise,resolve};pending.push(item);return item;}
/** :param promise: 原动作。:returns: 立即消费拒绝并保留断言结果。 */
export function run(promise){const result=promise.then(value=>({value}),error=>({error}));running.push(result);return result;}
/** :returns: 已完成HTTP和Vue微任务收口，不等同真实在途结束。 */
export async function flush(){await vue.nextTick();await nextTurn();await vue.nextTick();await nextTurn();}
/** :param kind: 手控块或完整设备页。:returns: 正式组件、独立Pinia及受控时钟。 */
export async function mount(kind='camera'){
 mountedKind=kind;calls=[];pending=[];running=[];respond=async(path,options)=>normalResponse(path,options);
 client.restoreProfile({origin:browser.location.origin,displayName:'内存设备主机',allowInsecureDevelopment:true});
 pinia=createPinia();auth=useAuth(pinia);auth.user={id:1,username:'memory',is_staff:true,is_superuser:true};auth.initialized=true;runtime=useRuntime(pinia);
 apiModule.clearApiSessionState();apiModule.registerUnauthorizedHandler(null);
 globalThis.fetch=(url,options={})=>{const path=new URL(String(url),browser.location.href).pathname;calls.push({url:String(url),path,method:options.method||'GET',body:options.body?JSON.parse(options.body):null});const result=Promise.resolve().then(()=>respond(path,options));running.push(result.catch(()=>undefined));return result;};
 globalThis.setInterval=(callback,delay,...args)=>{if(delay!==300)return originalInterval(callback,delay,...args);const id=timerId--;renewals.set(id,()=>callback(...args));return id;};
 globalThis.clearInterval=id=>{if(renewals.has(id))renewals.delete(id);else originalClearInterval(id);};
 selected=vue.ref({...cameras[0]});const root=browser.document.createElement('div');browser.document.body.append(root);
 app=vue.createApp({render:()=>kind==='camera'?vue.h(CameraPad,{camera:selected.value}):vue.h(DevicesView)});app.use(pinia).use(i18n);app.mount(root);await flush();
}
/** :param change: 真实Store/配置或公开组件属性边界。:returns: 不调用组件内部处理器。 */
export async function invalidate(change){
 if(change==='host')client.restoreProfile({origin:'https://new-venue.invalid',displayName:'新设备主机'});
 else if(change==='projection')runtime.connectionGeneration++;
 else if(change==='account'||change==='relogin')auth.user={id:change==='account'?2:1,username:'new-memory'};
 else if(change==='passwordRevision')await auth.changePassword('fake-old','fake-new');
 else if(change==='logout')auth.clearLocal();
 else if(change==='camera')selected.value={...cameras[1]};
 else if(change==='capability')selected.value={...selected.value,control_available:false};
 else if(change==='unmount'){app?.unmount();app=undefined;}
 await flush();
}
/** :param caption: 可见名称。:returns: 实际原生button，不调用组件私有函数。 */
export function button(caption){return[...browser.document.querySelectorAll('button')].find(node=>visibleText(node.textContent)===visibleText(caption));}
/** :param type: 按下/松开。:returns: 正式方向键输入。 */
export async function key(type='keydown'){const node=browser.document.querySelector('[aria-label="中摄像机1向上"]');node?.dispatchEvent(new browser.KeyboardEvent(type,{key:'Enter',keyCode:browserKeyCode('Enter'),which:browserKeyCode('Enter'),bubbles:true,cancelable:true}));await flush();}
/** :returns: 实际300ms定时回调前进一拍，未改租约内部状态。 */
export async function renew(){for(const callback of [...renewals.values()])callback();await flush();}
/** :returns: 仅运动/位置写，不把认证或只读请求当动作。 */
export function cameraWrites(){return calls.filter(item=>item.path.includes('/cameras/')&&item.method==='POST');}
/** :param path: 实际模块路径。:returns: 同一加载器实例。 */
export function loadModule(path){return server.ssrLoadModule(path);}
/** :param handler: 正式当前401处理器。:returns: 不替换认证寿命判断。 */
export function onUnauthorized(handler){apiModule.registerUnauthorizedHandler(handler);}
afterEach(async()=>{
 const popupMounted=Boolean(browser.document.querySelector('.ant-popconfirm'));
 app?.unmount();app=undefined;
 for(const hold of pending||[])hold.resolve(json({success:true,camera:{detail:'清理完成'},audio:{detail:'清理完成'}}));
 await Promise.all(running||[]);await flush();
 // 弹层的延后挂载须先收口；完整页面有长期浏览器观察任务，不能把窗口全空闲当HTTP终端。
 if(mountedKind==='camera'||popupMounted){await browser.happyDOM.waitUntilComplete();await flush();}
 renewals.clear();globalThis.setInterval=originalInterval;globalThis.clearInterval=originalClearInterval;
 if(pinia)disposePinia(pinia);apiModule.registerUnauthorizedHandler(null);
 browser.document.body.replaceChildren();await browser.happyDOM.abort();globalThis.fetch=originalFetch;
});
after(async()=>{await server.close();await browser.happyDOM.close();for(const[name,descriptor]of previous){if(descriptor)Object.defineProperty(globalThis,name,descriptor);else delete globalThis[name];}});
