/** 大屏编排真实Vue/Ant DOM回归；只在fetch边界模拟，绝不向现场发包。 */
import { visibleText } from './dom-test-server.mjs';

import assert from 'node:assert/strict';
import { setImmediate as nextTurn } from 'node:timers/promises';
import test, { before, after, afterEach } from 'node:test';
import { Window } from 'happy-dom';
import { createDomTestServer, waitForDom } from './dom-test-server.mjs';

const browser = new Window({ url: 'http://localhost:5173/dashboard' });
const names = ['window','document','navigator','HTMLElement','HTMLInputElement','HTMLTextAreaElement',
  'Element','Node','SVGElement','Document','MutationObserver','ResizeObserver','Image','InputEvent',
  'getComputedStyle','requestAnimationFrame','cancelAnimationFrame'];
const old = new Map(names.map(name => [name, Object.getOwnPropertyDescriptor(globalThis,name)]));
for (const name of names) Object.defineProperty(globalThis,name,{
  value: typeof browser[name] === 'function' && /^[a-z]/.test(name) ? browser[name].bind(browser) : browser[name],
  configurable:true,writable:true,
});
const originalFetch=globalThis.fetch;
let server,vue,createPinia,disposePinia,i18n,Component,apiModule,app,requests,pinia,useRuntime,useAuth;
before(async()=>{
  [vue,{createPinia,disposePinia},server]=await Promise.all([import('vue'),import('pinia'),createDomTestServer()]);
  ({default:Component}=await server.ssrLoadModule('/src/features/runtime/VideoWallMappingPanel.vue'));
  ({i18n}=await server.ssrLoadModule('/src/locales/index.ts'));
  apiModule=await server.ssrLoadModule('/src/services/api.ts');
  ({useRuntimeStore:useRuntime}=await server.ssrLoadModule('/src/stores/runtime.ts'));
  ({useAuthStore:useAuth}=await server.ssrLoadModule('/src/stores/auth.ts'));
});
afterEach(async()=>{app?.unmount();app=undefined;if(pinia)disposePinia(pinia);browser.document.body.replaceChildren();await browser.happyDOM.abort();globalThis.fetch=originalFetch;});
after(async()=>{await server?.close();await browser.happyDOM.close();for(const[name,d]of old){if(d)Object.defineProperty(globalThis,name,d);else delete globalThis[name];}});
/** :returns: 当前渲染和异步回调完成。 */
async function flush(){await vue.nextTick();await nextTurn();await vue.nextTick();}
/** :param label: 可见文案。:returns: 真实按钮。 */
function button(label){return[...browser.document.querySelectorAll('button')].find(item=>visibleText(item.textContent) === visibleText(label));}
/** :returns: 可手动释放的异步响应。 */
function deferred(){let resolve;const promise=new Promise(done=>{resolve=done;});return{promise,resolve};}
/** :param kinds: 当前已保存布局。:param hooks: 在HTTP边界控制失败和竞态。:returns: 挂载真实组件。 */
async function mount(kinds=['window_1','window_2'],hooks={}){
  apiModule.clearApiSessionState();requests=[];
  let state={draft:{name:'现场布局',can_apply:true,unavailable_reason:'',mappings:kinds.map((kind,index)=>({
    region:kinds.length===1?'fullscreen':index===0?'left':'right',input:{kind,ip_address:''}}))},
    draft_revision:7,applied:null,applied_revision:0,active_preset:'',dispatch_status:'sent_unconfirmed',
    dispatch_detail:'控制帧已写出；尚无与本次操作关联的实体画面确认'};
  const json=(body,status=200)=>new Response(JSON.stringify(body),{status,headers:{'Content-Type':'application/json'}});
  globalThis.fetch=async(url,options={})=>{
    const path=new URL(String(url),browser.location.origin).pathname,method=options.method??'GET';
    const payload=options.body?JSON.parse(options.body):null;requests.push({path,method,payload});
    if(path==='/api/auth/csrf/')return json({csrfToken:'qa-only'});
    if(path==='/api/video-wall/layout/apply/'){
      if(hooks.conflict)return json({detail:'布局已被其它客户端修改',error:'layout_revision_conflict'},409);
      assert.equal(payload.expected_revision,state.draft_revision);
      state={...state,applied:state.draft,dispatch_status:'simulated'};
      return json({success:true,layout:state,runtime:{big_screen_mode:'custom',muted_windows:[],volume_level:100},sessions:[]});
    }
    assert.equal(path,'/api/video-wall/layout/');
    if(method==='PUT'){
      await hooks.beforeSave?.();
      if(hooks.saveFail)return json({detail:'预检失败'},503);
      state={...state,draft_revision:state.draft_revision+1,draft:{...payload,can_apply:!hooks.unavailable,unavailable_reason:hooks.unavailable||''}};
    }
    return json({success:true,layout:state});
  };
  const host=browser.document.createElement('div');browser.document.body.append(host);
  pinia=createPinia();useAuth(pinia).user={id:1,username:'memory'};
  app=vue.createApp(Component);app.use(pinia);app.use(i18n);app.mount(host);await flush();
}
/** :param label: 单屏或双屏。:returns: 通过真实radio改变编排。 */
async function chooseLayout(label){
  const radio=[...browser.document.querySelectorAll('label')].find(item=>visibleText(item.textContent) === visibleText(label))?.querySelector('input[type="radio"]');
  assert.ok(radio,`应有${label}布局选择`);radio.click();await flush();
}
/** :param label: 区域输入名。:param choice: 实际选项。:returns: 从真实Ant菜单选源。 */
async function chooseInput(label,choice){
  const select=browser.document.querySelector(`[role="combobox"][aria-label="${label}"]`)
    ||browser.document.querySelector(`[aria-label="${label}"] input[role="combobox"]`);
  assert.ok(select,`${label}应有可访问组合框`);
  select.focus(); select.click();
  select.dispatchEvent(new browser.KeyboardEvent('keydown',{key:'ArrowDown',code:'ArrowDown',keyCode:40,which:40,bubbles:true}));await flush();
  const root=select.closest('.ant-select');
  const choices=['窗口 1','窗口 2','IP81','不映射','自定义 IP 流'];
  const from=choices.findIndex(item=>root.textContent.includes(item)),to=choices.indexOf(choice);
  assert.ok(from>=0&&to>=0,'只能从明确的当前输入切换到已知选项');
  for(let index=0;index<Math.abs(to-from);index++){
    select.dispatchEvent(new browser.KeyboardEvent('keydown',{key:to>from?'ArrowDown':'ArrowUp',keyCode:to>from?40:38,which:to>from?40:38,bubbles:true}));
    await flush();
  }
  select.dispatchEvent(new browser.KeyboardEvent('keydown',{key:'Enter',keyCode:13,which:13,bubbles:true}));await flush();
  assert.ok(root.textContent.includes(choice),'实际显示应变为'+choice);
}
/** :returns: 排除只读和CSRF后的业务写请求。 */
function writes(){return requests.filter(item=>item.method==='PUT'||item.path.endsWith('/apply/'));}

test('读取与选项编辑不下发，页面没有草稿步骤和发送详情',async()=>{
  await mount();const text=browser.document.querySelector('.wall-map').textContent;
  assert.doesNotMatch(text,/保存.*草稿|草稿已保存|上次发送|发送详情|控制帧已/);
  assert.equal(browser.document.querySelector('[data-wall-dispatch]'),null);
  assert.ok(button('应用布局'));assert.deepEqual(writes(),[]);
  await chooseLayout('单屏');assert.deepEqual(writes(),[]);
});
for(const left of ['window_1','window_2','laptop'])for(const kinds of [[left],...['window_1','window_2','laptop'].map(right=>[left,right])]){
  test(`一次点击应用${kinds.join('|')}，内部按本次返回修订发送`,async()=>{
    await mount(kinds);button('应用布局').click();await flush();await flush();
    const sent=writes();assert.deepEqual(sent.map(item=>item.method),['PUT','POST']);
    assert.deepEqual(sent[0].payload.mappings.map(item=>item.input.kind),kinds);
    assert.equal(sent[1].payload.expected_revision,8);
    assert.equal(requests.some(item=>item.path.includes('/playback/')),false);
    assert.doesNotMatch(browser.document.querySelector('.wall-map').textContent,/上次发送|控制帧已/);
  });
}
test('编辑双屏左右来源后无需先保存，往返布局保持选择',async()=>{
  await mount();await chooseInput('大屏左输入来源','IP81');await chooseInput('大屏右输入来源','窗口 1');
  await chooseLayout('单屏');await chooseLayout('双屏');assert.deepEqual(writes(),[]);
  button('应用布局').click();await flush();await flush();
  assert.deepEqual(writes()[0].payload.mappings.map(item=>item.input.kind),['laptop','window_1']);
});
test('应用忙碌覆盖预检和发送，重复点击只执行一次',async()=>{
  const gate=deferred();await mount(['window_1'],{beforeSave:()=>gate.promise});
  button('应用布局').click();button('应用布局').click();await flush();assert.equal(writes().length,1);
  assert.equal(button('应用布局').disabled,true);gate.resolve();await flush();await flush();assert.equal(writes().length,2);
});
test('设备流未登记只反馈具体原因，不调用发送接口',async()=>{
  await mount(['window_1'],{unavailable:'输入未登记设备流'});button('应用布局').click();await flush();await flush();
  assert.equal(writes().length,1);assert.match(browser.document.querySelector('[role="alert"]').textContent,/未登记设备流/);
});
for(const hooks of [{saveFail:true},{conflict:true}])test('失败或修订冲突不自动重发 '+JSON.stringify(hooks),async()=>{
  await mount(['window_1'],hooks);button('应用布局').click();await flush();await flush();
  assert.equal(writes().length,hooks.saveFail?1:2);assert.ok(browser.document.querySelector('[role="alert"]'));
});
for(const change of ['unmount','host','account'])test('预检等待期间'+change+'，旧操作不继续下发',async()=>{
  const gate=deferred();await mount(['window_1'],{beforeSave:()=>gate.promise});button('应用布局').click();await flush();
  if(change==='unmount'){app.unmount();app=undefined;}
  else if(change==='host')useRuntime(pinia).connectionGeneration++;
  else useAuth(pinia).user={id:123,username:'another-user'};
  gate.resolve();await flush();await flush();assert.equal(writes().length,1);
});
