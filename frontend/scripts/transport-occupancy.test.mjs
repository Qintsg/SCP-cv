/** 真实在途占用回归：认证失效不等于同后端请求终端；仅内存HTTP，不发实体指令。 */
import assert from 'node:assert/strict';
import test from 'node:test';
import {setup,mount,gate,run,json,respondWith,invalidate,decrease,muteSwitch,flush,volume,devices,calls,runtime,client,browser,loadModule,toasts,router,auth} from './device-actions/fixture.mjs';

test('同后端同设备原请求未结束，认证变化不能提前允许下一笔',async()=>{
 setup();const hold=gate();let originalFinished=false;
 respondWith(()=>calls.length===1?hold.promise:Promise.resolve(json({success:true})));
 const original=run(devices.power('splice_screen','on'));original.then(()=>{originalFinished=true;});
 await invalidate('relogin');const next=await run(devices.power('splice_screen','off'));
 assert.equal(originalFinished,false,'原请求须仍真实挂起，不能用已结束请求伪造竞态');
 assert.equal(calls.length,1,'同一后端原请求未终端，不因认证变化发送第二笔电源');
 assert.ok(next.error,'原请求仍在途时，重复动作必须明确拒绝而非排队或假成功');
});

for(const kind of ['power','toggle'])for(const change of ['account','relogin','passwordRevision','reset'])for(const status of [200,503]){
 test(`${kind} ${change}不解除真实占用，原${status}终端后可明确重试`,async()=>{
  setup();const hold=gate();let writes=0;
  respondWith(path=>path.startsWith('/api/devices/')?(++writes===1?hold.promise:Promise.resolve(json({success:true}))):Promise.resolve(json({csrfToken:'memory-token',success:true})));
  const id=kind==='power'?'splice_screen':'tv_left',send=()=>kind==='power'?devices.power(id,'on'):devices.toggle(id);
  const old=run(send());await invalidate(change);assert.equal(devices.isPending(id),true);
  assert.equal((await run(send())).error?.name,'DeviceCommandPendingError');assert.equal(writes,1);
  hold.resolve(json({success:status===200,detail:'原操作失败'},status));await old;assert.equal(devices.isPending(id),false);
  assert.equal((await run(send())).error,undefined);assert.equal(writes,2);assert.equal(devices.isPending(id),false);
 });
}

test('后端A→B→A保持各自设备占用，B终端不解锁A',async()=>{
 setup();const a=gate(),b=gate();respondWith(()=>calls.length===1?a.promise:b.promise);
 const original=run(devices.power('splice_screen','on'));await invalidate('host');
 assert.equal(devices.isPending('splice_screen'),false);const other=run(devices.power('splice_screen','on'));
 client.restoreProfile({origin:browser.location.origin,displayName:'A'});assert.equal(devices.isPending('splice_screen'),true);
 b.resolve(json({success:true}));await other;assert.equal(devices.isPending('splice_screen'),true);
 assert.ok((await run(devices.power('splice_screen','off'))).error);assert.equal(calls.length,2);
 a.resolve(json({success:true}));await original;assert.equal(devices.isPending('splice_screen'),false);
});

test('直接API同设备power与toggle共用真实锁，不依赖Store实例',async()=>{
 setup();const {api}=await loadModule('/src/services/api.ts'),hold=gate();respondWith(()=>hold.promise);
 const first=run(api.powerDevice('tv_left','on'));await invalidate('relogin');
 assert.equal((await run(api.toggleDevice('tv_left'))).error?.name,'WriteInProgressError');assert.equal(calls.length,1);
 hold.resolve(json({success:true}));await first;
});

test('当前认证的音量恢复等原请求终端再GET，保留其实际最终值',async()=>{
 await mount();const hold=gate();let server={level:50,muted:false};
 respondWith((path,options)=>{
  if(path!=='/api/volume/')return Promise.resolve(json({success:true}));
  if(options.method!=='PATCH')return Promise.resolve(json(volume(server)));
  return hold.promise.then(()=>{server=JSON.parse(options.body);return json(volume(server));});
 });
 await decrease();await invalidate('relogin');assert.equal(muteSwitch().disabled,true);
 assert.equal(calls.filter(item=>item.path==='/api/volume/'&&item.method==='GET').length,0);
 hold.resolve();await flush();await flush();assert.equal(runtime.systemVolume.level,49);assert.equal(muteSwitch().disabled,false);
 muteSwitch().click();await flush();assert.equal(runtime.systemVolume.muted,true);assert.equal(runtime.systemVolume.level,49);
 assert.equal(toasts.items.length,0);
});

for(const change of ['host','relogin','route'])test(`音量恢复等待中${change}失效不续发旧GET`,async()=>{
 await mount();const {api}=await loadModule('/src/services/api.ts'),hold=gate();respondWith(()=>hold.promise);
 const write=run(runtime.setSystemVolume(31,false)),generation=client.captureGeneration();
 const context=()=>client.captureGeneration()===generation&&router.currentRoute.value.path==='/dashboard';
 const read=run(api.getSystemVolume(context));await flush();await invalidate(change);
 hold.resolve(json(volume({level:31})));await write;assert.ok((await read).error);
 // 重新登录可由当前页面另发新的只读恢复，旧读本身绝不能借用新认证继续。
 if(change!=='relogin')assert.equal(calls.filter(item=>item.path==='/api/volume/'&&item.method==='GET').length,0);
});

test('较旧GET晚于新音量写入返回，不覆盖新写入快照',async()=>{
 setup();const hold=gate();respondWith((path,options)=>options.method==='PATCH'?Promise.resolve(json(volume(JSON.parse(options.body)))):hold.promise);
 const read=run(runtime.refreshSystemVolume());await flush();assert.equal(calls[0].method,'GET');
 assert.equal((await run(runtime.setSystemVolume(24,true))).error,undefined);assert.equal(runtime.systemVolume.level,24);
 hold.resolve(json(volume({level:83,muted:false})));assert.ok((await read).error);
 assert.equal(runtime.systemVolume.level,24);assert.equal(runtime.systemVolume.muted,true);
});

test('恢复GET在途时更新音量，旧恢复不反盖或提示归属错误',async()=>{
 await mount();const writeHold=gate(),readHold=gate();let writes=0;
 respondWith((path,options)=>options.method==='PATCH'?(++writes===1?writeHold.promise:Promise.resolve(json(volume(JSON.parse(options.body))))):readHold.promise);
 await decrease();await invalidate('relogin');writeHold.resolve(json(volume({level:49})));await flush();await flush();
 assert.equal(muteSwitch().disabled,true,'当前恢复GET完成前不能使用过期音量继续静音');
 assert.equal((await run(runtime.setSystemVolume(49,true))).error,undefined);assert.equal(runtime.systemVolume.muted,true);
 readHold.resolve(json(volume({level:49,muted:false})));await flush();await flush();
 assert.equal(runtime.systemVolume.muted,true);assert.equal(toasts.items.length,0);
});

test('同后端原音量仍在途，认证变化后不能交错静音造成界面与服务端相反',async()=>{
 await mount();const hold=gate();let server={level:50,muted:false},originalFinished=false;
 respondWith((path,options)=>{
  if(path!=='/api/volume/')return Promise.resolve(json({success:true}));
  if((options.method||'GET')==='GET')return Promise.resolve(json(volume(server)));
  const body=JSON.parse(options.body),writes=calls.filter(item=>item.path==='/api/volume/'&&item.method==='PATCH');
  if(writes.length===1)return hold.promise.then(()=>{server={...body};originalFinished=true;return json(volume(body));});
  server={...body};return Promise.resolve(json(volume(body)));
 });
 await decrease();await invalidate('relogin');muteSwitch().click();await flush();
 const dispatched=calls.filter(item=>item.path==='/api/volume/'&&item.method==='PATCH').length;
 assert.equal(originalFinished,false,'原请求须仍真实挂起');hold.resolve();await flush();await flush();
 assert.equal(dispatched,1,'原音量仍在途，不能因认证归属失效并发静音');
 assert.equal(runtime.systemVolume.muted,server.muted,'丢弃旧响应不能阻止后端稍后完成原写入');
});

test('实际地址提交后忙态响应式切到新Origin，而不是沿用切换前的缓存',async()=>{
 setup();const {computed}=await import('vue'),{isWritePending}=await loadModule('/src/services/writeFlights.ts');
 const hold=gate();respondWith(()=>hold.promise);run(devices.power('splice_screen','on'));
 const busy=computed(()=>isWritePending('device:splice_screen'));assert.equal(busy.value,true);
 await client.switchServer({origin:'https://other-memory.invalid',displayName:'B'});assert.equal(busy.value,false);
 client.restoreProfile({origin:browser.location.origin,displayName:'A'});assert.equal(busy.value,true);
});

test('获取写占用时同步认证重入，原意图在真正HTTP之前拒绝',async()=>{
 setup();const {watch}=await import('vue'),{isWritePending}=await loadModule('/src/services/writeFlights.ts'),{api}=await loadModule('/src/services/api.ts');
 const stop=watch(()=>isWritePending('system-volume'),busy=>{if(busy)auth.user={id:2,username:'new-memory'};},{flush:'sync'});
 try{assert.ok((await run(api.setSystemVolume(20,true))).error);assert.equal(calls.length,0);assert.equal(isWritePending('system-volume'),false);}
 finally{stop();}
});

test('Store写pending时同步认证重入，不能把原设备动作借给新身份',async()=>{
 setup();const {watch}=await import('vue');
 const stop=watch(()=>devices.lastActionResult.splice_screen,value=>{if(value==='pending')auth.user={id:2,username:'new-memory'};},{flush:'sync'});
 try{assert.ok((await run(devices.power('splice_screen','on'))).error);assert.equal(calls.length,0);assert.equal(devices.lastActionAt.splice_screen,undefined);}
 finally{stop();}
});

test('旧占用释放回调同步开始新写，旧finally不清新占用且读取仍等新请求',async()=>{
 setup();const {watch}=await import('vue'),{isWritePending}=await loadModule('/src/services/writeFlights.ts'),{api}=await loadModule('/src/services/api.ts');
 const first=gate(),next=gate();let started=false,latest;
 respondWith((path,options)=>options.method==='PATCH'?(calls.filter(x=>x.method==='PATCH').length===1?first.promise:next.promise):Promise.resolve(json(volume({level:35,muted:true}))));
 const old=run(api.setSystemVolume(20,false));
 const stop=watch(()=>isWritePending('system-volume'),busy=>{if(!busy&&!started){started=true;latest=run(api.setSystemVolume(35,true));}},{flush:'sync'});
 try{
  first.resolve(json(volume({level:20})));await old;assert.equal(isWritePending('system-volume'),true);
  const read=run(api.getSystemVolume());await flush();assert.equal(calls.filter(x=>x.method==='GET').length,0);
  next.resolve(json(volume({level:35,muted:true})));await latest;assert.equal((await read).value.volume.level,35);assert.equal(isWritePending('system-volume'),false);
 }finally{stop();}
});
