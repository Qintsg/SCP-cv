/** 退出完成归属：真实Auth/API/Ant与内存路由，HTTP只在内存延迟，不操作真实账号。 */
import assert from 'node:assert/strict';
import test, { before, after, afterEach } from 'node:test';
import { setImmediate as nextTurn } from 'node:timers/promises';
import { Window } from 'happy-dom';
import { createDomTestServer } from './dom-test-server.mjs';

const browser = new Window({ url: 'http://localhost:5173/settings' });
const names = ['window','document','navigator','history','location','HTMLElement','HTMLInputElement','HTMLTextAreaElement',
  'Element','Node','SVGElement','Document','MutationObserver','ResizeObserver','Image','InputEvent','KeyboardEvent',
  'getComputedStyle','requestAnimationFrame','cancelAnimationFrame'];
const globals = new Map(names.map(name => [name, Object.getOwnPropertyDescriptor(globalThis, name)]));
for (const name of names) Object.defineProperty(globalThis, name, { configurable: true, writable: true,
  value: typeof browser[name] === 'function' && /^[a-z]/.test(name) ? browser[name].bind(browser) : browser[name] });
const originalFetch = globalThis.fetch;
let server, vue, createPinia, routing, i18n, Account, useAuth, useRuntime, useToasts, api, client, app, auth, requests=[];

before(async () => {
  [vue, { createPinia }, routing, server] = await Promise.all([import('vue'), import('pinia'), import('vue-router'), createDomTestServer()]);
  ({ i18n } = await server.ssrLoadModule('/src/locales/index.ts'));
  ({ default: Account } = await server.ssrLoadModule('/src/features/settings/tabs/AccountSettingsTab.vue'));
  ({ useAuthStore: useAuth } = await server.ssrLoadModule('/src/stores/auth.ts'));
  ({ useRuntimeStore: useRuntime } = await server.ssrLoadModule('/src/stores/runtime.ts'));
  ({ useToastStore: useToasts } = await server.ssrLoadModule('/src/composables/useToast.ts'));
  api = await server.ssrLoadModule('/src/services/api.ts');
  ({ clientConnection: client } = await server.ssrLoadModule('/src/platform/connection.ts'));
});
/** :returns: 网络、响应式及路由导航完成，门闩由用例明确释放。 */
async function flush() { await nextTurn(); await vue.nextTick(); await nextTurn(); await vue.nextTick(); }
/** :param detail: 假HTTP说明。:param status: 状态。:returns: 正式JSON解析路径可读的响应。 */
function response(detail='ok', status=200) { return new Response(JSON.stringify({ detail }), { status, headers: { 'Content-Type':'application/json' } }); }
/** :param id: 虚构用户。:returns: 无真实凭据的身份。 */
function user(id=1) { return { id, username:'memory-'+id, is_staff:true, is_superuser:true }; }

afterEach(async () => {
  api.registerUnauthorizedHandler(null); app?.unmount(); app=undefined;
  for (const request of requests) request.resolve(response());
  await flush(); auth?.$dispose(); browser.document.body.replaceChildren();
  await browser.happyDOM.abort(); globalThis.fetch=originalFetch;
});
after(async () => {
  await server.close(); await browser.happyDOM.close();
  for (const [name, value] of globals) { if (value) Object.defineProperty(globalThis,name,value); else delete globalThis[name]; }
});

/** :returns: 真实路由卸载、原组件及Pinia；没有用假Auth绕过自身清身份。 */
async function mount() {
  requests=[];
  globalThis.fetch=(input, options={}) => new Promise((resolve,reject) => {
    const path=new URL(String(input),browser.location.href).pathname;
    assert.equal(path,'/api/auth/logout/');
    requests.push({ resolve,reject,path,credentials:options.credentials,method:options.method });
  });
  const pinia=createPinia(); auth=useAuth(pinia); auth.user=user(); auth.initialized=true;
  api.configureApiServer({origin:browser.location.origin,displayName:'内存测试',allowInsecureDevelopment:true}); api.clearApiSessionState();
  const router=routing.createRouter({history:routing.createMemoryHistory(),routes:[
    {path:'/settings',component:Account},{path:'/login',component:{render:()=>vue.h('h1','登录')}},
    {path:'/elsewhere',component:{render:()=>vue.h('h1','其它页面')}},
  ]});
  api.registerUnauthorizedHandler(() => { auth.clearLocal(); void router.replace('/login'); });
  app=vue.createApp({render:()=>vue.h(routing.RouterView)}); app.use(pinia); app.use(i18n); app.use(router);
  await router.push('/settings'); await router.isReady();
  const root=browser.document.createElement('div'); browser.document.body.append(root); app.mount(root); await flush();
  return { root,router,toasts:useToasts(pinia),runtime:useRuntime(pinia) };
}
/** :param root: 当前账户页。:returns: 真实Ant退出按钮。 */
function exitButton(root) { const button=[...root.querySelectorAll('button')].find(node=>node.textContent.includes('退出登录')); assert.ok(button); return button; }

test('正常退出单次POST，自己清身份后仍反馈并进入登录页',async()=>{
  const {root,router,toasts}=await mount();const button=exitButton(root);button.click();button.click();await flush();
  assert.equal(requests.length,1);assert.equal(button.disabled,true);assert.equal(requests[0].credentials,'include');
  requests[0].resolve(response());await flush();
  assert.equal(auth.user,null);assert.equal(router.currentRoute.value.path,'/login');
  assert.equal(toasts.items.length,1);assert.equal(toasts.items[0].level,'success');
});
for (const failure of ['503','network']) test(`当前退出${failure}清页面状态但不谎称服务器已退出`,async()=>{
  const {root,router,toasts}=await mount();exitButton(root).click();await flush();
  if (failure==='503') requests[0].resolve(response('测试服务端退出暂不可用',503)); else requests[0].reject(new Error('memory-network'));
  await flush();assert.equal(auth.user,null);assert.equal(router.currentRoute.value.path,'/login');
  assert.equal(requests.length,1);assert.equal(toasts.items.length,1);assert.equal(toasts.items[0].level,'warning');
  assert.equal(toasts.items[0].duration,0);assert.match(toasts.items[0].description,/服务器上的登录可能仍然有效/);
  if(failure==='503')assert.match(toasts.items[0].description,/测试服务端退出暂不可用/);
  assert.ok(!toasts.items.some(item=>item.message==='已退出登录'));
});
test('当前401沿用全局退出，不串额外退出失败或成功提示',async()=>{
  const {root,router,toasts}=await mount();exitButton(root).click();await flush();requests[0].resolve(response('未登录',401));await flush();
  assert.equal(auth.user,null);assert.equal(router.currentRoute.value.path,'/login');assert.equal(toasts.items.length,0);
});
for (const kind of ['route','query','host','runtime-projection','account','same-id','reset']) for (const status of [200,503]) {
  test(`退出在途${kind}后迟到${status}不串提示或导航`,async()=>{
    const {root,router,toasts,runtime}=await mount();exitButton(root).click();await flush();
    if(kind==='route')await router.push('/elsewhere');
    if(kind==='query')await router.push('/settings?view=changed');
    if(kind==='host'){runtime.connectionGeneration++;client.restoreProfile({origin:'http://isolated.invalid',displayName:'新内存主机',allowInsecureDevelopment:true});}
    if(kind==='runtime-projection')runtime.connectionGeneration++;
    if(kind==='account')auth.user=user(2);
    if(kind==='same-id')auth.user=user(1);
    if(kind==='reset')auth.$reset();
    await flush();requests[0].resolve(response('旧退出',status));await flush();
    assert.equal(router.currentRoute.value.path,kind==='route'?'/elsewhere':'/settings');assert.equal(toasts.items.length,0);
    if(kind==='query')assert.equal(router.currentRoute.value.query.view,'changed');
    if(kind==='account')assert.equal(auth.user.id,2);
    if(kind==='same-id')assert.equal(auth.user.id,1);
    assert.equal(requests.length,1);
  });
}
test('换账号可开始新退出，旧finally不能解锁新请求',async()=>{
  const {root,router,toasts}=await mount();exitButton(root).click();await flush();auth.user=user(2);await flush();
  assert.equal(exitButton(root).disabled,false);exitButton(root).click();await flush();assert.equal(requests.length,2);
  requests[0].resolve(response());await flush();assert.equal(exitButton(root).disabled,true);assert.equal(toasts.items.length,0);assert.equal(auth.user.id,2);
  requests[1].resolve(response());await flush();assert.equal(router.currentRoute.value.path,'/login');assert.equal(toasts.items.length,1);
});
test('退出完成凭据在后续同ID身份、清理及新退出意图之后立即失效',async()=>{
  await mount();let pending=auth.logout();requests[0].resolve(response());const receipt=await pending;
  assert.equal(receipt.remoteSignedOut,true);assert.equal(receipt.isCurrent(),true);
  auth.user=user(1);assert.equal(receipt.isCurrent(),false);
  pending=auth.logout();requests[1].resolve(response());const second=await pending;assert.equal(second.isCurrent(),true);
  const next=auth.logout();assert.equal(second.isCurrent(),false);requests[2].resolve(response());await next;
});
