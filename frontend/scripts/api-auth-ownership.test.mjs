/** 认证响应寿命回归：真实API/Pinia，JSON与XHR仅在网络边界替换，不使用真实账号或设备。 */
import assert from 'node:assert/strict';
import test, { before, after, afterEach } from 'node:test';
import { createPinia } from 'pinia';
import { Window } from 'happy-dom';
import { createDomTestServer } from './dom-test-server.mjs';

const browser = new Window({url:'http://localhost:5173/'});
const names=['window','document','navigator','HTMLElement','Element','Node','SVGElement'];
const globals=new Map(names.map(name=>[name,Object.getOwnPropertyDescriptor(globalThis,name)]));
for(const name of names)Object.defineProperty(globalThis,name,{configurable:true,writable:true,value:browser[name]});
const originalFetch=globalThis.fetch, originalXhr=globalThis.XMLHttpRequest;
let server,api,useAuth,client,pinia,auth,calls,respond,clears,xhrs;
/** :param id: 假用户ID。:returns: 非敏感测试身份。 */
function user(id){return{id,username:'memory-user-'+id,is_staff:true,is_superuser:true};}
/** :param data: 假HTTP数据。:param status: 状态。:returns: 原生Response。 */
function json(data,status=200){return new Response(JSON.stringify(data),{status,headers:{'Content-Type':'application/json'}});}
/** :returns: 可手动完成的外部响应。 */
function gate(){let resolve;const promise=new Promise(done=>{resolve=done;});return{promise,resolve};}
before(async()=>{
 server=await createDomTestServer();
 api=await server.ssrLoadModule('/src/services/api.ts');
 ({useAuthStore:useAuth}=await server.ssrLoadModule('/src/stores/auth.ts'));
 ({clientConnection:client}=await server.ssrLoadModule('/src/platform/connection.ts'));
});
afterEach(async()=>{api.registerUnauthorizedHandler(null);auth?.$dispose();await browser.happyDOM.abort();globalThis.fetch=originalFetch;globalThis.XMLHttpRequest=originalXhr;});
after(async()=>{await server.close();await browser.happyDOM.close();for(const[name,value]of globals){if(value)Object.defineProperty(globalThis,name,value);else delete globalThis[name];}});
/** :returns: 独立认证与网络边界；默认登录响应是新用户。 */
function setup(){
 pinia=createPinia();auth=useAuth(pinia);auth.user=user(1);auth.initialized=true;calls=[];clears=0;xhrs=[];
 browser.document.cookie='csrftoken_http=; Max-Age=0; Path=/';browser.document.cookie='csrftoken=; Max-Age=0; Path=/';
 api.configureApiServer({origin:browser.location.origin,displayName:'内存测试',allowInsecureDevelopment:true});
 api.clearApiSessionState();
 api.registerUnauthorizedHandler(()=>{clears++;auth.clearLocal();});
 respond=async path=>json(path==='/api/auth/csrf/'?{csrfToken:'csrf-current'}:path==='/api/auth/login/'?{user:user(2)}:path==='/api/auth/status/'?{authenticated:true,user:user(1)}:{success:true});
 globalThis.fetch=async(url,options={})=>{const path=new URL(String(url),browser.location.href).pathname;calls.push({path,method:options.method||'GET',headers:options.headers,body:options.body,credentials:options.credentials});return respond(path,options);};
 globalThis.XMLHttpRequest=class {
  upload={};headers={};
  /** :param method: 方法。:param url: 上传地址。:returns: 不连接网络。 */
  open(method,url){this.method=method;this.url=url;}
  /** :param name: 头名称。:param value: 头值。:returns: 保留原认证/CSRF检查证据。 */
  setRequestHeader(name,value){this.headers[name]=value;}
  /** :returns: 使用生产JSON解析路径。 */
  getResponseHeader(){return 'application/json';}
  /** :param body: multipart。:returns: 只登记，不发网络。 */
  send(body){this.body=body;xhrs.push(this);}
 };
}
/** :param xhr: 网络替身。:param status: 状态。:param body: 响应体。:returns: 通过真实onload路径交付。 */
function finishXhr(xhr,status,body){xhr.status=status;xhr.responseText=JSON.stringify(body);xhr.onload();}
/** :returns: 不泄露测试凭据的真正Auth Store登录动作。 */
function login(){return auth.login('memory-only','not-a-real-password');}

for(const status of [200,401,503])test(`JSON旧${status}晚于新登录，不交付旧数据或清除新用户`,async()=>{
 setup();const pending=gate(),fallback=respond;respond=(path,options)=>path==='/api/sources/'?pending.promise:fallback(path,options);
 const old=api.api.listSources();const rejected=assert.rejects(old,/旧响应已丢弃/);
 await login();assert.equal(auth.user.id,2);
 pending.resolve(json(status===200?{sources:[{id:999}]}:{detail:'旧会话错误'},status));await rejected;
 assert.equal(clears,0);assert.equal(auth.user.id,2);
});
test('当前JSON401仍触发全局退出并保留原错误',async()=>{
 setup();respond=async()=>json({detail:'当前会话已失效'},401);
 await assert.rejects(api.api.listSources(),/当前会话已失效/);assert.equal(clears,1);assert.equal(auth.user,null);
});
test('同ID重新登录也使旧JSON响应失效',async()=>{
 setup();const pending=gate(),fallback=respond;respond=(path,options)=>path==='/api/sources/'?pending.promise:path==='/api/auth/login/'?Promise.resolve(json({user:user(1)})):fallback(path,options);
 const old=api.api.listSources();const rejected=assert.rejects(old,/旧响应已丢弃/);await login();
 pending.resolve(json({detail:'旧同ID会话'},401));await rejected;assert.equal(auth.user.id,1);assert.equal(clears,0);
});
test('收到响应头后才发生新登录，也不消费旧响应正文',async()=>{
 setup();const body=gate(),fallback=respond;
 respond=(path,options)=>path==='/api/sources/'?Promise.resolve({status:401,ok:false,headers:new Headers({'Content-Type':'application/json'}),text:()=>body.promise}):fallback(path,options);
 const old=api.api.listSources();const rejected=assert.rejects(old,/旧响应已丢弃/);await login();body.resolve('{"detail":"旧正文"}');await rejected;
 assert.equal(auth.user.id,2);assert.equal(clears,0);
});
test('旧HTML401先判归属，不误解析成当前错误或退出新登录',async()=>{
 setup();const pending=gate(),fallback=respond;respond=(path,options)=>path==='/api/sources/'?pending.promise:fallback(path,options);
 const old=api.api.listSources();const rejected=assert.rejects(old,/旧响应已丢弃/);await login();
 pending.resolve(new Response('<html>old response</html>',{status:401,headers:{'Content-Type':'text/html'}}));await rejected;
 assert.equal(auth.user.id,2);assert.equal(clears,0);
});
test('旧CSRF响应不能覆盖新登录获取的请求令牌',async()=>{
 setup();const pending=gate(),fallback=respond;let first=true;
 respond=(path,options)=>{if(path==='/api/auth/csrf/'&&first){first=false;return pending.promise;}return fallback(path,options);};
 const old=api.api.fetchCsrfToken();const rejected=assert.rejects(old,/旧响应已丢弃/);await login();
 pending.resolve(json({csrfToken:'stale-token'}));await rejected;await api.api.deleteSource(1);
 assert.equal(calls.at(-1).headers['X-CSRFToken'],'csrf-current');
});
for(const status of [200,401])test(`XHR旧${status}不影响新登录或发送完成进度`,async()=>{
 setup();const progress=[];const old=api.api.uploadSource(new FormData(),{onProgress:value=>progress.push(value)});
 const rejected=assert.rejects(old,/旧响应已丢弃/),xhr=xhrs[0];await login();
 xhr.upload.onprogress({lengthComputable:true,loaded:9,total:10});finishXhr(xhr,status,status===200?{source:{id:1}}:{detail:'旧上传401'});await rejected;
 assert.deepEqual(progress,[]);assert.equal(clears,0);assert.equal(auth.user.id,2);
});
test('当前XHR401仍退出，当前成功上传保留Cookie/CSRF及100进度',async()=>{
 setup();await api.api.fetchCsrfToken();const progress=[];
 const good=api.api.uploadSource(new FormData(),{onProgress:value=>progress.push(value)});
 assert.equal(xhrs[0].withCredentials,true);assert.equal(xhrs[0].headers['X-CSRFToken'],'csrf-current');
 finishXhr(xhrs[0],200,{source:{id:4}});assert.equal((await good).source.id,4);assert.deepEqual(progress,[100]);
 const bad=api.api.uploadSource(new FormData());const rejected=assert.rejects(bad,/当前上传认证已失效/);
 finishXhr(xhrs[1],401,{detail:'当前上传认证已失效'});await rejected;assert.equal(clears,1);assert.equal(auth.user,null);
});
test('XHR保留主机代次围栏',async()=>{
 setup();const old=api.api.uploadSource(new FormData());const rejected=assert.rejects(old,/切换|旧响应已丢弃/);
 client.restoreProfile({origin:'http://isolated.invalid',displayName:'纯内存新主机',allowInsecureDevelopment:true});
 finishXhr(xhrs[0],401,{detail:'旧主机上传'});await rejected;assert.equal(clears,0);
});
test('并发首次初始化只执行一次CSRF和状态读取',async()=>{
 setup();auth.$reset();const pending=gate(),fallback=respond;respond=(path,options)=>path==='/api/auth/csrf/'?pending.promise:fallback(path,options);
 const first=auth.ensureInitialized(),second=auth.ensureInitialized();pending.resolve(json({csrfToken:'init-token'}));await Promise.all([first,second]);
 assert.equal(calls.filter(call=>call.path==='/api/auth/csrf/').length,1);assert.equal(calls.filter(call=>call.path==='/api/auth/status/').length,1);
 assert.equal(auth.user.id,1);assert.equal(auth.initialized,true);
});
for(const failure of [false,true])test(`旧初始化${failure?'失败':'未登录'}晚于新登录不能回写`,async()=>{
 setup();auth.$reset();const pending=gate(),fallback=respond;respond=(path,options)=>path==='/api/auth/status/'?pending.promise:fallback(path,options);
 const old=auth.ensureInitialized();await new Promise(resolve=>setImmediate(resolve));await login();
 pending.resolve(json(failure?{detail:'旧初始化错误'}:{authenticated:false,user:null},failure?503:200));await old;
 assert.equal(auth.user.id,2);assert.equal(auth.initialized,true);
});
test('旧主机探测不能覆盖后完成的新登录',async()=>{
 setup();const pending=gate(),fallback=respond;respond=(path,options)=>path==='/api/auth/status/'?pending.promise:fallback(path,options);
 const old=auth.probeCurrentServer();const rejected=assert.rejects(old,/旧响应已丢弃/);await new Promise(resolve=>setImmediate(resolve));await login();
 pending.resolve(json({authenticated:false,user:null}));await rejected;assert.equal(auth.user.id,2);assert.equal(auth.initialized,true);
});
test('旧退出完成不能清除新登录',async()=>{
 setup();const pending=gate(),fallback=respond;respond=(path,options)=>path==='/api/auth/logout/'?pending.promise:fallback(path,options);
 const old=auth.logout();const rejected=assert.rejects(old,/旧响应已丢弃/);await login();pending.resolve(json({detail:'ok'}));await rejected;
 assert.equal(auth.user.id,2);
});
test('等待CSRF的旧登录不得在新登录后继续发送密码',async()=>{
 setup();const pending=gate(),fallback=respond;let first=true;
 respond=(path,options)=>{if(path==='/api/auth/csrf/'&&first){first=false;return pending.promise;}return fallback(path,options);};
 const old=auth.login('old-intent','memory-only');const rejected=assert.rejects(old,/旧响应已丢弃/);await login();
 pending.resolve(json({csrfToken:'old-csrf'}));await rejected;
 assert.equal(calls.filter(call=>call.path==='/api/auth/login/').length,1);assert.equal(auth.user.id,2);
});
test('主机改变后，旧登录不得继续CSRF之后的密码提交',async()=>{
 setup();const pending=gate();respond=()=>pending.promise;const old=login();const rejected=assert.rejects(old,/切换|旧响应已丢弃/);
 client.restoreProfile({origin:'http://isolated.invalid',displayName:'纯内存新主机',allowInsecureDevelopment:true});pending.resolve(json({csrfToken:'old-host-token'}));
 await rejected;assert.equal(calls.filter(call=>call.path==='/api/auth/login/').length,0);
});
test('reset/同ID新认证对象都使旧请求失效',async()=>{
 setup();let pending=gate();respond=()=>pending.promise;
 let old=api.api.listSources(),rejected=assert.rejects(old,/旧响应已丢弃/);auth.$reset();pending.resolve(json({sources:[]}));await rejected;
 auth.user=user(1);pending=gate();old=api.api.listSources();rejected=assert.rejects(old,/旧响应已丢弃/);auth.user=user(1);pending.resolve(json({sources:[]}));await rejected;
});
test('reset后新初始化不等待旧探测，旧finally不解除新寿命',async()=>{
 setup();auth.$reset();const pending=gate(),fallback=respond;let first=true;
 respond=(path,options)=>{if(path==='/api/auth/csrf/'&&first){first=false;return pending.promise;}return fallback(path,options);};
 const old=auth.ensureInitialized();auth.$reset();const current=auth.ensureInitialized();
 await new Promise(resolve=>setImmediate(resolve));const csrfCount=calls.filter(call=>call.path==='/api/auth/csrf/').length;
 pending.resolve(json({csrfToken:'obsolete'}));await Promise.all([old,current]);
 assert.equal(csrfCount,2,'新初始化不能挂在旧外部响应上');assert.equal(auth.user.id,1);assert.equal(auth.initialized,true);
});
test('显式探测期间的初始化共用该探测，不另发状态查询',async()=>{
 setup();const pending=gate(),fallback=respond;respond=async(path,options)=>{if(path==='/api/auth/status/'){await pending.promise;return json({authenticated:true,user:user(1)});}return fallback(path,options);};
 const probe=auth.probeCurrentServer();const initialized=auth.ensureInitialized();
 await new Promise(resolve=>setImmediate(resolve));const statusCount=calls.filter(call=>call.path==='/api/auth/status/').length;
 pending.resolve();const outcomes=await Promise.allSettled([probe,initialized]);assert.equal(statusCount,1);
 assert.equal(outcomes[0].status,'fulfilled');assert.equal(outcomes[0].value,true);
 assert.equal(auth.user.id,1);assert.equal(auth.initialized,true);
});
test('认证绑定重建后不能退回空绑定而复活旧请求',async()=>{
 setup();auth.$dispose();const pending=gate();respond=()=>pending.promise;
 const old=api.api.listSources();const rejected=assert.rejects(old,/旧响应已丢弃/);
 const temporary=useAuth(createPinia());temporary.$dispose();pending.resolve(json({sources:[]}));await rejected;
});
test('旧认证Store释放不使新认证Store的在途请求失效',async()=>{
 setup();const previous=auth;auth=useAuth(createPinia());auth.user=user(2);const pending=gate();respond=()=>pending.promise;
 const current=api.api.listSources();previous.$dispose();pending.resolve(json({sources:[{id:2}]}));
 assert.equal((await current).sources[0].id,2);assert.equal(auth.user.id,2);assert.equal(clears,0);
});
test('认证ID原地改变再恢复，也不能复活旧响应',async()=>{
 setup();const pending=gate();respond=()=>pending.promise;const old=api.api.listSources();const rejected=assert.rejects(old,/旧响应已丢弃/);
 auth.user.id=9;auth.user.id=1;pending.resolve(json({sources:[]}));await rejected;
});
test('旧认证Store不得向新会话发送修改密码请求',async()=>{
 setup();const previous=auth;auth=useAuth(createPinia());auth.user=user(2);
 try {
  await assert.rejects(previous.changePassword('memory-old','memory-new'),/旧响应已丢弃/);
  assert.equal(calls.length,0,'不能发出请求后才发现旧认证动作');
 } finally { previous.$dispose(); }
});
test('修改密码轮换Cookie后，后续写入必须使用新CSRF而非旧缓存',async()=>{
 setup();let token='before-password';
 respond=async path=>{
  if(path==='/api/auth/csrf/')return json({csrfToken:token});
  if(path==='/api/auth/change-password/'){token='after-password';browser.document.cookie='csrftoken_http='+token+'; Path=/';}
  return json({success:true});
 };
 await api.api.fetchCsrfToken();await auth.changePassword('memory-old','memory-new');await api.api.deleteSource(1);
 assert.equal(calls.at(-1).headers['X-CSRFToken'],'after-password');
});
for(const origin of ['http://isolated.invalid','https://isolated.invalid'])test(`修改密码同步使用目标${new URL(origin).protocol}主机令牌，不依赖页面Cookie`,async()=>{
 setup();api.configureApiServer({origin,displayName:'纯内存跨源主机',allowInsecureDevelopment:true});let token='target-before';
 browser.document.cookie='csrftoken_http=unrelated-page-token; Path=/';
 respond=async path=>{
  if(path==='/api/auth/csrf/')return json({csrfToken:token});
  if(path==='/api/auth/change-password/')token='target-after';
  return json({success:true});
 };
 await api.api.fetchCsrfToken();assert.equal(await auth.changePassword('memory-old','memory-new'),true);
 await api.api.deleteSource(1);assert.equal(calls.at(-1).headers['X-CSRFToken'],'target-after');assert.equal(calls.at(-1).credentials,'include');
 assert.equal(calls.filter(call=>call.path==='/api/auth/change-password/').length,1);
});
test('密码已修改但同步503不误报密码失败，显式重试仅读取新CSRF',async()=>{
 setup();let changed=false,failSync=true;
 respond=async path=>{
  if(path==='/api/auth/change-password/')changed=true;
  if(path==='/api/auth/csrf/')return changed&&failSync?json({detail:'仅同步失败'},503):json({csrfToken:changed?'new-token':'old-token'});
  return json({success:true});
 };
 await api.api.fetchCsrfToken();assert.equal(await auth.changePassword('memory-old','memory-new'),false);assert.equal(auth.user.id,1);
 failSync=false;await auth.synchronizeSession();await api.api.deleteSource(1);
 assert.equal(calls.at(-1).headers['X-CSRFToken'],'new-token');assert.equal(calls.filter(call=>call.path==='/api/auth/change-password/').length,1);
});
test('密码POST真实失败保留原错误和令牌，不启动成功同步',async()=>{
 setup();respond=async path=>path==='/api/auth/csrf/'?json({csrfToken:'unchanged-token'}):path==='/api/auth/change-password/'?json({detail:'当前密码不正确'},400):json({success:true});
 await api.api.fetchCsrfToken();await assert.rejects(auth.changePassword('memory-old','memory-new'),/当前密码不正确/);
 assert.equal(calls.filter(call=>call.path==='/api/auth/csrf/').length,1);await api.api.deleteSource(1);assert.equal(calls.at(-1).headers['X-CSRFToken'],'unchanged-token');
});
for(const invalidate of ['account','host'])test(`密码成功后同步在途${invalidate}改变，旧响应不写缓存或报告新会话失败`,async()=>{
 setup();const pending=gate();let changed=false;
 respond=async path=>{
  if(path==='/api/auth/change-password/'){changed=true;return json({detail:'已修改'});}
  if(path==='/api/auth/csrf/'&&changed)return pending.promise;
  return json({csrfToken:'current-token'});
 };
 const old=auth.changePassword('memory-old','memory-new');const rejected=assert.rejects(old,/切换|旧响应已丢弃/);
 // 先释放外部门闩再断言；假fetch忽略AbortSignal，不允许夹具把错误实现挂死。
 await new Promise(resolve=>setImmediate(resolve));const syncing=calls.some(call=>call.path==='/api/auth/csrf/');
 if(invalidate==='account')auth.user=user(2);else client.restoreProfile({origin:'http://isolated.invalid',displayName:'新主机',allowInsecureDevelopment:true});
 respond=async()=>json({csrfToken:'replacement-token'});await api.api.fetchCsrfToken();pending.resolve(json({csrfToken:'obsolete-token'}));await rejected;
 assert.equal(syncing,true);await api.api.deleteSource(1);assert.equal(calls.at(-1).headers['X-CSRFToken'],'replacement-token');assert.equal(clears,0);
});
test('手动会话同步仍保留真实错误和旧Store发送前围栏',async()=>{
 setup();respond=async()=>json({detail:'重读令牌暂不可用'},503);await assert.rejects(auth.synchronizeSession(),/重读令牌暂不可用/);
 const previous=auth;auth=useAuth(createPinia());auth.user=user(2);const count=calls.length;
 try {await assert.rejects(previous.synchronizeSession(),/旧响应已丢弃/);assert.equal(calls.length,count);} finally {previous.$dispose();}
});
test('修改密码成功使此前同ID请求失效，迟到401不能退出重签会话',async()=>{
 setup();const pending=gate(),fallback=respond;respond=(path,options)=>path==='/api/sources/'?pending.promise:fallback(path,options);
 const old=api.api.listSources();const rejected=assert.rejects(old,/旧响应已丢弃/);
 assert.equal(await auth.changePassword('memory-old','memory-new'),true);pending.resolve(json({detail:'改密前401'},401));await rejected;
 assert.equal(clears,0);assert.equal(auth.user.id,1);
});
