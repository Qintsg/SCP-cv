/** 切换完整链路回归：实际Runtime/Auth/API，网络与Electron桥只接内存，不发设备指令。 */
import assert from 'node:assert/strict';
import test, { before, after, afterEach } from 'node:test';
import { setImmediate as nextTurn } from 'node:timers/promises';
import { Window } from 'happy-dom';
import { createDomTestServer } from './dom-test-server.mjs';
const browser=new Window({url:'http://localhost:5173/connect'});
const names=['window','document','navigator','HTMLElement','Element','Node','SVGElement'];
const globals=new Map(names.map(name=>[name,Object.getOwnPropertyDescriptor(globalThis,name)]));
for(const name of names)Object.defineProperty(globalThis,name,{configurable:true,writable:true,value:browser[name]});
const originalFetch=globalThis.fetch, originalXhr=globalThis.XMLHttpRequest;
let server,createPinia,setActivePinia,useAuth,useRuntime,api,client,auth,runtime,requests,respond,pending,running,unauthorized,xhrCount;
before(async()=>{
 [{createPinia,setActivePinia},server]=await Promise.all([import('pinia'),createDomTestServer()]);
 ({useAuthStore:useAuth}=await server.ssrLoadModule('/src/stores/auth.ts'));
 ({useRuntimeStore:useRuntime}=await server.ssrLoadModule('/src/stores/runtime.ts'));
 api=await server.ssrLoadModule('/src/services/api.ts');({clientConnection:client}=await server.ssrLoadModule('/src/platform/connection.ts'));
});
/** :returns: 门闩无论断言成败都会在afterEach释放，不以假fetch挂死充当产品失败。 */
function gate(){let resolve,reject;const promise=new Promise((done,fail)=>{resolve=done;reject=fail;});const value={promise,resolve,reject};pending.push(value);return value;}
/** :param value: 在途操作。:returns: 立即消费拒绝，最后按真实结果断言。 */
function run(value){const result=value.then(value=>({value}),error=>({error}));running.push(result);return result;}
/** :param data: 假数据。:param status: HTTP状态。:returns: 正式JSON解析器响应。 */
function json(data,status=200){return new Response(JSON.stringify(data),{status,headers:{'Content-Type':'application/json'}});}
/** :param name: 假主机。:returns: 仅在内存fetch识别的地址。 */
function profile(name){return{origin:`http://${name}.invalid`,displayName:name};}
/** :returns: 不含真实凭据的独立store与原请求边界。 */
function setup(){
 pending=[];running=[];requests=[];unauthorized=0;xhrCount=0;browser.localStorage.clear();delete browser.scpCvElectron;
 const pinia=createPinia();setActivePinia(pinia);auth=useAuth(pinia);auth.user={id:1,username:'memory-one',is_staff:true,is_superuser:true};auth.initialized=true;
 api.configureApiServer(profile('initial'));api.clearApiSessionState();runtime=useRuntime(pinia);
 api.registerUnauthorizedHandler(()=>{unauthorized++;auth.clearLocal();});
 respond=async path=>json(path==='/api/auth/csrf/'?{csrfToken:'memory-token'}:path==='/api/auth/status/'?{authenticated:true,user:{id:1,username:'memory-one'}}:{detail:'ok'});
 globalThis.fetch=(url,options={})=>{const path=new URL(String(url),browser.location.href).pathname;requests.push({path,url:String(url),method:options.method||'GET'});return respond(path,options);};
 globalThis.XMLHttpRequest=class{upload={};constructor(){xhrCount++;}open(){}setRequestHeader(){}getResponseHeader(){return'application/json';}send(){this.status=200;this.responseText='{"source":{"id":1}}';this.onload();}};
}
afterEach(async()=>{
 for(const item of pending??[])item.resolve(json({detail:'cleanup-only'}));await Promise.all(running??[]);await nextTurn();
 api.registerUnauthorizedHandler(null);runtime?.disconnectEvents();auth?.$dispose();delete browser.scpCvElectron;
 await browser.happyDOM.abort();globalThis.fetch=originalFetch;globalThis.XMLHttpRequest=originalXhr;
});
after(async()=>{await server.close();await browser.happyDOM.close();for(const[name,value]of globals){if(value)Object.defineProperty(globalThis,name,value);else delete globalThis[name];}});

test('Runtime开始切换时就失效真实代次，不等待旧logout',async()=>{
 setup();const old=client.captureGeneration(),hold=gate(),fallback=respond;respond=(path,options)=>path==='/api/auth/logout/'?hold.promise:fallback(path,options);
 const operation=run(runtime.switchServer(profile('next')));await nextTurn();const generation=client.captureGeneration(),projected=runtime.connectionGeneration;
 hold.resolve(json({detail:'ok'}));const result=await operation;
 assert.notEqual(generation,old);assert.equal(projected,generation);assert.equal(result.error,undefined);
 assert.equal(runtime.serverProfile.origin,profile('next').origin);assert.equal(result.value.isCurrent(),true);assert.equal(auth.user,null);
});
test('非法新地址保持原身份/主机和零网络',async()=>{
 setup();const before=client.captureGeneration();const result=await run(runtime.switchServer({origin:'ftp://bad',displayName:''}));
 assert.ok(result.error);assert.equal(requests.length,0);assert.equal(client.captureGeneration(),before);assert.equal(auth.user.id,1);assert.equal(runtime.serverProfile.origin,profile('initial').origin);
});
for(const status of [200,401,503])test(`受管旧logout ${status}不触发当前401清身份或阻止有效切换`,async()=>{
 setup();respond=async()=>json({detail:'受控旧退出'},status);const result=await run(runtime.switchServer(profile('next')));
 assert.equal(result.error,undefined);assert.equal(unauthorized,0);assert.equal(requests.length,1);assert.equal(requests[0].url,profile('initial').origin+'/api/auth/logout/');
 assert.equal(runtime.serverProfile.origin,profile('next').origin);assert.equal(auth.user,null);
});
test('等待旧logout期间出现新认证对象，旧切换不再清新身份或改地址',async()=>{
 setup();const hold=gate();respond=()=>hold.promise;const operation=run(runtime.switchServer(profile('next')));await nextTurn();auth.user={id:2,username:'memory-two'};
 hold.resolve(json({detail:'旧响应'}));const result=await operation;assert.ok(result.error);assert.equal(auth.user.id,2);assert.equal(client.profile.origin,profile('initial').origin);
});
test('原生清会话未结束时普通读/写/上传/新认证均零派发，不重放',async()=>{
 setup();const hold=gate();let entered=false;browser.scpCvElectron={clearSession:async()=>{entered=true;await hold.promise;},saveServerProfile:async value=>value};
 const switching=run(runtime.switchServer(profile('next')));await nextTurn();const reached=entered;
 const calls=await Promise.all([run(api.api.listSources()),run(api.api.deleteSource(1)),run(api.api.uploadSource(new FormData())),run(auth.login('memory','not-real')),run(auth.logout())]);
 const count=requests.length,uploads=xhrCount;hold.resolve();const completed=await switching;
 assert.equal(reached,true);assert.ok(calls.every(result=>result.error&&/正在切换/.test(result.error.message)));assert.equal(count,1);assert.equal(uploads,0);
 assert.equal(completed.error,undefined);assert.equal(requests.length,1,'完成后也不能重放被拒绝请求');assert.equal(client.isSwitching,false);
});
test('调用页面在旧logout期间离开，不继续原生清理或提交目标',async()=>{
 setup();const hold=gate();respond=()=>hold.promise;let active=true,clears=0;browser.scpCvElectron={clearSession:async()=>{clears++;},saveServerProfile:async value=>value};
 const switching=run(runtime.switchServer(profile('next'),()=>active));await nextTurn();active=false;hold.resolve(json({detail:'ok'}));const result=await switching;
 assert.ok(result.error);assert.equal(clears,0);assert.equal(client.profile.origin,profile('initial').origin);assert.equal(runtime.serverProfile.origin,profile('initial').origin);
});
test('当前原生清理失败保留原错误并投影真实代次与原主机',async()=>{
 setup();const failure=new Error('native-clear-failed');browser.scpCvElectron={clearSession:async()=>{throw failure;},saveServerProfile:async value=>value};
 const result=await run(runtime.switchServer(profile('next')));assert.equal(result.error,failure);assert.equal(runtime.connectionGeneration,client.captureGeneration());
 assert.equal(runtime.serverProfile.origin,profile('initial').origin);assert.equal(client.isSwitching,false);
});
test('当前原生保存失败如实保留已提交新主机，不留旧投影或伪造成功',async()=>{
 setup();const failure=new Error('native-save-failed');browser.scpCvElectron={clearSession:async()=>{},saveServerProfile:async()=>{throw failure;}};
 const result=await run(runtime.switchServer(profile('next')));assert.equal(result.error,failure);assert.equal(runtime.connectionGeneration,client.captureGeneration());
 assert.equal(runtime.serverProfile.origin,profile('next').origin);assert.equal(JSON.parse(browser.localStorage.getItem('scp-cv.server-profile')).origin,profile('next').origin);
});
test('完成凭据在后续同ID重新认证后失效',async()=>{
 setup();const result=await run(runtime.switchServer(profile('next')));assert.equal(result.value.isCurrent(),true);auth.user={id:1,username:'new-memory-one'};assert.equal(result.value.isCurrent(),false);
});
test('受管例外不会在普通未切换logout中绕开当前401处理',async()=>{
 setup();respond=async()=>json({detail:'当前401'},401);const result=await run(api.api.logout(()=>true));assert.ok(result.error);assert.equal(unauthorized,1);assert.equal(auth.user,null);
});
test('切换期间安静初始化不发CSRF或回写身份',async()=>{
 setup();const hold=gate();browser.scpCvElectron={clearSession:()=>hold.promise,saveServerProfile:async value=>value};
 const switching=run(runtime.switchServer(profile('next')));await nextTurn();auth.initialized=false;await auth.ensureInitialized();const count=requests.length;
 hold.resolve();await switching;assert.equal(count,1);assert.equal(requests.length,1);
});
test('清会话例外只限固定logout，不接受其它读写路由的越权检查器',async()=>{
 setup();const hold=gate();browser.scpCvElectron={clearSession:()=>hold.promise,saveServerProfile:async value=>value};
 const switching=run(runtime.switchServer(profile('next')));await nextTurn();
 const result=await run(api.requestJson('/api/volume/',{method:'PATCH',body:'{}'},1000,{allowDuringSwitch:()=>true}));const count=requests.length;
 hold.resolve();await switching;assert.ok(result.error&&/正在切换/.test(result.error.message));assert.equal(count,1);
});
test('实际Runtime反序原生清理保持串行，旧结果不覆写最新配置',async()=>{
 setup();const old=gate(),latest=gate();let clears=0,active=0,peak=0;
 browser.scpCvElectron={clearSession:async()=>{const index=++clears;active++;peak=Math.max(peak,active);try{await(index===1?old.promise:latest.promise);}finally{active--;}},saveServerProfile:async value=>value};
 const a=run(runtime.switchServer(profile('older')));await nextTurn();const b=run(runtime.switchServer(profile('latest')));await nextTurn();const premature=clears;
 old.resolve();latest.resolve();const[previous,current]=await Promise.all([a,b]);assert.ok(previous.error);assert.equal(current.error,undefined);
 assert.equal(premature,1);assert.equal(peak,1);assert.equal(clears,2);assert.equal(runtime.serverProfile.origin,profile('latest').origin);assert.equal(current.value.isCurrent(),true);
});
test('实际Runtime持久化反序返回也不能将磁盘或投影反盖为旧主机',async()=>{
 setup();const old=gate();let entered=false,disk='',clears=0;
 browser.scpCvElectron={clearSession:async()=>{clears++;},saveServerProfile:async value=>{if(value.origin===profile('older').origin){entered=true;await old.promise;}disk=value.origin;return value;}};
 const a=run(runtime.switchServer(profile('older')));await nextTurn();const reached=entered;const b=run(runtime.switchServer(profile('latest')));await nextTurn();const premature=clears;
 old.resolve();const[previous,current]=await Promise.all([a,b]);assert.equal(reached,true);assert.equal(premature,1);assert.ok(previous.error);assert.equal(current.error,undefined);
 assert.equal(disk,profile('latest').origin);assert.equal(runtime.serverProfile.origin,disk);assert.equal(JSON.parse(browser.localStorage.getItem('scp-cv.server-profile')).origin,disk);
});
