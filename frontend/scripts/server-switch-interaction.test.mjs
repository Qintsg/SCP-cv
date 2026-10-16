/** 连接页完整寿命：真实Ant/Runtime/Auth/API/路由，全部网络和原生桥仅在内存。 */
import assert from 'node:assert/strict';
import test,{before,after,afterEach}from'node:test';
import {setImmediate as nextTurn}from'node:timers/promises';
import {Window}from'happy-dom';
import {createDomTestServer}from'./dom-test-server.mjs';
const browser=new Window({url:'http://localhost:5173/connect'});
const names=['window','document','navigator','history','location','HTMLElement','HTMLInputElement','HTMLTextAreaElement','Element','Node','SVGElement',
 'Document','MutationObserver','ResizeObserver','Image','InputEvent','KeyboardEvent','getComputedStyle','requestAnimationFrame','cancelAnimationFrame'];
const globals=new Map(names.map(name=>[name,Object.getOwnPropertyDescriptor(globalThis,name)]));
for(const name of names)Object.defineProperty(globalThis,name,{configurable:true,writable:true,value:typeof browser[name]==='function'&&/^[a-z]/.test(name)?browser[name].bind(browser):browser[name]});
const originalFetch=globalThis.fetch;
let server,vue,createPinia,routing,i18n,View,useAuth,useRuntime,api,client,auth,runtime,app,requests,pending,respond;
before(async()=>{
 [vue,{createPinia},routing,server]=await Promise.all([import('vue'),import('pinia'),import('vue-router'),createDomTestServer()]);
 ({i18n}=await server.ssrLoadModule('/src/locales/index.ts'));({default:View}=await server.ssrLoadModule('/src/features/settings/ServerConnectionView.vue'));
 ({useAuthStore:useAuth}=await server.ssrLoadModule('/src/stores/auth.ts'));({useRuntimeStore:useRuntime}=await server.ssrLoadModule('/src/stores/runtime.ts'));
 api=await server.ssrLoadModule('/src/services/api.ts');({clientConnection:client}=await server.ssrLoadModule('/src/platform/connection.ts'));
});
/** :returns: Vue/异步表单及路由队列完成，不等待未释放的门闩。 */
async function flush(){await nextTurn();await vue.nextTick();await nextTurn();await vue.nextTick();}
/** :param data: 内存HTTP内容。:param status: 状态。:returns: 正式JSON解析器使用的响应。 */
function json(data,status=200){return new Response(JSON.stringify(data),{status,headers:{'Content-Type':'application/json'}});}
/** :param path: 假HTTP路径。:returns: 默认未登录主机，不冒充播放器就绪。 */
function response(path){return json(path==='/api/auth/csrf/'?{csrfToken:'memory-token'}:path==='/api/auth/status/'?{authenticated:false,user:null}:{detail:'ok'});}
/** :returns: 可显式释放的HTTP/原生阶段。 */
function gate(){let resolve;const promise=new Promise(done=>{resolve=done;});const value={promise,resolve};pending.push(value);return value;}
afterEach(async()=>{
 app?.unmount();app=undefined;for(const item of pending??[])item.resolve(json({detail:'cleanup'}));await flush();await flush();
 api.registerUnauthorizedHandler(null);runtime?.disconnectEvents();auth?.$dispose();delete browser.scpCvElectron;browser.document.body.replaceChildren();
 await browser.happyDOM.abort();globalThis.fetch=originalFetch;
});
after(async()=>{await server.close();await browser.happyDOM.close();for(const[name,value]of globals){if(value)Object.defineProperty(globalThis,name,value);else delete globalThis[name];}});
/** :returns: 原连接页、真正路由卸载与Pinia。 */
async function mount(){
 requests=[];pending=[];browser.localStorage.clear();delete browser.scpCvElectron;
 const pinia=createPinia();auth=useAuth(pinia);auth.user=null;auth.initialized=true;
 api.configureApiServer({origin:'http://initial.invalid',displayName:'initial'});api.clearApiSessionState();runtime=useRuntime(pinia);
 respond=async path=>response(path);
 globalThis.fetch=(url,options={})=>{const path=new URL(String(url),browser.location.href).pathname;
  assert.ok(['/api/auth/logout/','/api/auth/csrf/','/api/auth/status/'].includes(path),path);requests.push({path,url:String(url),method:options.method||'GET'});return respond(path,options);};
 const router=routing.createRouter({history:routing.createMemoryHistory(),routes:[{path:'/',redirect:'/connect'},{path:'/connect',component:View},
  {path:'/login',component:{render:()=>vue.h('h1','登录')}},{path:'/dashboard',component:{render:()=>vue.h('h1','工作台')}}]});
 api.registerUnauthorizedHandler(()=>{auth.clearLocal();void router.replace('/login');});
 app=vue.createApp({render:()=>vue.h(routing.RouterView)});app.use(pinia);app.use(i18n);app.use(router);await router.push('/connect');await router.isReady();
 const root=browser.document.createElement('div');browser.document.body.append(root);app.mount(root);await flush();return{root,router};
}
/** :param root: 连接页。:param value: 假目标。:returns: 原生输入的实际响应式更新。 */
async function fill(root,value='http://next.invalid'){
 const input=root.querySelector('input[aria-label="ControlHost 地址"]');assert.ok(input);input.value=value;input.dispatchEvent(new browser.Event('input',{bubbles:true}));await flush();
}
/** :param root: 当前连接页。:returns: 实际Ant连接提交按钮。 */
function connectButton(root){const button=[...root.querySelectorAll('button')].find(node=>node.type==='submit');assert.ok(button);return button;}

test('当前连接一次提交完成三个认证阶段，目标明确且不冒充播放器在线',async()=>{
 const{root}=await mount();await fill(root);const button=connectButton(root);button.click();button.click();await flush();await flush();
 assert.deepEqual(requests.map(item=>item.path),['/api/auth/logout/','/api/auth/csrf/','/api/auth/status/']);
 assert.equal(runtime.serverProfile.origin,'http://next.invalid');assert.match(root.textContent,/等待登录|前往登录/);assert.equal(auth.user,null);assert.equal(button.disabled,false);
});
for(const stage of ['logout','csrf','status'])test(`连接${stage}在途离页后不续读或导航`,async()=>{
 const{root,router}=await mount();await fill(root);const hold=gate(),fallback=respond;const path=`/api/auth/${stage}/`;
 respond=(next,options)=>next===path?hold.promise:fallback(next,options);connectButton(root).click();await flush();await flush();const count=requests.length;
 await router.push('/login');hold.resolve(stage==='status'?json({authenticated:true,user:{id:2,username:'memory-old'}}):response(path));await flush();await flush();
 assert.equal(router.currentRoute.value.path,'/login');assert.equal(requests.length,count);assert.equal(auth.user,null);
});
test('离开旧CSRF探测后，新初始化不等待旧探测或借用旧结果',async()=>{
 const{root,router}=await mount();await fill(root);const hold=gate(),fallback=respond;let first=true;
 respond=(path,options)=>{if(path==='/api/auth/csrf/'&&first){first=false;return hold.promise;}return fallback(path,options);};
 connectButton(root).click();await flush();await flush();await router.push('/login');const initialized=auth.ensureInitialized();await flush();
 const csrfCount=requests.filter(item=>item.path==='/api/auth/csrf/').length;hold.resolve(response('/api/auth/csrf/'));await initialized;await flush();
 assert.equal(csrfCount,2);assert.equal(auth.initialized,true);assert.equal(auth.user,null);
});
test('当前探测失败保持原说明，恢复后只经显式重试，不自动再发',async()=>{
 const{root}=await mount();await fill(root);const fallback=respond;let failed=true;
 respond=(path,options)=>path==='/api/auth/status/'&&failed?Promise.resolve(json({detail:'current-probe-failed'},503)):fallback(path,options);
 connectButton(root).click();await flush();await flush();assert.match(root.textContent,/current-probe-failed/);assert.equal(requests.length,3);
 failed=false;connectButton(root).click();await flush();await flush();assert.equal(requests.length,6);assert.doesNotMatch(root.textContent,/current-probe-failed/);
});
test('当前保存失败显示已切换的实际地址，不继续探测或报就绪',async()=>{
 const{root}=await mount();await fill(root);browser.scpCvElectron={clearSession:async()=>{},saveServerProfile:async()=>{throw new Error('current-save-failed');}};
 connectButton(root).click();await flush();await flush();assert.match(root.textContent,/current-save-failed/);assert.equal(requests.length,1);assert.equal(runtime.serverProfile.origin,'http://next.invalid');
 assert.doesNotMatch(root.textContent,/ControlHost 不可达/,'尚未探测不能把配置保存失败写成主机离线');assert.match(root.textContent,/尚未检测/);
});
test('无效地址零副作用，原已连接状态不会被表单失败误报离线',async()=>{
 const{root}=await mount();runtime.sseStatus='connected';await fill(root,'ftp://invalid');connectButton(root).click();await flush();
 assert.equal(requests.length,0);assert.equal(runtime.serverProfile.origin,'http://initial.invalid');assert.match(root.textContent,/控制链路已连接/);assert.doesNotMatch(root.textContent,/ControlHost 不可达/);
});
test('同页面路由意图改变后可重试，旧CSRF完成不解锁新切换',async()=>{
 const{root,router}=await mount();await fill(root);const old=gate(),latest=gate(),fallback=respond;let firstCsrf=true,holdLogout=false;
 respond=(path,options)=>{if(path==='/api/auth/csrf/'&&firstCsrf){firstCsrf=false;return old.promise;}if(path==='/api/auth/logout/'&&holdLogout)return latest.promise;return fallback(path,options);};
 connectButton(root).click();await flush();await flush();await router.push('/connect?attempt=new');await flush();
 assert.equal(connectButton(root).disabled,false);await fill(root,'http://latest.invalid');holdLogout=true;connectButton(root).click();await flush();
 old.resolve(response('/api/auth/csrf/'));await flush();assert.equal(connectButton(root).disabled,true);assert.doesNotMatch(root.textContent,/旧响应已丢弃/);
 latest.resolve(response('/api/auth/logout/'));await flush();await flush();assert.equal(runtime.serverProfile.origin,'http://latest.invalid');assert.equal(connectButton(root).disabled,false);
});
test('新认证在旧退出等待中成立，不被旧连接页清除或显示旧错误',async()=>{
 const{root,router}=await mount();await fill(root);const hold=gate(),fallback=respond;respond=(path,options)=>path==='/api/auth/logout/'?hold.promise:fallback(path,options);
 connectButton(root).click();await flush();auth.user={id:2,username:'memory-new'};hold.resolve(response('/api/auth/logout/'));await flush();await flush();
 assert.equal(auth.user.id,2);assert.equal(runtime.serverProfile.origin,'http://initial.invalid');assert.equal(router.currentRoute.value.path,'/connect');assert.doesNotMatch(root.textContent,/旧响应已丢弃/);assert.equal(connectButton(root).disabled,false);
});
test('真实初始化路由守卫不会把离页意图挂在旧页面的CSRF探测上',async()=>{
 const{root,router}=await mount();router.beforeEach(async()=>{if(!auth.initialized)await auth.ensureInitialized();});await fill(root);
 const hold=gate(),fallback=respond;let first=true;respond=(path,options)=>{if(path==='/api/auth/csrf/'&&first){first=false;return hold.promise;}return fallback(path,options);};
 connectButton(root).click();await flush();await flush();let navigated=false;const leaving=router.push('/login').then(()=>{navigated=true;});await flush();await flush();
 const independent=navigated;hold.resolve(response('/api/auth/csrf/'));await leaving;await flush();assert.equal(independent,true,'必须先取消旧页面归属，再让全局守卫初始化新页面');assert.equal(router.currentRoute.value.path,'/login');
});
test('当前已登录主机的探测完成仍正常进入工作台',async()=>{
 const{root,router}=await mount();await fill(root);const fallback=respond;respond=(path,options)=>path==='/api/auth/status/'?Promise.resolve(json({authenticated:true,user:{id:3,username:'memory-current'}})):fallback(path,options);
 connectButton(root).click();await flush();await flush();assert.equal(router.currentRoute.value.path,'/dashboard');assert.equal(auth.user.id,3);assert.equal(requests.length,3);
});
