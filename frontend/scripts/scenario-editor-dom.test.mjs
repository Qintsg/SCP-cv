/** 已存预案真实编辑器回归：首帧初始化/完整保存/取消归属，仅内存DOM和fetch。 */
import assert from 'node:assert/strict';
import test,{before,after,afterEach} from 'node:test';
import {setImmediate as turn} from 'node:timers/promises';
import {Window} from 'happy-dom';
import {createDomTestServer,visibleText} from './dom-test-server.mjs';
const browser=new Window({url:'http://localhost:5173/scenarios'});
const names=['window','document','navigator','HTMLElement','HTMLInputElement','HTMLTextAreaElement','Element','Node','SVGElement','Document','MutationObserver','ResizeObserver','Image','InputEvent','getComputedStyle','requestAnimationFrame','cancelAnimationFrame'];
const descriptors=new Map(names.map(name=>[name,Object.getOwnPropertyDescriptor(globalThis,name)]));
for(const name of names)Object.defineProperty(globalThis,name,{configurable:true,writable:true,value:typeof browser[name]==='function'&&/^[a-z]/.test(name)?browser[name].bind(browser):browser[name]});
const previousFetch=globalThis.fetch;
let server,vue,createPinia,disposePinia,Component,DirectoryTools,DialogHost,i18n,useAuth,useSources,useScenarios,apiModule,app,pinia,open,saved,calls,response;
const existing={id:7,name:'已保存预案',description:'保留备注',folder_id:3,big_screen_mode_state:'unset',big_screen_mode:'double',volume_state:'set',volume_level:0,targets:[{window_id:1,source_state:'unset',source_id:null,autoplay:false,resume:false}],advanced:{camera_positions:{center1:3},audio:{handheld_volume:0}}};
before(async()=>{vue=await import('vue');({createPinia,disposePinia}=await import('pinia'));server=await createDomTestServer();({default:Component}=await server.ssrLoadModule('/src/features/scenarios/ScenarioEditDrawer.vue'));({i18n}=await server.ssrLoadModule('/src/locales/index.ts'));({useAuthStore:useAuth}=await server.ssrLoadModule('/src/stores/auth.ts'));({useSourceStore:useSources}=await server.ssrLoadModule('/src/stores/sources.ts'));({useScenarioStore:useScenarios}=await server.ssrLoadModule('/src/stores/scenarios.ts'));apiModule=await server.ssrLoadModule('/src/services/api.ts');});
afterEach(async()=>{app?.unmount();app=undefined;await flush();if(pinia)disposePinia(pinia);browser.document.body.replaceChildren();await browser.happyDOM.abort();globalThis.fetch=previousFetch;});
after(async()=>{await server.close();await browser.happyDOM.close();for(const[name,value]of descriptors){if(value)Object.defineProperty(globalThis,name,value);else delete globalThis[name];}});
/** :returns: 已完成响应/Vue任务，不依赖系统输入或任意等待。 */
async function flush(){await vue.nextTick();await turn();await vue.nextTick();await turn();}
/** :param text: 按钮名称。:returns: 真实Ant按钮。 */
function button(text){return [...browser.document.querySelectorAll('button')].find(node=>visibleText(node.textContent)===visibleText(text));}
/** :param item: 现有预案或新建。:returns: 首次即打开的真实编辑器及受控API。 */
async function mount(item=existing){
  pinia=createPinia();useAuth(pinia).user={id:1,username:'memory'};useAuth(pinia).initialized=true;useSources(pinia).sources=[];useScenarios(pinia).folders=[{id:3,name:'会议',parent_id:null}];apiModule.clearApiSessionState();apiModule.registerUnauthorizedHandler(null);
  open=vue.ref(true);saved=[];calls=[];response=async()=>new Response(JSON.stringify({success:true,scenario:existing}),{headers:{'Content-Type':'application/json'}});
  globalThis.fetch=async(url,options={})=>{assert.equal(new URL(String(url),browser.location.href).hostname,'localhost');const path=new URL(String(url),browser.location.href).pathname;
    if(path==='/api/auth/csrf/')return new Response(JSON.stringify({csrfToken:'memory'}),{headers:{'Content-Type':'application/json'}});
    if(path==='/api/inputs/')return new Response(JSON.stringify({success:true,inputs:[]}),{headers:{'Content-Type':'application/json'}});
    calls.push({path,method:options.method||'GET',body:options.body?JSON.parse(options.body):null});return response();};
  const host=browser.document.createElement('div');browser.document.body.append(host);app=vue.createApp({render:()=>vue.h(Component,{open:open.value,scenario:item,'onUpdate:open':next=>{open.value=next;},onSaved:item=>saved.push(item)})});app.use(pinia).use(i18n);app.mount(host);await flush();
}
test('首帧直接打开既有预案时初始化名称、目录、0音量，保留旧不切墙意图',async()=>{await mount();assert.equal(browser.document.querySelector('input[aria-label="预案名称"]').value,existing.name);assert.equal(browser.document.querySelector('input[aria-label="预案目录路径"]').value,'/会议');assert.match(browser.document.body.textContent,/保留旧预案：不改变布局/);assert.deepEqual(calls,[]);});
test('编辑后保存发PATCH且保留其它高级项与0音量，不切墙',async()=>{await mount();const input=browser.document.querySelector('input[aria-label="预案名称"]');input.value='修改后预案';input.dispatchEvent(new browser.Event('input',{bubbles:true}));await flush();button('保存预案').click();await flush();assert.equal(calls.length,1);assert.equal(calls[0].method,'PATCH');assert.equal(calls[0].path,'/api/scenarios/7/');assert.equal(calls[0].body.name,'修改后预案');assert.equal(calls[0].body.volume_level,0);assert.equal(calls[0].body.advanced.audio.handheld_volume,0);assert.equal(calls[0].body.advanced.camera_positions.center1,3);assert.equal(saved.length,1);});
test('新建初始选择窗口1全屏，不出现旧预案保持项',async()=>{await mount(null);assert.doesNotMatch(browser.document.body.textContent,/保留旧预案/);const single=[...browser.document.querySelectorAll('label')].find(node=>visibleText(node.textContent)==='窗口1全屏');assert.equal(single?.querySelector('input')?.checked,true);});
test('两电视分别切换来源可保存，改为保持去掉旧输入，其它高级字段不丢失',async()=>{
  const item={...existing,advanced:{...existing.advanced,televisions:{tv_left:{state:'set',input:{kind:'window_4'}},tv_right:{state:'set',input:{kind:'window_2'}}}}};
  await mount(item);const original=JSON.stringify(item);
  const left=browser.document.querySelector('[aria-label="电视左预案配置"]');
  [...left.querySelectorAll('label')].find(node=>visibleText(node.textContent)==='保持').querySelector('input').click();await flush();
  browser.document.querySelector('button[aria-label="电视右预案输入"]').click();await flush();
  [...browser.document.querySelectorAll('[data-input-menu] button')].find(node=>visibleText(node.textContent).includes('窗口3')).click();await flush();
  button('保存预案').click();await flush();const payload=calls.find(call=>call.method==='PATCH').body;
  assert.deepEqual(payload.advanced.televisions.tv_left,{state:'keep'});
  assert.equal(payload.advanced.televisions.tv_right.input.kind,'window_3');
  assert.equal(payload.advanced.audio.handheld_volume,0);assert.equal(payload.advanced.camera_positions.center1,3);
  assert.equal(JSON.stringify(item),original,'取消/编辑均不得修改列表对象');
});
test('现场音量摄像机全部保持不擦除电视输入或大屏代码',async()=>{
  await mount({...existing,advanced:{...existing.advanced,wall_layout_code:'',televisions:{tv_left:{state:'set',input:{kind:'window_3'}},tv_right:{state:'keep'}}}});
  button('现场参数全部保持').click();await flush();button('保存预案').click();await flush();
  const payload=calls.find(call=>call.method==='PATCH').body;assert.equal(payload.advanced.televisions.tv_left.input.kind,'window_3');
  assert.equal(payload.advanced.audio,undefined);assert.equal(payload.advanced.camera_positions,undefined);
});
for(const change of ['complete','relogin','create'])test(`读取当前输出${change}时只消费当前归属的电视配置`,async()=>{
  await mount();app.unmount();await flush();browser.document.body.replaceChildren();
  const {default:View}=await server.ssrLoadModule('/src/features/scenarios/ScenariosView.vue');
  const {useRuntimeStore}=await server.ssrLoadModule('/src/stores/runtime.ts');
  const {useSessionStore}=await server.ssrLoadModule('/src/stores/sessions.ts');
  useRuntimeStore(pinia).runtime={big_screen_mode:'double'};useRuntimeStore(pinia).systemVolume={level:25,systemSynced:true};useSessionStore(pinia).sessions=[];
  let resolve;response=()=>new Promise(done=>{resolve=done;});const host=browser.document.createElement('div');browser.document.body.append(host);
  app=vue.createApp({render:()=>vue.h(View)});app.use(pinia).use(i18n);app.mount(host);await flush();
  button('从当前状态生成').click();await flush();assert.equal(calls.length,1);assert.equal(calls[0].path,'/api/display-routing/');assert.equal(calls[0].method,'GET');
  if(change==='relogin')useAuth(pinia).user={id:1,username:'new-memory'};
  if(change==='create')button('新建预案').click();await flush();
  resolve(new Response(JSON.stringify({success:true,outputs:{wall:{status:'unknown',last_sent:null},tv_left:{status:'sent_unconfirmed',last_sent:{input:{kind:'window_4'}}},tv_right:{status:'unknown',last_sent:null}}}),{headers:{'Content-Type':'application/json'}}));await flush();await flush();
  assert.equal(calls.length,1,'捕获配置不发任何实体操作');
  if(change==='complete')assert.ok(browser.document.querySelector('button[aria-label="电视左预案输入"]'));
  else assert.equal(browser.document.querySelector('button[aria-label="电视左预案输入"]'),null);
  if(change==='relogin')assert.equal(browser.document.querySelector('input[aria-label="预案名称"]'),null,'旧GET不重新打开抽屉');
});
for(const change of ['close','relogin'])test(`保存等待期间${change}不重复写、不反馈或重新打开旧预览`,async()=>{
  await mount();let resolve;response=()=>new Promise(done=>{resolve=done;});button('保存预案').click();button('保存预案').click();await flush();assert.equal(calls.length,1);
  if(change==='close')open.value=false;else useAuth(pinia).user={id:1,username:'new-memory'};await flush();
  resolve(new Response(JSON.stringify({success:true,scenario:existing}),{headers:{'Content-Type':'application/json'}}));await flush();assert.equal(saved.length,0);assert.equal(open.value,false);assert.equal(calls.length,1);
});
test('预案批量删除在同ID重登后不继续发第二条旧删除或刷新',async()=>{
  await mount();app.unmount();await flush();browser.document.body.replaceChildren();
  ({default:DirectoryTools}=await server.ssrLoadModule('/src/features/scenarios/ScenarioDirectoryTools.vue'));
  ({default:DialogHost}=await server.ssrLoadModule('/src/design-system/FDialogHost.vue'));
  const store=useScenarios(pinia);store.scenarios=[{...existing,id:1,name:'第一份'},{...existing,id:2,name:'第二份'}];store.folders=[];
  const selected=vue.ref([1,2]);let resolve,first=true;response=()=>first?(first=false,new Promise(done=>{resolve=done;})):Promise.resolve(new Response(JSON.stringify({success:true,scenarios:[],folders:[]}),{headers:{'Content-Type':'application/json'}}));
  const host=browser.document.createElement('div');browser.document.body.append(host);
  app=vue.createApp({render:()=>[vue.h(DirectoryTools,{folderId:null,search:'',selected:selected.value,visibleIds:[1,2],'onUpdate:selected':value=>{selected.value=value;}}),vue.h(DialogHost)]});app.use(pinia).use(i18n);app.mount(host);await flush();
  button('删除所选').click();await flush();button('删除').click();await flush();assert.equal(calls.length,1);
  useAuth(pinia).user={id:1,username:'new-memory'};await flush();resolve(new Response(JSON.stringify({success:true}),{headers:{'Content-Type':'application/json'}}));await flush();await flush();
  assert.equal(calls.length,1,'旧批次不借新认证发送后续DELETE/GET');
});
