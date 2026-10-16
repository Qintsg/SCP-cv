/** 启动与原生恢复链路归属；实际Auth/API/Stores，生命周期及EventSource仅用内存边界。 */
import assert from 'node:assert/strict';
import test,{before,after,afterEach}from'node:test';
import {setImmediate as nextTurn}from'node:timers/promises';
import {Window}from'happy-dom';
import {createDomTestServer}from'./dom-test-server.mjs';
const browser=new Window({url:'http://localhost:5173/dashboard'});
const names=['window','document','navigator','history','location','HTMLElement','Element','Node','SVGElement'];
const globals=new Map(names.map(name=>[name,Object.getOwnPropertyDescriptor(globalThis,name)]));
for(const name of names)Object.defineProperty(globalThis,name,{configurable:true,writable:true,value:browser[name]});
const originalFetch=globalThis.fetch,originalEvents=globalThis.EventSource;
let server,vue,createPinia,setActivePinia,routing,api,client,useAuth,useRuntime,useToasts,bootstrap,install,auth,runtime,toasts,router,requests,respond,gates,running,sockets,cleanup,event,back;
before(async()=>{
 [vue,{createPinia,setActivePinia},routing,server]=await Promise.all([import('vue'),import('pinia'),import('vue-router'),createDomTestServer()]);
 api=await server.ssrLoadModule('/src/services/api.ts');({clientConnection:client}=await server.ssrLoadModule('/src/platform/connection.ts'));
 ({useAuthStore:useAuth}=await server.ssrLoadModule('/src/stores/auth.ts'));({useRuntimeStore:useRuntime}=await server.ssrLoadModule('/src/stores/runtime.ts'));
 ({useToastStore:useToasts}=await server.ssrLoadModule('/src/composables/useToast.ts'));({bootstrapStores:bootstrap}=await server.ssrLoadModule('/src/stores/index.ts'));
 ({installClientLifecycle:install}=await server.ssrLoadModule('/src/platform/lifecycle.ts'));
});
/** :returns: 认证/全量读取与Vue回调的事件队列。 */
async function flush(){await nextTurn();await vue.nextTick();await nextTurn();await vue.nextTick();}
/** :param data: 假数据。:param status: 状态。:returns: 真实解析路径。 */
function json(data,status=200){return new Response(JSON.stringify(data),{status,headers:{'Content-Type':'application/json'}});}
/** :returns: 总会在清理时释放的假网络。 */
function gate(){let resolve;const promise=new Promise(done=>{resolve=done;});const value={promise,resolve};gates.push(value);return value;}
/** :param value: 在途启动。:returns: 不遗漏拒绝的最终结果。 */
function run(value){const result=value.then(value=>({value}),error=>({error}));running.push(result);return result;}
/** :returns: HTTP实际模块、Fake原生订阅和Fake SSE，不建立任何真实连接。 */
async function setup(){
 gates=[];running=[];requests=[];sockets=[];event=undefined;back=undefined;cleanup=undefined;browser.localStorage.clear();
 const pinia=createPinia();setActivePinia(pinia);auth=useAuth(pinia);auth.user={id:1,username:'memory-one'};auth.initialized=true;
 api.configureApiServer({origin:'http://initial.invalid',displayName:'initial'});api.clearApiSessionState();runtime=useRuntime(pinia);toasts=useToasts(pinia);
 router=routing.createRouter({history:routing.createMemoryHistory(),routes:[{path:'/',redirect:'/dashboard'},...['dashboard','login','connect'].map(path=>({path:'/'+path,component:{render:()=>null}}))]});await router.push('/dashboard');
 respond=async path=>path==='/api/auth/csrf/'?json({csrfToken:'memory-token'}):path==='/api/auth/status/'?json({authenticated:false,user:null}):json({detail:'当前受控读取失败'},503);
 globalThis.fetch=(url,options={})=>{const path=new URL(String(url),browser.location.href).pathname;requests.push({path,method:options.method||'GET'});return respond(path,options);};
 globalThis.EventSource=class{static CLOSED=2;readyState=0;constructor(url){this.url=url;sockets.push(this);}addEventListener(){}close(){this.readyState=2;}};
 api.registerUnauthorizedHandler(()=>{auth.clearLocal();void router.replace('/login');});
}
/** :returns: 实际注册生命周期处理器；回调在本机测试内存调用，不发原生指令。 */
function installNative(){cleanup=install({onLifecycleChanged:callback=>{event=callback;return()=>{};},onBackRequested:callback=>{back=callback;return()=>{};}},router);}
afterEach(async()=>{
 cleanup?.();for(const item of gates??[])item.resolve(json({authenticated:false,user:null}));await Promise.all(running??[]);await flush();await flush();
 runtime?.disconnectEvents();toasts?.clear();auth?.$dispose();api.registerUnauthorizedHandler(null);await browser.happyDOM.abort();globalThis.fetch=originalFetch;globalThis.EventSource=originalEvents;
});
after(async()=>{await server.close();await browser.happyDOM.close();for(const[name,value]of globals){if(value)Object.defineProperty(globalThis,name,value);else delete globalThis[name];}});

for(const kind of ['host','same-id','page'])test(`启动快照${kind}失效后不串失败提示或新SSE`,async()=>{
 await setup();const hold=gate();respond=()=>hold.promise;let active=true;const operation=run(bootstrap(()=>active));await flush();
 if(kind==='host')client.restoreProfile({origin:'http://new.invalid',displayName:'new'});
 if(kind==='same-id')auth.user={id:1,username:'new-memory-one'};
 if(kind==='page')active=false;
 hold.resolve(json({detail:'旧受控读取失败'},503));await operation;await flush();assert.equal(sockets.length,0);assert.equal(toasts.items.length,0);
});
test('当前部分读取失败仍明确警告并只建一个当前SSE',async()=>{
 await setup();await bootstrap();assert.equal(toasts.items.length,1);assert.equal(toasts.items[0].level,'warning');assert.equal(sockets.length,1);assert.equal(sockets[0].url,'http://initial.invalid/api/events/');
});
for(const state of ['inactive','dispose'])for(const authenticated of [false,true])test(`原生${state}后旧状态${authenticated}不回写/续读/导航`,async()=>{
 await setup();const hold=gate(),fallback=respond;respond=(path,options)=>path==='/api/auth/status/'?hold.promise:fallback(path,options);
 installNative();event('active');await flush();const before=requests.length;
 if(state==='inactive')event('inactive');else cleanup();
 hold.resolve(json({authenticated,user:authenticated?{id:2,username:'memory-old'}:null}));await flush();await flush();
 assert.equal(requests.length,before);assert.equal(sockets.length,0);assert.equal(toasts.items.length,0);assert.equal(router.currentRoute.value.path,'/dashboard');assert.equal(auth.user,null);
});
test('重复active共用当前探测，inactive后的新active不等待旧探测',async()=>{
 await setup();const hold=gate(),fallback=respond;let first=true;
 respond=(path,options)=>{if(path==='/api/auth/status/'&&first){first=false;return hold.promise;}return fallback(path,options);};
 installNative();event('active');event('active');await flush();const firstCount=requests.filter(item=>item.path==='/api/auth/status/').length;
 event('inactive');event('active');await flush();await flush();const currentCount=requests.filter(item=>item.path==='/api/auth/status/').length;
 hold.resolve(json({authenticated:true,user:{id:2,username:'memory-old'}}));await flush();await flush();
 assert.equal(firstCount,1);assert.equal(currentCount,2);assert.equal(router.currentRoute.value.path,'/login');assert.equal(auth.user,null);assert.equal(sockets.length,0);
});
test('当前未登录恢复仍去登录页，已卸载的晚到原生通知不再探测',async()=>{
 await setup();installNative();event('active');await flush();await flush();assert.equal(router.currentRoute.value.path,'/login');
 const old=event,count=requests.length;cleanup();old('active');await flush();assert.equal(requests.length,count);assert.equal(await back(),false);
});
