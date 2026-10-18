/** 现场音量页面归属/真实占用回归：正式DevicesView/Ant/API，仅内存网络，不发设备指令。 */
import assert from 'node:assert/strict';
import test from 'node:test';
import {mount,invalidate,button,flush,browser,gate,json,respondWith,venue,calls,run,loadModule,visibleText,auth,onUnauthorized} from './venue-actions/fixture.mjs';
/** :returns: 通过实际Ant页签进入音量区。 */
async function audio(){const tab=[...browser.document.querySelectorAll('[role="tab"]')].find(node=>visibleText(node.textContent)==='音量与来源');assert.ok(tab);tab.click();await flush();}
/** :returns: 现场声音PUT，不混淆认证POST。 */
function writes(){return calls.filter(item=>item.path==='/api/venue/audio/'&&item.method==='PUT');}
/** :param path: 请求路径。:param options: 原请求。:returns: 保留正式GET及认证响应的边界替身。 */
function normal(path,options){return Promise.resolve(json(path==='/api/venue/'?venue():path==='/api/auth/csrf/'?{csrfToken:'memory'}:path==='/api/sources/'?{sources:[]}:path==='/api/folders/'?{folders:[]}:{success:true,audio:{status:'sent_unconfirmed',detail:'当前音频请求已发送'}}));}

for(const change of ['host','projection','account','relogin','passwordRevision','logout','unmount'])for(const status of [200,503])test(`现场音量${change}后旧${status}不串反馈`,async()=>{
 await mount('page');await audio();const hold=gate();respondWith((path,options)=>path==='/api/venue/audio/'&&options.method==='PUT'?hold.promise:normal(path,options));
 button('应用大屏音量').click();await flush();assert.equal(writes().length,1);await invalidate(change);
 hold.resolve(json(status===200?{audio:{status:'sent_unconfirmed',detail:'旧音频已发送'}}:{detail:'旧音频故障'},status));await flush();await flush();
 assert.doesNotMatch(browser.document.body.textContent,/旧音频已发送|旧音频故障|旧响应已丢弃/);assert.equal(writes().length,1);
});

for(const change of ['relogin','passwordRevision','host'])test(`${change}立即清旧设备投影并由新范围独立加载`,async()=>{
 await mount('page');await audio();const hold=gate();respondWith(path=>path==='/api/venue/'?hold.promise:Promise.resolve(json(path==='/api/auth/csrf/'?{csrfToken:'memory'}:{success:true})));
 const before=calls.filter(item=>item.path==='/api/venue/').length;await invalidate(change);
 assert.equal(Boolean(browser.document.querySelector('.audio-grid')),false,'旧设备投影必须立即消失');assert.equal(calls.filter(item=>item.path==='/api/venue/').length,before+1);
 hold.resolve(json(venue({computer_volume:77})));await flush();await flush();assert.equal(browser.document.querySelector('input[aria-label="大屏音量"]').value,'77');
});

test('退出时旧请求finally不能向匿名环境自发加载设备',async()=>{
 await mount('page');await audio();const hold=gate();respondWith((path,options)=>options.method==='PUT'?hold.promise:normal(path,options));
 button('应用大屏音量').click();await flush();await invalidate('logout');const before=calls.length;
 hold.resolve(json({audio:{detail:'旧提交终端'}}));await flush();await flush();assert.equal(calls.length,before);assert.equal(Boolean(browser.document.querySelector('.audio-grid')),false);
});

test('旧初始GET未结束时新同ID认证能独立加载，旧finally不解除新忙态',async()=>{
 await mount('page');const first=gate(),latest=gate();let gets=0;
 respondWith((path,options)=>path==='/api/venue/'?(++gets===1?first.promise:latest.promise):normal(path,options));
 button('刷新').click();await flush();await invalidate('relogin');assert.equal(gets,2);
 first.resolve(json(venue({computer_volume:11})));await flush();assert.equal(button('刷新').disabled,true);
 latest.resolve(json(venue({computer_volume:82})));await flush();await audio();assert.equal(browser.document.querySelector('input[aria-label="大屏音量"]').value,'82');
});

test('相同后端重登录期间原现场声音仍在途，新范围等终端才GET且不发第二写',async()=>{
 await mount('page');await audio();const hold=gate();let server={computer_volume:25};
 respondWith((path,options)=>path==='/api/venue/audio/'?hold.promise.then(()=>{server=JSON.parse(options.body);return json({audio:{detail:'旧提交完成'}});}):path==='/api/venue/'?Promise.resolve(json(venue(server))):normal(path,options));
 const before=calls.filter(item=>item.path==='/api/venue/').length;button('应用大屏音量').click();await flush();await invalidate('relogin');
 assert.equal(calls.filter(item=>item.path==='/api/venue/').length,before);assert.equal(writes().length,1);
 hold.resolve();await flush();await flush();assert.equal(calls.filter(item=>item.path==='/api/venue/').length,before+1);assert.doesNotMatch(browser.document.body.textContent,/旧提交完成|旧响应已丢弃/);
});

test('不同后端的新应用不被旧请求finally解锁或串结果',async()=>{
 await mount('page');await audio();const first=gate(),latest=gate();respondWith((path,options)=>path==='/api/venue/audio/'?(writes().length===1?first.promise:latest.promise):normal(path,options));
 button('应用大屏音量').click();await flush();await invalidate('host');await flush();assert.ok(calls.some(item=>item.path==='/api/venue/'&&item.url.startsWith('https://new-venue.invalid')));
 button('应用手持话筒').click();await flush();assert.equal(writes().length,2);
 first.resolve(json({audio:{detail:'旧主机提交完成'}}));await flush();assert.equal(button('应用手持话筒').disabled,true);assert.doesNotMatch(browser.document.body.textContent,/旧主机提交完成/);
 latest.resolve(json({audio:{detail:'新主机提交完成'}}));await flush();await flush();assert.equal(button('应用手持话筒').disabled,false);assert.match(browser.document.body.textContent,/新主机提交完成/);
});

for(const status of [200,503])test(`当前现场音量${status}防重并保留真实结果`,async()=>{
 await mount('page');await audio();const hold=gate();respondWith((path,options)=>path==='/api/venue/audio/'?hold.promise:normal(path,options));
 const apply=button('应用大屏音量');apply.click();apply.click();await flush();assert.equal(writes().length,1);
 hold.resolve(json(status===200?{audio:{detail:'当前提交结果',status:'uncertain',failed_field:'computer_volume'}}:{detail:'当前设备原始故障'},status));await flush();await flush();
 assert.match(browser.document.body.textContent,status===200?/当前提交结果/:/当前设备原始故障/);assert.equal(button('应用大屏音量').disabled,false);
});

test('直接声音API原PUT未终端，跨认证的readAudio和PUT均明确拒绝不排队',async()=>{
 await mount('page');const {venueApi}=await loadModule('/src/services/venueApi.ts'),hold=gate();let actions=0;
 respondWith((path,options)=>options.method&&path.startsWith('/api/venue/audio')?(++actions===1?hold.promise:normal(path,options)):normal(path,options));
 const original=run(venueApi.applyAudio({computer_volume:31}));await flush();await invalidate('relogin');
 assert.equal((await run(venueApi.readAudio())).error?.name,'WriteInProgressError');assert.equal((await run(venueApi.applyAudio({handheld_volume:25}))).error?.name,'WriteInProgressError');assert.equal(actions,1);
 hold.resolve(json({audio:{detail:'原写结束'}}));await original;
});

for(const change of ['host','relogin','passwordRevision'])test(`声音GET等待实际占用时${change}，旧等待读不借用新范围`,async()=>{
 await mount();const {venueApi}=await loadModule('/src/services/venueApi.ts'),hold=gate();respondWith((path,options)=>path==='/api/venue/audio/'?hold.promise:normal(path,options));
 const write=run(venueApi.applyAudio({source:'ip81'}));await flush();const read=run(venueApi.get());await invalidate(change);
 const before=calls.filter(item=>item.path==='/api/venue/').length;hold.resolve(json({audio:{detail:'原请求终端'}}));await write;assert.ok((await read).error);assert.equal(calls.filter(item=>item.path==='/api/venue/').length,before);
});

test('页面归属在声音等待中失效，零后续GET',async()=>{
 await mount();const {venueApi}=await loadModule('/src/services/venueApi.ts'),hold=gate();respondWith((path,options)=>path==='/api/venue/audio/'?hold.promise:normal(path,options));
 const write=run(venueApi.applyAudio({computer_volume:18}));await flush();let active=true;const read=run(venueApi.get(()=>active));active=false;
 hold.resolve(json({audio:{detail:'原请求终端'}}));await write;assert.ok((await read).error);assert.equal(calls.filter(item=>item.path==='/api/venue/').length,0);
});

test('现场声音较旧GET晚于新写，明确拒绝过期快照',async()=>{
 await mount();const {venueApi}=await loadModule('/src/services/venueApi.ts'),hold=gate();respondWith((path,options)=>path==='/api/venue/'?hold.promise:normal(path,options));
 const read=run(venueApi.get());await flush();assert.equal(calls.filter(item=>item.path==='/api/venue/').length,1);
 assert.equal((await run(venueApi.applyAudio({computer_volume:78}))).error,undefined);hold.resolve(json(venue({computer_volume:13})));assert.equal((await read).error?.name,'RequestScopeChangedError');
});

test('当前页面旧刷新遇新写不反盖，也不把正常淘汰当错误反馈',async()=>{
 await mount('page');await audio();const {venueApi}=await loadModule('/src/services/venueApi.ts'),hold=gate();respondWith((path,options)=>path==='/api/venue/'?hold.promise:normal(path,options));
 button('刷新').click();await flush();await run(venueApi.applyAudio({computer_volume:78}));hold.resolve(json(venue({computer_volume:13})));await flush();await flush();
 assert.equal(browser.document.querySelector('input[aria-label="大屏音量"]').value,'25');assert.doesNotMatch(browser.document.body.textContent,/旧响应已丢弃/);assert.equal(button('刷新').disabled,false);
});

test('155实际读回POST也占用同一路，终端后可明确应用',async()=>{
 await mount();const {venueApi}=await loadModule('/src/services/venueApi.ts'),hold=gate();respondWith((path,options)=>path==='/api/venue/audio/read/'?hold.promise:normal(path,options));
 const read=run(venueApi.readAudio());await flush();assert.equal((await run(venueApi.applyAudio({screen_volume:40}))).error?.name,'WriteInProgressError');
 hold.resolve(json({audio:{status:'confirmed',screen_volume:60,detail:'155读回'}}));assert.equal((await read).error,undefined);assert.equal((await run(venueApi.applyAudio({screen_volume:40}))).error,undefined);
});

test('现场声音当前401仍全局退出，不从旧finally给匿名环境发恢复读取',async()=>{
 await mount('page');await audio();let unauthorized=0;onUnauthorized(()=>{unauthorized++;auth.clearLocal();});
 respondWith((path,options)=>path==='/api/venue/audio/'?Promise.resolve(json({detail:'当前登录失效'},401)):normal(path,options));
 const reads=calls.filter(item=>item.path==='/api/venue/').length;button('应用大屏音量').click();await flush();await flush();assert.equal(unauthorized,1);assert.equal(auth.user,null);assert.equal(calls.filter(item=>item.path==='/api/venue/').length,reads);
});
