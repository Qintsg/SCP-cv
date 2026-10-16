/** 登录意图与HTTP完成归属：真实Ant/Auth/API/路由，所有凭据与网络均为内存测试数据。 */
import assert from 'node:assert/strict';
import test,{before,after,afterEach}from'node:test';
import {setImmediate as nextTurn}from'node:timers/promises';
import {Window}from'happy-dom';
import {createDomTestServer}from'./dom-test-server.mjs';
const browser=new Window({url:'http://localhost:5173/login'});
const names=['window','document','navigator','history','location','HTMLElement','HTMLInputElement','HTMLTextAreaElement','Element','Node','SVGElement',
 'Document','MutationObserver','ResizeObserver','Image','InputEvent','KeyboardEvent','getComputedStyle','requestAnimationFrame','cancelAnimationFrame'];
const globals=new Map(names.map(name=>[name,Object.getOwnPropertyDescriptor(globalThis,name)]));
for(const name of names)Object.defineProperty(globalThis,name,{configurable:true,writable:true,value:typeof browser[name]==='function'&&/^[a-z]/.test(name)?browser[name].bind(browser):browser[name]});
const originalFetch=globalThis.fetch;
let server,vue,createPinia,routing,i18n,View,useAuth,useRuntime,useToasts,api,client,auth,runtime,app,requests,respond,gates,running,unauthorized;
before(async()=>{
 [vue,{createPinia},routing,server]=await Promise.all([import('vue'),import('pinia'),import('vue-router'),createDomTestServer()]);
 ({i18n}=await server.ssrLoadModule('/src/locales/index.ts'));({default:View}=await server.ssrLoadModule('/src/features/auth/LoginView.vue'));
 ({useAuthStore:useAuth}=await server.ssrLoadModule('/src/stores/auth.ts'));({useRuntimeStore:useRuntime}=await server.ssrLoadModule('/src/stores/runtime.ts'));
 ({useToastStore:useToasts}=await server.ssrLoadModule('/src/composables/useToast.ts'));api=await server.ssrLoadModule('/src/services/api.ts');
 ({clientConnection:client}=await server.ssrLoadModule('/src/platform/connection.ts'));
});
/** :returns: 真实表单/路由响应式队列结束，不等待未释放的HTTP门闩。 */
async function flush(){await nextTurn();await vue.nextTick();await nextTurn();await vue.nextTick();}
/** :param data: 假数据。:param status: HTTP状态。:returns: 真实解析器输入。 */
function json(data,status=200){return new Response(JSON.stringify(data),{status,headers:{'Content-Type':'application/json'}});}
/** :param id: 虚构ID。:returns: 无真实凭据的身份。 */
function user(id=1){return{id,username:'memory-'+id,is_staff:true,is_superuser:true};}
/** :returns: 必在清理时释放的受控响应。 */
function gate(){let resolve;const promise=new Promise(done=>{resolve=done;});const value={promise,resolve};gates.push(value);return value;}
/** :param value: 在途认证。:returns: 立即接拒绝的最终结果。 */
function run(value){const result=value.then(value=>({value}),error=>({error}));running.push(result);return result;}
afterEach(async()=>{
 app?.unmount();app=undefined;for(const item of gates??[])item.resolve(json({csrfToken:'cleanup-only',user:user()}));await Promise.all(running??[]);await flush();await flush();
 api.registerUnauthorizedHandler(null);runtime?.disconnectEvents();auth?.$dispose();browser.document.body.replaceChildren();await browser.happyDOM.abort();globalThis.fetch=originalFetch;
});
after(async()=>{await server.close();await browser.happyDOM.close();for(const[name,value]of globals){if(value)Object.defineProperty(globalThis,name,value);else delete globalThis[name];}});
/** :param redirect: 初始目的地。:returns: 原登录组件、原生输入和真实路由。 */
async function mount(redirect='/sources'){
 gates=[];running=[];requests=[];unauthorized=0;const pinia=createPinia();auth=useAuth(pinia);auth.user=null;auth.initialized=true;
 api.configureApiServer({origin:browser.location.origin,displayName:'内存主机',allowInsecureDevelopment:true});api.clearApiSessionState();runtime=useRuntime(pinia);
 respond=async path=>json(path==='/api/auth/csrf/'?{csrfToken:'memory-current-token'}:path==='/api/auth/status/'?{authenticated:false,user:null}:{user:user()});
 globalThis.fetch=(url,options={})=>{const path=new URL(String(url),browser.location.href).pathname;requests.push({path,url:String(url),body:options.body&&JSON.parse(options.body),credentials:options.credentials,headers:options.headers});return respond(path,options);};
 const router=routing.createRouter({history:routing.createMemoryHistory(),routes:[
  {path:'/',redirect:'/login'},{path:'/login',component:View},
  ...['connect','sources','dashboard'].map(path=>({path:'/'+path,component:{render:()=>vue.h('h1',path)}})),
 ]});
 api.registerUnauthorizedHandler(()=>{unauthorized++;auth.clearLocal();const current=router.currentRoute.value;if(current.path!=='/login')void router.push({path:'/login',query:{redirect:current.fullPath}});});
 app=vue.createApp({render:()=>vue.h(routing.RouterView)});app.use(pinia);app.use(i18n);app.use(router);await router.push({path:'/login',query:{redirect}});await router.isReady();
 const root=browser.document.createElement('div');browser.document.body.append(root);app.mount(root);await flush();return{root,router,toasts:useToasts(pinia)};
}
/** :param root: 登录页。:param name: 假用户名。:param secret: 假密码。:returns: 原生输入已更新。 */
async function fill(root,name='  memory-input  ',secret='Memory-Only-123'){
 for(const[label,value]of[['用户名',name],['密码',secret]]){const input=root.querySelector(`input[aria-label="${label}"]`);assert.ok(input);input.value=value;input.dispatchEvent(new browser.Event('input',{bubbles:true}));}await flush();
}
/** :param root: 登录页。:returns: 原生表单提交，覆盖同一渲染轮多次回车。 */
function submit(root){root.querySelector('form').dispatchEvent(new browser.Event('submit',{bubbles:true,cancelable:true}));}

test('当前成功保留凭据合同、一次提示并进入原目的地',async()=>{
 const{root,router,toasts}=await mount();await fill(root);submit(root);await flush();await flush();
 assert.equal(router.currentRoute.value.path,'/sources');assert.equal(auth.user.id,1);assert.equal(toasts.items.length,1);assert.equal(toasts.items[0].level,'success');
 assert.equal(requests.length,2);assert.deepEqual(requests[1].body,{username:'memory-input',password:'Memory-Only-123'});assert.equal(requests[1].credentials,'include');assert.equal(requests[1].headers['X-CSRFToken'],'memory-current-token');
});
test('同渲染轮重复提交只启动一个CSRF与密码链路',async()=>{
 const{root}=await mount();await fill(root);const hold=gate(),fallback=respond;respond=(path,options)=>path==='/api/auth/csrf/'?hold.promise:fallback(path,options);
 submit(root);submit(root);await flush();const count=requests.length;hold.resolve(json({csrfToken:'memory-current-token'}));await flush();await flush();
 assert.equal(count,1);assert.equal(requests.filter(item=>item.path==='/api/auth/login/').length,1);
});
for(const status of [401,503])test(`当前密码请求${status}保留原说明/输入关联及显式重试`,async()=>{
 const{root,router,toasts}=await mount();await fill(root);const fallback=respond;let failed=true;
 respond=(path,options)=>path==='/api/auth/login/'&&failed?Promise.resolve(json({detail:'current-login-failed-'+status},status)):fallback(path,options);
 submit(root);await flush();await flush();assert.equal(router.currentRoute.value.path,'/login');assert.equal(requests.length,2);assert.match(root.textContent,new RegExp('current-login-failed-'+status));
 const input=root.querySelector('input[aria-label="密码"]'),description=input.getAttribute('aria-describedby');assert.ok(description);assert.match(browser.document.getElementById(description).textContent,/current-login-failed/);
 assert.equal(input.disabled,false);assert.equal(toasts.items.length,0);if(status===401)assert.equal(unauthorized,1);
 failed=false;submit(root);await flush();await flush();assert.equal(router.currentRoute.value.path,'/sources');assert.equal(requests.length,4);
});
for(const stage of ['csrf','login'])for(const kind of ['route','host','account','same-id','query'])for(const status of [200,401,503]){
 test(`${stage}在途${kind}后迟到${status}不串全局401、提示、导航或凭据`,async()=>{
  const{root,router,toasts}=await mount();await fill(root);const hold=gate(),fallback=respond;respond=(path,options)=>path===`/api/auth/${stage}/`?hold.promise:fallback(path,options);
  submit(root);await flush();await flush();
  if(kind==='route')await router.push('/connect');
  if(kind==='host')client.restoreProfile({origin:'http://isolated.invalid',displayName:'新内存主机'});
  if(kind==='account')auth.user=user(2);
  if(kind==='same-id')auth.user={...user(1),username:'new-memory-one'};
  if(kind==='query')await router.push('/login?redirect=/dashboard');
  await flush();hold.resolve(json(status===200?(stage==='csrf'?{csrfToken:'obsolete-token'}:{user:user()}):{detail:'obsolete-login-error'},status));await flush();await flush();
  assert.equal(router.currentRoute.value.path,kind==='route'?'/connect':'/login');assert.equal(toasts.items.length,0);assert.equal(unauthorized,0);
  assert.equal(requests.filter(item=>item.path==='/api/auth/login/').length,stage==='csrf'?0:1);
  if(kind==='account')assert.equal(auth.user.id,2);else if(kind==='same-id')assert.equal(auth.user.username,'new-memory-one');else assert.equal(auth.user,null);
  if(kind!=='route'){assert.equal(root.querySelector('input[aria-label="密码"]').value,'');assert.doesNotMatch(root.textContent,/obsolete-login-error|旧响应已丢弃/);assert.equal(root.querySelector('button[type="submit"]').disabled,false);}
 });
}
test('主机投影地址在代次未变时提交，也会清旧输入并停止旧链路',async()=>{
 const{root}=await mount();await fill(root);const hold=gate(),fallback=respond;respond=(path,options)=>path==='/api/auth/csrf/'?hold.promise:fallback(path,options);
 submit(root);await flush();runtime.serverProfile={origin:'http://next.invalid',displayName:'新投影'};await flush();hold.resolve(json({csrfToken:'obsolete'}));await flush();await flush();
 assert.equal(requests.length,1);assert.equal(root.querySelector('input[aria-label="密码"]').value,'');
});
test('旧finally不能解锁或清空新主机上的新提交',async()=>{
 const{root,router,toasts}=await mount();await fill(root);const first=gate(),second=gate(),fallback=respond;let posts=0;
 respond=(path,options)=>path==='/api/auth/login/'?(++posts===1?first.promise:second.promise):fallback(path,options);
 submit(root);await flush();client.restoreProfile({origin:'http://latest.invalid',displayName:'新主机'});runtime.connectionGeneration=client.captureGeneration();runtime.serverProfile=client.profile;await flush();
 assert.equal(root.querySelector('button[type="submit"]').disabled,false);await fill(root,'new-input','New-Memory-123');submit(root);await flush();
 first.resolve(json({detail:'obsolete-login-error'},401));await flush();assert.equal(root.querySelector('button[type="submit"]').disabled,true);assert.equal(root.querySelector('input[aria-label="密码"]').value,'New-Memory-123');assert.equal(toasts.items.length,0);
 second.resolve(json({user:user(2)}));await flush();await flush();assert.equal(router.currentRoute.value.path,'/sources');assert.equal(auth.user.id,2);assert.equal(posts,2);
});
test('Auth已提交但UI未恢复时新认证成立，旧完成凭据不再提示或导航',async()=>{
 const{root,router,toasts}=await mount();await fill(root);const remove=auth.$onAction(({name,after})=>{if(name==='login')after(()=>{auth.user=user(2);});});
 try{submit(root);await flush();await flush();assert.equal(router.currentRoute.value.path,'/login');assert.equal(toasts.items.length,0);assert.equal(auth.user.id,2);assert.equal(root.querySelector('input[aria-label="密码"]').value,'');}finally{remove();}
});
test('进入前调用已取消，Auth不清身份或派发网络',async()=>{
 await mount();auth.user=user(2);const result=await run(auth.login('memory','memory-only',{canContinue:()=>false}));assert.ok(result.error);assert.equal(requests.length,0);assert.equal(auth.user.id,2);
});
test('登录保留AuthUser返回值并冻结完成回调，凭据识别后续同ID认证',async()=>{
 await mount();const hold=gate(),fallback=respond;respond=(path,options)=>path==='/api/auth/csrf/'?hold.promise:fallback(path,options);let original=0,replaced=0,owns;
 const options={onCommitted:current=>{original++;owns=current;}};const operation=run(auth.login('memory','memory-only',options));options.onCommitted=()=>{replaced++;};hold.resolve(json({csrfToken:'current'}));const result=await operation;
 assert.equal(result.value.id,1);assert.equal(original,1);assert.equal(replaced,0);assert.equal(owns(),true);auth.user={...user(1),username:'another-memory-one'};assert.equal(owns(),false);
});
test('当前CSRF读取失败仍保留既有单次登录回退，不自动重放密码',async()=>{
 const{root,router}=await mount();await fill(root);const fallback=respond;respond=(path,options)=>path==='/api/auth/csrf/'?Promise.resolve(json({detail:'current-csrf-unavailable'},503)):fallback(path,options);
 submit(root);await flush();await flush();assert.equal(router.currentRoute.value.path,'/sources');assert.equal(requests.length,2);assert.equal(requests.filter(item=>item.path==='/api/auth/login/').length,1);
});
test('空凭据保持可访问错误且零网络',async()=>{
 const{root}=await mount();submit(root);await flush();const input=root.querySelector('input[aria-label="用户名"]'),description=input.getAttribute('aria-describedby');
 assert.equal(requests.length,0);assert.ok(description);assert.match(browser.document.getElementById(description).textContent,/登录失败/);assert.equal(root.querySelector('button[type="submit"]').disabled,false);
});
test('登录页卸载释放主机订阅，重进页面不会累积监听器',async()=>{
 const original=client.onGenerationChanged;let listeners=0;
 client.onGenerationChanged=function(listener){listeners++;const remove=original.call(this,listener);let attached=true;return()=>{if(attached){attached=false;listeners--;}remove();};};
 try{const{router}=await mount();assert.equal(listeners,1);await router.push('/connect');await flush();assert.equal(listeners,0);await router.push('/login');await flush();assert.equal(listeners,1);app.unmount();app=undefined;assert.equal(listeners,0);}finally{app?.unmount();app=undefined;client.onGenerationChanged=original;}
});
