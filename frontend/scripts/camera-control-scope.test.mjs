/** 云台完整归属和位置确认回归：真实Ant/租约/API，仅内存HTTP及受控续租时钟。 */
import assert from 'node:assert/strict';
import test from 'node:test';
import {mount,invalidate,key,renew,cameraWrites,button,flush,browser,gate,json,respondWith,loadModule,run,client} from './venue-actions/fixture.mjs';

for(const change of ['host','projection','account','relogin','passwordRevision','logout'])test(`旧运动在${change}后不续租或把停止发给新环境`,async()=>{
 await mount();await key();assert.deepEqual(cameraWrites().map(item=>item.body.phase),['start']);
 await invalidate(change);await renew();await key('keyup');
 assert.deepEqual(cameraWrites().map(item=>item.body.phase),['start']);
 assert.equal(Boolean(browser.document.querySelector('[aria-pressed="true"]')),false);
});

for(const change of ['host','relogin','passwordRevision'])test(`迟到start在${change}后不恢复心跳或旧错误`,async()=>{
 await mount();const hold=gate();respondWith(path=>path.includes('/motion/')?hold.promise:Promise.resolve(json(path==='/api/auth/csrf/'?{csrfToken:'memory'}:{success:true})));
 await key();await invalidate(change);hold.resolve(json({success:true,camera:{detail:'旧按下完成'}}));await flush();await renew();
 assert.equal(cameraWrites().length,1);assert.doesNotMatch(browser.document.body.textContent,/旧响应已丢弃|旧按下完成/);
});

for(const change of ['unmount','camera','capability'])test(`${change}仍独立停止原有效运动，不续租`,async()=>{
 await mount();await key();await invalidate(change);await renew();
 assert.deepEqual(cameraWrites().map(item=>[item.path,item.body.phase]),[['/api/venue/cameras/center1/motion/','start'],['/api/venue/cameras/center1/motion/','stop']]);
 assert.equal(cameraWrites()[0].body.motion_id,cameraWrites()[1].body.motion_id);
});

test('当前按住续租与失焦保持同一令牌，重复失焦不重发停止',async()=>{
 await mount();await key();await renew();browser.dispatchEvent(new browser.Event('blur'));await flush();browser.dispatchEvent(new browser.Event('blur'));await flush();await renew();
 assert.deepEqual(cameraWrites().map(item=>item.body.phase),['start','hold','stop']);assert.equal(new Set(cameraWrites().map(item=>item.body.motion_id)).size,1);
});

for(const change of ['host','projection','account','relogin','passwordRevision','logout','camera','capability'])test(`保存位置确认在${change}后失效，零位置写`,async()=>{
 await mount();button('保存当前位置…').click();await flush();assert.match(browser.document.body.textContent,/将覆盖中摄像机1的位置1/);
 await invalidate(change);button('OK')?.click();await flush();
 assert.equal(cameraWrites().filter(item=>item.path.includes('/positions/')).length,0);
});

for(const status of [200,503])for(const change of ['host','relogin','passwordRevision','camera'])test(`旧位置${status}在${change}后不串反馈`,async()=>{
 await mount();const hold=gate();respondWith(path=>path.includes('/positions/')?hold.promise:Promise.resolve(json(path==='/api/auth/csrf/'?{csrfToken:'memory'}:{success:true})));
 button('调用位置').click();await flush();await invalidate(change);hold.resolve(json(status===200?{camera:{detail:'旧机位已发送'}}:{detail:'旧机位故障'},status));await flush();await flush();
 assert.doesNotMatch(browser.document.body.textContent,/旧机位已发送|旧机位故障|旧响应已丢弃/);
});

test('位置调用同渲染轮防重，当前失败保留说明并允许显式重试',async()=>{
 await mount();const hold=gate();respondWith(()=>hold.promise);const call=button('调用位置');call.click();call.click();await flush();assert.equal(cameraWrites().length,1);
 hold.resolve(json({detail:'当前机位故障'},503));await flush();assert.match(browser.document.body.textContent,/当前机位故障/);assert.equal(button('调用位置').disabled,false);
});

test('直接位置API在同Origin认证变化后仍保留实际在途占用',async()=>{
 await mount();const {venueApi}=await loadModule('/src/services/venueApi.ts'),hold=gate();respondWith(()=>cameraWrites().length===1?hold.promise:Promise.resolve(json({camera:{detail:'第二笔响应'}})));
 const first=run(venueApi.position('center1',1,'call'));await invalidate('relogin');const next=await run(venueApi.position('center1',2,'save'));
 assert.equal(cameraWrites().length,1);assert.equal(next.error?.name,'WriteInProgressError');hold.resolve(json({camera:{detail:'原请求完成'}}));await first;
});

for(const status of [200,503])test(`当前保存确认${status}只保存冻结位置一次并保留原反馈`,async()=>{
 await mount();const hold=gate();respondWith(()=>hold.promise);
 const input=browser.document.querySelector('input[aria-label="中摄像机1位置"]');input.value='3';input.dispatchEvent(new browser.Event('input',{bubbles:true}));await flush();
 button('保存当前位置…').click();await flush();assert.match(browser.document.body.textContent,/将覆盖中摄像机1的位置3/);assert.equal(input.disabled,true);assert.equal(cameraWrites().length,0);
 const confirm=button('OK');assert.ok(confirm);confirm.click();confirm.click();await flush();assert.equal(cameraWrites().length,1);assert.equal(cameraWrites()[0].path,'/api/venue/cameras/center1/positions/3/save/');
 hold.resolve(json(status===200?{camera:{detail:'当前位置保存已发送'}}:{detail:'当前位置保存故障'},status));await flush();assert.match(browser.document.body.textContent,status===200?/当前位置保存已发送/:/当前位置保存故障/);assert.equal(button('调用位置').disabled,false);
});

test('取消保存确认零请求，方向与位置恢复可操作',async()=>{
 await mount();button('保存当前位置…').click();await flush();assert.equal(button('调用位置').disabled,true);button('Cancel').click();await flush();assert.equal(cameraWrites().length,0);assert.equal(button('调用位置').disabled,false);
});

test('位置请求真实在途时同ID新认证不能提前开放同摄像机操作',async()=>{
 await mount();const hold=gate();respondWith(()=>hold.promise);button('调用位置').click();await flush();await invalidate('relogin');assert.equal(button('调用位置').disabled,true);
 button('调用位置').click();await flush();assert.equal(cameraWrites().length,1);hold.resolve(json({camera:{detail:'原位置完成'}}));await flush();assert.equal(button('调用位置').disabled,false);assert.doesNotMatch(browser.document.body.textContent,/原位置完成/);
});

test('旧摄像机位置finally不解锁另一摄像机的新位置请求',async()=>{
 await mount();const old=gate(),latest=gate();respondWith(()=>cameraWrites().length===1?old.promise:latest.promise);
 button('调用位置').click();await flush();await invalidate('camera');assert.equal(button('调用位置').disabled,false);button('调用位置').click();await flush();assert.equal(cameraWrites().length,2);
 old.resolve(json({camera:{detail:'旧位置完成'}}));await flush();assert.equal(button('调用位置').disabled,true);assert.doesNotMatch(browser.document.body.textContent,/旧位置完成/);
 latest.resolve(json({camera:{detail:'新位置已发送'}}));await flush();assert.equal(button('调用位置').disabled,false);assert.match(browser.document.body.textContent,/新位置已发送/);
});

test('迟到start不阻止卸载独立stop，响应回来后不再续租',async()=>{
 await mount();const hold=gate();respondWith((path,options)=>JSON.parse(options.body).phase==='start'?hold.promise:Promise.resolve(json({camera:{detail:'独立停止'}})));
 await key();await invalidate('unmount');assert.deepEqual(cameraWrites().map(item=>item.body.phase),['start','stop']);hold.resolve(json({camera:{detail:'原start完成'}}));await flush();await renew();assert.equal(cameraWrites().length,2);
});

test('当前续租失败发送独立停止并保留原说明，不自动开始',async()=>{
 await mount();respondWith((path,options)=>JSON.parse(options.body).phase==='hold'?Promise.resolve(json({detail:'当前续租故障'},503)):Promise.resolve(json({camera:{detail:'已发送'}})));
 await key();await renew();await key('keyup');await renew();assert.deepEqual(cameraWrites().map(item=>item.body.phase),['start','hold','stop']);assert.match(browser.document.body.textContent,/当前续租故障/);
});
