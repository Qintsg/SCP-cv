/** 媒体/目录请求归属回归：真实Vue生命周期、Pinia和受控HTTP，不连接任何播放主机。 */
import assert from 'node:assert/strict';
import test, { before, after, afterEach } from 'node:test';
import { setImmediate as nextTurn } from 'node:timers/promises';
import { Window } from 'happy-dom';
import { createDomTestServer } from './dom-test-server.mjs';

const browser = new Window({url:'http://localhost:5173/sources'});
const names=['window','document','navigator','HTMLElement','HTMLInputElement','Element','Node','SVGElement','Document','MutationObserver','ResizeObserver','Image','getComputedStyle','requestAnimationFrame','cancelAnimationFrame'];
const saved=new Map(names.map(name=>[name,Object.getOwnPropertyDescriptor(globalThis,name)]));
for(const name of names)Object.defineProperty(globalThis,name,{configurable:true,writable:true,value:typeof browser[name]==='function'&&/^[a-z]/.test(name)?browser[name].bind(browser):browser[name]});
const previousFetch=globalThis.fetch;
let server,vue,createPinia,i18n,useSources,useAudio,useAuth,useToast,useActions,useFolders,connection,app,pinia,calls,respond;
const source={id:1,name:'原音频',source_type:'audio',folder_id:1,uri:'/media/a.mp3',file_size:16,is_available:true};
const folder={id:1,name:'原目录',parent_id:null,relative_path:'/原目录'};
/** :param value: 原始快照。:param status: HTTP状态。:returns: 真实Response。 */
function json(value,status=200){return new Response(JSON.stringify(value),{status,headers:{'Content-Type':'application/json'}});}
/** :param id: 当前音频。:returns: 时间较新的旧主机结果，不能依靠时间过滤掩盖归属问题。 */
function audioPayload(id){return {success:true,background_audio:{state:{source_id:id,playback_state:'loading',updated_at:'2026-10-18T00:00:00Z'},playlist:[]}};}
/** :returns: 可手动释放的外部HTTP响应。 */
function gate(){let resolve;const promise=new Promise(done=>{resolve=done;});return{resolve,promise};}
before(async()=>{
 [vue,{createPinia},server]=await Promise.all([import('vue'),import('pinia'),createDomTestServer()]);
 ({i18n}=await server.ssrLoadModule('/src/locales/index.ts'));
 ({useSourceStore:useSources}=await server.ssrLoadModule('/src/stores/sources.ts'));
 ({useBackgroundAudioStore:useAudio}=await server.ssrLoadModule('/src/stores/backgroundAudio.ts'));
 ({useAuthStore:useAuth}=await server.ssrLoadModule('/src/stores/auth.ts'));
 ({useToastStore:useToast}=await server.ssrLoadModule('/src/composables/useToast.ts'));
 ({useSourceActions:useActions}=await server.ssrLoadModule('/src/features/sources/useSourceActions.ts'));
 ({useSourceFolders:useFolders}=await server.ssrLoadModule('/src/features/sources/useSourceFolders.ts'));
 ({clientConnection:connection}=await server.ssrLoadModule('/src/platform/connection.ts'));
});
afterEach(async()=>{app?.unmount();app=undefined;await vue.nextTick();browser.document.body.replaceChildren();await browser.happyDOM.abort();globalThis.fetch=previousFetch;});
after(async()=>{await server?.close();await browser.happyDOM.close();for(const[name,descriptor]of saved){if(descriptor)Object.defineProperty(globalThis,name,descriptor);else delete globalThis[name];}});
/** :returns: 独立原身份与HTTP记录；所有域名均由fetch边界截断。 */
function setup(){
 pinia=createPinia();calls=[];
 connection.restoreProfile({origin:browser.location.origin,displayName:'隔离测试',allowInsecureDevelopment:true});
 const store=useSources(pinia);store.sources=[{...source}];store.folders=[{...folder}];store.currentFolderId=1;
 const auth=useAuth(pinia);auth.user={id:1,username:'first'};
 respond=async path=>json(path==='/api/sources/'?{sources:[{...source}]}:path==='/api/folders/'?{folders:[{...folder}]}:{success:true});
 globalThis.fetch=async(url,options={})=>{const path=new URL(String(url),browser.location.href).pathname;calls.push({path,method:options.method||'GET',body:options.body});return respond(path,options);};
 return{store,auth};
}
/** :param change: 生命周期变化。:returns: 不发网络的真实归属切换。 */
function invalidate(change){
 if(change==='unmount'){app.unmount();app=undefined;}
 else if(change==='host')connection.restoreProfile({origin:'http://isolated.invalid',displayName:'新主机',allowInsecureDevelopment:true});
 else useAuth(pinia).user={id:change==='relogin'?1:2,username:'another-session'};
}
/** :param mode: 业务动作。:returns: 按原生按钮启动并可等待的公开操作。 */
async function mountAction(mode,beforeClick=()=>{}){
 let operation,action,extraRefresh=0;
 app=vue.createApp({setup(){
  const store=useSources(pinia);
  if(mode==='play'||mode==='add'){
   const options=useActions(()=>{},()=>{},()=>[])(store.sources[0]);
   action=options.find(item=>item.key===(mode==='play'?'play-background-audio':'add-background-audio')).action;
  }else{
   const ops=useFolders(async()=>{extraRefresh++;},vue.ref(false));
   if(mode==='create'){ops.openFolderDialog();ops.newFolderName.value='待创建';action=()=>ops.createFolder();}
   else if(mode==='rename'){ops.buildFolderMenu(store.folders[0]).find(item=>item.key==='rename-folder').action();ops.renameFolderName.value='新目录';action=()=>ops.executeFolderRename();}
   else if(mode==='folderMove'){
    store.folders.push({id:2,name:'目标目录',parent_id:null});
    action=ops.buildFolderMenu(store.folders[0]).find(item=>item.key==='move-folder').children.find(item=>item.key==='folder-1-to-2').action;
   }
   else if(mode==='folderDelete')action=()=>{ops.buildFolderMenu(store.folders[0]).find(item=>item.key==='delete-folder').action();return ops.executeFolderDelete();};
   else action=ops.buildMoveToFolderOptions(store.sources[0]).find(item=>item.key==='move-root').action;
  }
  return()=>vue.h('button',{onClick:()=>{operation=action();}},'执行');
 }});app.use(pinia);app.use(i18n);
 const root=browser.document.createElement('div');browser.document.body.append(root);app.mount(root);
 beforeClick();root.querySelector('button').click();await vue.nextTick();await nextTurn();
 return{finish:()=>operation,extraRefresh:()=>extraRefresh};
}

for(const mode of ['play','add','create','move','rename','folderMove','folderDelete'])for(const change of ['unmount','host','account','relogin']){
 test(`${mode}等待时${change}，迟到结果不提示或发UI后续刷新`,async()=>{
  setup();const pending=gate();
  respond=async(path,options)=>options.method?pending.promise:json(path==='/api/folders/'?{folders:[folder]}:{sources:[source]});
  const action=await mountAction(mode);assert.equal(calls.length,1);
  invalidate(change);useToast(pinia).clear();
  pending.resolve(json(mode==='play'||mode==='add'?audioPayload(1):mode==='create'?{success:true,folder:{id:2,name:'待创建',parent_id:1}}:{success:true,source:{...source,folder_id:null}}));
  await action.finish();await vue.nextTick();
  assert.deepEqual(useToast(pinia).items,[]);
  assert.equal(action.extraRefresh(),0);
  assert.equal(calls.length,1,'旧页面或账号的响应不能继续触发读取');
 });
}
for(const mode of ['play','create'])test(`${mode}原页面迟到失败仍显示原始错误`,async()=>{
 setup();const pending=gate();respond=()=>pending.promise;const action=await mountAction(mode);
 pending.resolve(json({detail:'受控原始错误'},503));await action.finish();
 assert.match(useToast(pinia).items.at(-1)?.description||'',/受控原始错误/);
});
test('媒体名称在途改变，成功反馈仍使用点击时名称',async()=>{
 const{store}=setup();const pending=gate();respond=()=>pending.promise;const action=await mountAction('play');
 store.sources[0].name='后来改名';pending.resolve(json(audioPayload(1)));await action.finish();
 assert.match(useToast(pinia).items.at(-1)?.message||'',/原音频/);
 assert.doesNotMatch(useToast(pinia).items.at(-1)?.message||'',/后来改名/);
});
test('新建表单打开后目录改变，仍只写入已冻结的父目录',async()=>{
 const{store}=setup();const pending=gate();respond=()=>pending.promise;
 const action=await mountAction('create',()=>{store.currentFolderId=2;});
 assert.equal(JSON.parse(calls[0].body).parent_id,1);
 pending.resolve(json({success:true,folder:{id:3,name:'待创建',parent_id:1}}));await action.finish();
 assert.equal(store.currentFolderId,2);assert.equal(store.folders.at(-1).parent_id,1);
});

const mutations={
 create:store=>store.createFolder('新目录',null),rename:store=>store.renameFolder(1,'新目录'),
 folderMove:store=>store.moveFolder(1,9),folderDelete:store=>store.deleteFolder(1),sourceMove:store=>store.moveSource(1,null),
};
for(const[key,invoke]of Object.entries(mutations))test(`${key}换账号后的旧结果不污染索引或继续刷新`,async()=>{
 const{store,auth}=setup();const pending=gate();
 respond=async(path,options)=>options.method?pending.promise:json(path==='/api/folders/'?{folders:[folder]}:{sources:[source]});
 const operation=invoke(store);auth.user={id:2,username:'next'};
 store.folders=[{...folder,name:'新账号目录'}];store.sources=[{...source,name:'新账号媒体'}];
 pending.resolve(json({success:true,folder:{id:2,name:'旧结果',parent_id:null},source:{...source,name:'旧结果'}}));
 await assert.rejects(operation,/旧响应已丢弃/);
 assert.equal(calls.length,1,'旧账号完成后不能再发GET');
 assert.deepEqual(store.folders.map(item=>item.name),['新账号目录']);
 assert.deepEqual(store.sources.map(item=>item.name),['新账号媒体']);
 assert.equal(store.currentFolderId,1);
});
test('删除失败时换账号，不以恢复索引为由查询新账号',async()=>{
 const{store,auth}=setup();const pending=gate();respond=async(path,options)=>options.method?pending.promise:json(path==='/api/folders/'?{folders:[folder]}:{sources:[source]});
 const operation=store.deleteSource(1);auth.user={id:2};pending.resolve(json({detail:'原删除错误'},503));
 await assert.rejects(operation,/原删除错误/);assert.equal(calls.length,1);
});
test('背景音乐旧账号的较新时间戳也不能覆盖新账号状态',async()=>{
 const{auth}=setup();const audio=useAudio(pinia);const pending=gate();respond=()=>pending.promise;
 const operation=audio.playSource(1);auth.user={id:2};audio.snapshot={state:{source_id:99,updated_at:'2026-10-17T00:00:00Z'},playlist:[]};
 pending.resolve(json(audioPayload(1)));await assert.rejects(operation,/旧响应已丢弃/);assert.equal(audio.state.source_id,99);
});

test('同一账号两次完整刷新，旧读取不能覆盖较新的完整索引',async()=>{
 const{store}=setup();const pending=[gate(),gate()];let reads=0;
 respond=async path=>{const index=Math.floor(reads++/2);await pending[index].promise;
  const name=index===0?'较旧索引':'最新索引';return json(path==='/api/folders/'?{folders:[{...folder,name}]}:{sources:[{...source,name}]});};
 const first=store.refresh(),latest=store.refresh();pending[1].resolve();await latest;
 pending[0].resolve();await first;
 assert.equal(store.sources[0].name,'最新索引');assert.equal(store.folders[0].name,'最新索引');
});
test('同ID重新认证后，旧完整读取不覆盖新会话索引',async()=>{
 const{store,auth}=setup();const pending=gate();respond=async path=>{await pending.promise;return json(path==='/api/folders/'?{folders:[folder]}:{sources:[source]});};
 const operation=store.refresh();auth.user={id:1,username:'re-authenticated'};
 store.sources=[{...source,name:'新会话媒体'}];store.folders=[{...folder,name:'新会话目录'}];
 pending.resolve();await operation;
 assert.equal(store.sources[0].name,'新会话媒体');assert.equal(store.folders[0].name,'新会话目录');
});
