/** 正式预览组件的本机边界、无流/失败/帧和释放回归；所有HTTP均为内存替身。 */
import assert from 'node:assert/strict';
import test,{before,after,afterEach} from 'node:test';
import {setImmediate as turn} from 'node:timers/promises';
import {Window} from 'happy-dom';
import {createDomTestServer} from './dom-test-server.mjs';
const browser=new Window({url:'https://console.invalid/dashboard'});
const names=['window','document','navigator','HTMLElement','HTMLInputElement','Element','Node','SVGElement','MutationObserver','ResizeObserver','getComputedStyle','requestAnimationFrame','cancelAnimationFrame','sessionStorage'];
const descriptors=new Map(names.map(name=>[name,Object.getOwnPropertyDescriptor(globalThis,name)]));
for(const name of names)Object.defineProperty(globalThis,name,{configurable:true,writable:true,value:typeof browser[name]==='function'&&/^[a-z]/.test(name)?browser[name].bind(browser):browser[name]});
const previousFetch=globalThis.fetch;
let server,vue,Component,address,app,calls;
before(async()=>{vue=await import('vue');server=await createDomTestServer();({default:Component}=await server.ssrLoadModule('/src/features/preview/ClientStreamPreview.vue'));address=await server.ssrLoadModule('/src/features/preview/previewAddress.ts');});
afterEach(async()=>{app?.unmount();app=undefined;await flush();browser.document.body.replaceChildren();browser.sessionStorage.clear();await browser.happyDOM.abort();globalThis.fetch=previousFetch;});
after(async()=>{await server.close();await browser.happyDOM.close();for(const[name,value]of descriptors){if(value)Object.defineProperty(globalThis,name,value);else delete globalThis[name];}});
/** :returns: 执行真实异步微任务而不等待任意墙钟延迟。 */
async function flush(){for(let i=0;i<5;i++){await vue.nextTick();await turn();}}
/** :param input: 输入来源。:param handler: 本机HTTP替身。:param configured: 是否已手动配置本机token。:returns: 正式组件。 */
async function mount(input,handler=()=>{throw Error('unexpected network');},configured=false){
  calls=[];if(configured)browser.sessionStorage.setItem('scp-cv.client-preview-token','a'.repeat(48));
  globalThis.fetch=(url,options={})=>{const parsed=new URL(String(url));assert.equal(parsed.origin,'http://127.0.0.1:18765','不得向D4/媒体设备请求预览');calls.push({path:parsed.pathname,options});return handler(parsed.pathname,options);};
  const host=browser.document.createElement('div');browser.document.body.append(host);app=vue.createApp(Component,{input,quality:'low'});app.mount(host);await flush();
}
test('已登记输入按chn0/1生成地址，3/4不猜IP，无配置为空',()=>{
  assert.equal(address.previewAddress({kind:'window_1'},'low'),'rtsp://192.168.5.63:554/live/chn1');
  assert.equal(address.previewAddress({kind:'window_2'},'high'),'rtsp://192.168.5.64:554/live/chn0');
  assert.equal(address.previewAddress({kind:'laptop'},'high'),'rtsp://192.168.5.81:554/live/chn0');
  for(const input of [undefined,{kind:'window_3'},{kind:'window_4'},{kind:'ip_stream',ip_address:'bad'}])assert.equal(address.previewAddress(input,'low'),'');
});
test('没有流只显示空态，不发请求、不报错或弹窗',async()=>{await mount({kind:'window_3'});assert.match(browser.document.body.textContent,/暂无预览流/);assert.equal(browser.document.querySelector('figcaption'),null);assert.deepEqual(calls,[]);});
test('未配置本机工具不发网络，明确就地说明',async()=>{await mount({kind:'window_1'});assert.match(browser.document.body.textContent,/本机预览未启用/);assert.equal(browser.document.querySelectorAll('figcaption').length,1);assert.deepEqual(calls,[]);});
test('本机请求失败只留一行固定原因，不回显敏感异常、不弹窗',async()=>{await mount({kind:'window_1'},()=>Promise.reject(Error('fixture-secret@source')),true);assert.match(browser.document.body.textContent,/暂时无法预览/);assert.equal(browser.document.querySelectorAll('figcaption').length,1);assert.doesNotMatch(browser.document.body.textContent,/fixture-secret/);assert.equal(browser.document.querySelector('[role="alert"],[role="dialog"],.ant-notification'),null);assert.equal(calls.length,1);});
test('真实组件消费Blob帧，卸载仅释放自己的随机会话',async()=>{
  const id='a'.repeat(32);
  await mount({kind:'window_1'},async(path,options)=>options.method==='POST'?new Response(JSON.stringify({id})):options.method==='DELETE'?new Response(null,{status:204}):new Response(new Uint8Array([255,216,255,217]),{headers:{'Content-Type':'image/jpeg'}}),true);
  assert.match(browser.document.querySelector('img')?.src||'',/^blob:/);assert.equal(browser.document.querySelector('figcaption'),null);
  assert.ok(JSON.parse(calls[0].options.body).url.endsWith('/chn1'));app.unmount();app=undefined;await flush();
  assert.equal(calls.filter(call=>call.options.method==='DELETE'&&call.path===`/sessions/${id}`).length,1);
});
test('创建迟到于卸载仍释放原token会话，不启动帧读取',async()=>{
  let resolve;const pending=new Promise(done=>{resolve=done;});
  await mount({kind:'window_1'},(path,options)=>options.method==='POST'?pending:Promise.resolve(new Response(null,{status:204})),true);
  app.unmount();app=undefined;resolve(new Response(JSON.stringify({id:'b'.repeat(32)})));await flush();
  assert.equal(calls.some(call=>call.path.endsWith('/frame')),false);assert.equal(calls.filter(call=>call.options.method==='DELETE').length,1);
  assert.equal(calls.at(-1).options.headers.Authorization,'Bearer '+'a'.repeat(48));
});
test('图像无法解码时释放自己的订阅并显示内联原因，不留下空白',async()=>{
  const id='c'.repeat(32);await mount({kind:'window_1'},async(path,options)=>options.method==='POST'?new Response(JSON.stringify({id})):options.method==='DELETE'?new Response(null,{status:204}):new Response(new Uint8Array([255,216,255,217])),true);
  browser.document.querySelector('img').dispatchEvent(new browser.Event('error'));await flush();assert.match(browser.document.body.textContent,/无法显示预览画面/);assert.equal(browser.document.querySelectorAll('figcaption').length,1);assert.equal(calls.filter(call=>call.options.method==='DELETE').length,1);assert.equal(browser.document.querySelector('img'),null);
});
