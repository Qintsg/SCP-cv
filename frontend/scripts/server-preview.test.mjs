/** 正式预览组件的租约／可见性／归属回归；假媒体边界不是原生浏览器解码证明。 */
import assert from 'node:assert/strict';
import test,{before,after,afterEach} from 'node:test';
import {setImmediate as turn,setTimeout as delay} from 'node:timers/promises';
import {Window} from 'happy-dom';
import {createDomTestServer} from './dom-test-server.mjs';
const browser=new Window({url:'https://console.invalid/dashboard'});
const names=['window','document','navigator','HTMLElement','HTMLInputElement','Element','Node','SVGElement','MutationObserver','ResizeObserver','getComputedStyle','requestAnimationFrame','cancelAnimationFrame','sessionStorage'];
const descriptors=new Map(names.map(name=>[name,Object.getOwnPropertyDescriptor(globalThis,name)]));
for(const name of names)Object.defineProperty(globalThis,name,{configurable:true,writable:true,value:typeof browser[name]==='function'&&/^[a-z]/.test(name)?browser[name].bind(browser):browser[name]});
const previousFetch=globalThis.fetch;
const previousObserver=Object.getOwnPropertyDescriptor(globalThis,'IntersectionObserver');
let server,vue,piniaLib,Component,targets,playerModule,poolModule,transportModule,useAuth,app,calls,players,props,auth,pool;
before(async()=>{vue=await import('vue');piniaLib=await import('pinia');server=await createDomTestServer();
  ({default:Component}=await server.ssrLoadModule('/src/features/preview/ClientStreamPreview.vue'));
  targets=await server.ssrLoadModule('/src/features/preview/previewTarget.ts');playerModule=await server.ssrLoadModule('/src/features/preview/previewPlayer.ts');
  poolModule=await server.ssrLoadModule('/src/features/preview/previewPool.ts');transportModule=await server.ssrLoadModule('/src/features/preview/previewTransport.ts');
  ({useAuthStore:useAuth}=await server.ssrLoadModule('/src/stores/auth.ts'));
});
afterEach(async()=>{app?.unmount();app=undefined;pool?.dispose();await flush();browser.document.body.replaceChildren();await browser.happyDOM.abort();globalThis.fetch=previousFetch;delete browser.document.hidden;if(previousObserver)Object.defineProperty(globalThis,'IntersectionObserver',previousObserver);else delete globalThis.IntersectionObserver;});
after(async()=>{await server.close();await browser.happyDOM.close();for(const[name,value]of descriptors){if(value)Object.defineProperty(globalThis,name,value);else delete globalThis[name];}});
/** :returns: 实际异步微任务，不等待模拟用户输入。 */
async function flush(){for(let i=0;i<8;i++){await vue.nextTick();await turn();}}
/** :param input: 来源身份。:param handler: 内存HTTP。:param options: 假媒体行为。:returns: 挂正式组件与正式请求封装。 */
async function mount(input,handler,options={}){
  calls=[];players=[];const pinia=piniaLib.createPinia();auth=useAuth(pinia);auth.user=options.anonymous?null:{id:1,username:'alice'};
  let serial=0;globalThis.fetch=async(url,init={})=>{
    const parsed=new URL(String(url),browser.location.href);assert.equal(parsed.origin,'https://console.invalid');
    calls.push({path:parsed.pathname,init});
    if(handler)return handler(parsed.pathname,init);
    return init.method==='POST'?new Response(JSON.stringify({id:`00000000-0000-0000-0000-${String(++serial).padStart(12,'0')}`,
      sharing_key:(JSON.parse(init.body||'{}').input?.kind==='window_2'?'b':'a').repeat(64),expires_at:new Date(Date.now()+30000).toISOString()}),{headers:{'Content-Type':'application/json'}}):new Response(null,{status:204});
  };
  props=vue.reactive({input,quality:'low'});const host=browser.document.createElement('div');browser.document.body.append(host);
  app=vue.createApp({render:()=>vue.h(Component,props)});app.use(pinia);
  const factory=async(video,url,frame,failed)=>{
    const player={url,frame,failed,destroyed:false,destroy(){this.destroyed=true;}};players.push(player);
    if(options.frame!==false)frame();return player;
  };
  pool=new poolModule.PreviewPool(factory,transportModule.createPreviewTransport(),{idleGrace:1});
  app.provide(poolModule.PREVIEW_POOL,pool);app.provide(playerModule.PREVIEW_PLAYER_FACTORY,factory);app.mount(host);await flush();
}
test('仅提交来源身份与清晰度，3/4已登记，媒体ID与输入互斥',()=>{
  assert.deepEqual(targets.previewTarget({kind:'window_1'},undefined,'low'),{input:{kind:'window_1'},quality:'low'});
  assert.deepEqual(targets.previewTarget(undefined,7,'high'),{source_id:7,quality:'high'});
  for(const kind of ['window_3','window_4'])assert.deepEqual(targets.previewTarget({kind},undefined,'low'),{input:{kind},quality:'low'});
  for(const input of [undefined,{kind:'ip_stream',ip_address:'bad'}])assert.equal(targets.previewTarget(input,undefined,'low'),undefined);
  assert.equal(targets.previewTarget({kind:'window_1'},7,'low'),undefined);
});
test('无流与匿名不申请租约、不报错',async()=>{
  await mount(undefined);assert.equal(browser.document.querySelector('figure').dataset.previewState,'empty');assert.equal(browser.document.querySelector('figcaption'),null);assert.deepEqual(calls,[]);
});
test('未登录时不会用可见卡片启动拉流',async()=>{await mount({kind:'window_1'},undefined,{anonymous:true});assert.deepEqual(calls,[]);assert.deepEqual(players,[]);});
test('无需本机令牌即可向Host申请，媒体工厂只得到随机会话流地址，卸载释放',async()=>{
  await mount({kind:'window_1'});assert.equal(players.length,1);assert.match(players[0].url,/^https:\/\/console\.invalid\/api\/stream-previews\/[^/]+\/stream.flv$/);
  assert.deepEqual(JSON.parse(calls[0].init.body),{input:{kind:'window_1'},quality:'low'});
  assert.equal(browser.document.querySelector('video').muted,true);assert.equal(browser.document.querySelector('figcaption'),null);
  app.unmount();app=undefined;await delay(10);await flush();assert.equal(players[0].destroyed,true);assert.equal(calls.filter(call=>call.init.method==='DELETE').length,2);
});
test('连接失败只红色细边/辅助状态，不泄露原始异常，不弹窗',async()=>{
  await mount({kind:'window_1'},()=>Promise.reject(Error('fixture-secret@source')));
  assert.equal(browser.document.querySelector('figure').dataset.previewState,'unavailable');assert.equal(browser.document.querySelectorAll('figcaption').length,0);
  assert.doesNotMatch(browser.document.body.textContent,/fixture-secret/);assert.equal(browser.document.querySelector('[role="alert"],[role="dialog"],.ant-notification'),null);
});
test('服务端明确无预览流时是空态而非错误，也不启动播放器',async()=>{
  await mount({kind:'ip_stream',ip_address:'192.0.2.1'},()=>Promise.resolve(new Response(JSON.stringify({code:'preview_no_stream',detail:'无流'}),{status:422,headers:{'Content-Type':'application/json'}})));
  assert.match(browser.document.body.textContent,/暂无预览流/);assert.equal(browser.document.querySelector('figcaption'),null);assert.equal(players.length,0);
});
test('来源改变后销毁旧播放器与租约，旧帧／错误不能覆盖新连接',async()=>{
  await mount({kind:'window_1'});const old=players[0];props.input={kind:'window_2'};await flush();await delay(10);
  assert.equal(old.destroyed,true);assert.equal(players.length,2);old.failed();await flush();
  assert.equal(players[1].destroyed,false);assert.equal(calls.filter(call=>call.init.method==='POST').length,4);
});
test('同ID换登录对象后不向新归属发送旧DELETE，迟到创建不消费',async()=>{
  let resolve;const pending=new Promise(done=>{resolve=done;});
  await mount({kind:'window_1'},()=>pending);auth.user={id:1,username:'alice'};await flush();
  app.unmount();app=undefined;resolve(new Response(JSON.stringify({id:'00000000-0000-0000-0000-000000000099'})));await flush();
  assert.equal(players.length,0);assert.equal(calls.some(call=>call.init.method==='DELETE'),false);
});
test('页面隐藏立即释放，恢复可见才重新申请，不持续后台拉流',async()=>{
  await mount({kind:'window_1'});const old=players[0];Object.defineProperty(browser.document,'hidden',{configurable:true,value:true});
  browser.document.dispatchEvent(new browser.Event('visibilitychange'));await flush();assert.equal(old.destroyed,true);assert.equal(calls.filter(call=>call.init.method==='DELETE').length,2);
  Object.defineProperty(browser.document,'hidden',{configurable:true,value:false});browser.document.dispatchEvent(new browser.Event('visibilitychange'));await flush();assert.equal(players.length,2);
});
test('离屏不订阅，相同相交通知不重开，重新进入才恢复',async()=>{
  let notify;globalThis.IntersectionObserver=class{constructor(callback){notify=callback;}observe(){}disconnect(){}};
  await mount({kind:'window_1'});assert.equal(calls.length,0);
  notify([{isIntersecting:true}]);await flush();assert.equal(players.length,1);
  notify([{isIntersecting:true}]);await flush();assert.equal(players.length,1);
  notify([{isIntersecting:false}]);await flush();await delay(10);assert.equal(players[0].destroyed,true);
  notify([{isIntersecting:true}]);await flush();assert.equal(players.length,2);
});

test('只修改展示比例/标签/quality提示不重开，连接状态没有常驻figcaption',async()=>{
  await mount({kind:'window_1'});props.quality='high';props.label='修改后的区域';await flush();
  assert.equal(players.length,1);assert.equal(calls.filter(call=>call.init.method==='POST').length,2);
  assert.equal(browser.document.querySelector('figure').dataset.previewState,'live');assert.equal(browser.document.querySelector('figcaption'),null);
  assert.match(browser.document.querySelector('figure').getAttribute('aria-label'),/修改后的区域/);
});
