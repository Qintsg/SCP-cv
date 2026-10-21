/** 共享池平台薄适配：普通Web不走IPC，原生只传受限来源，不静默降回服务器中转。 */
import assert from 'node:assert/strict';
import test,{before,after,afterEach}from'node:test';
import{Window}from'happy-dom';
import{createDomTestServer}from'./dom-test-server.mjs';
const browser=new Window({url:'https://console.invalid/dashboard'});
const names=['window','document','navigator','HTMLElement','Element','Node','SVGElement','MutationObserver','ResizeObserver','getComputedStyle'];
const saved=new Map(names.map(name=>[name,Object.getOwnPropertyDescriptor(globalThis,name)]));
for(const name of names)Object.defineProperty(globalThis,name,{configurable:true,writable:true,value:typeof browser[name]==='function'&&/^[a-z]/.test(name)?browser[name].bind(browser):browser[name]});
const originalFetch=globalThis.fetch;
let server,transport,native;
before(async()=>{server=await createDomTestServer();transport=await server.ssrLoadModule('/src/features/preview/previewTransport.ts');native=await server.ssrLoadModule('/src/platform/native.ts');});
afterEach(()=>{native.setNativePlatformAdapter(null);delete browser.scpCvElectron;globalThis.fetch=originalFetch;});
after(async()=>{await server.close();await browser.happyDOM.close();for(const[name,descriptor]of saved){if(descriptor)Object.defineProperty(globalThis,name,descriptor);else delete globalThis[name];}});
const request={input:{kind:'window_3'},quality:'low'};
const lease={id:'00000000-0000-0000-0000-000000000001',sharingKey:'a'.repeat(64),streamUrl:'app://scp-cv/__preview/'+'b'.repeat(64)+'/stream.flv',expiresAt:new Date(Date.now()+30000).toISOString()};
test('普通网页只申请同Host受限租约，心跳别名不泄露源地址，失效不发旧DELETE',async()=>{
  const calls=[];globalThis.fetch=async(url,init={})=>{calls.push({url:String(url),init});return new Response(JSON.stringify({id:lease.id,sharing_key:lease.sharingKey,expires_at:lease.expiresAt}),{headers:{'Content-Type':'application/json'}});};
  const adapter=transport.createPreviewTransport(),abort=new AbortController();let current=true;
  const created=await adapter.acquire(request,()=>current,abort.signal);assert.match(created.streamUrl,/^https:\/\/console\.invalid\/api\/stream-previews\//);
  assert.deepEqual(JSON.parse(calls[0].init.body),request);
  await adapter.heartbeat(created,()=>current,abort.signal,[lease.id]);assert.deepEqual(JSON.parse(calls[1].init.body),{aliases:[lease.id]});
  current=false;await adapter.release(created,()=>current);assert.equal(calls.length,2);assert.doesNotMatch(JSON.stringify(calls),/rtsp:|rtmp:|192\.168/);
});
test('Electron缺少原生能力不伪装为已直连，也不静默走Web传输',async()=>{
  browser.scpCvElectron={};let calls=0;globalThis.fetch=async()=>{calls++;throw Error('不得请求');};
  await assert.rejects(()=>transport.createPreviewTransport().acquire(request,()=>true,new AbortController().signal),/原生预览组件不可用/);
  assert.equal(calls,0);
});
test('原生平台只传受限target与租约ID，保留alias heartbeat和受控fallback',async()=>{
  const calls=[];native.setNativePlatformAdapter({preview:{async acquire(target){calls.push(['acquire',target]);return lease;},async heartbeat(id,aliases){calls.push(['heartbeat',id,aliases]);},async release(id){calls.push(['release',id]);},async invalidate(){calls.push(['invalidate']);},async fallback(id){calls.push(['fallback',id]);}}});
  globalThis.fetch=async()=>{throw Error('原生不能请求Web流');};const adapter=transport.createPreviewTransport(),abort=new AbortController();
  const created=await adapter.acquire(request,()=>true,abort.signal);assert.equal(created.streamUrl,lease.streamUrl);
  await adapter.heartbeat(created,()=>true,abort.signal,['validation']);await adapter.fallback(created);await adapter.release(created,()=>false);
  assert.deepEqual(calls,[['acquire',request],['heartbeat',lease.id,['validation']],['fallback',lease.id],['release',lease.id]]);
});
test('原生迟到创建在归属失效后只释放原租约，不交给decoder',async()=>{
  let current=true,released=[];native.setNativePlatformAdapter({preview:{async acquire(){current=false;return lease;},async release(id){released.push(id);}}});
  await assert.rejects(()=>transport.createPreviewTransport().acquire(request,()=>current,new AbortController().signal),/预览归属已失效/);
  assert.deepEqual(released,[lease.id]);
});

test('首屏早于PlatformAdapter安装时使用已就绪的受限preload桥，不缓存成不可用',async()=>{
  let acquired=0;browser.scpCvElectron={preview:{async acquire(target){acquired++;assert.deepEqual(target,request);return lease;},async heartbeat(){},async release(){},async invalidate(){}}};
  globalThis.fetch=async()=>{throw Error('不得回退Web');};
  const created=await transport.createPreviewTransport().acquire(request,()=>true,new AbortController().signal);
  assert.equal(acquired,1);assert.equal(created.streamUrl,lease.streamUrl);
});
