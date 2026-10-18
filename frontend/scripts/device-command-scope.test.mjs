/** 设备结果归属与防重：真实DeviceStore/Auth/API，只在HTTP边界挂起响应。 */
import assert from 'node:assert/strict';
import test from 'node:test';
import {setup,gate,run,json,respondWith,invalidate,devices,calls,auth,client,onUnauthorized} from './device-actions/fixture.mjs';
/** :param kind: 电源动作。:returns: 正式设备动作Promise。 */
function command(kind){return kind==='power'?devices.power('splice_screen','on'):devices.toggle('tv_left');}
/** :param kind: 电源动作。:returns: 实际设备键。 */
function key(kind){return kind==='power'?'splice_screen':'tv_left';}
for(const kind of ['power','toggle'])for(const change of ['host','account','relogin','passwordRevision','reset'])for(const status of [200,503]){
 test(`${kind} ${change}后旧${status}不写新环境错误/时间`,async()=>{
  setup();const hold=gate();respondWith((path)=>path.startsWith('/api/devices/')?hold.promise:Promise.resolve(json(path==='/api/auth/csrf/'?{csrfToken:'memory-token'}:{success:true})));
  const old=run(command(kind));await invalidate(change);const id=key(kind);
  devices.$patch({lastActionResult:{[id]:'pending'},lastActionDetail:{[id]:'新环境保留'},lastActionAt:{[id]:123}});
  hold.resolve(json(status===200?{success:true}:{detail:'旧设备错误'},status));const result=await old;
  assert.ok(result.error,'旧结果必须明确拒绝');assert.equal(devices.lastActionResult[id],'pending');assert.equal(devices.lastActionDetail[id],'新环境保留');assert.equal(devices.lastActionAt[id],123);
 });
}
for(const kind of ['power','toggle'])for(const status of [200,503])test(`${kind}当前${status}保留原结果/时间`,async()=>{
 setup();respondWith(()=>Promise.resolve(json(status===200?{success:true}:{detail:'原设备故障'},status)));const result=await run(command(kind)),id=key(kind);
 assert.equal(devices.lastActionResult[id],status===200?'success':'error');assert.ok(devices.lastActionAt[id]>0);
 if(status===503){assert.match(result.error?.message||'',/原设备故障/);assert.match(devices.lastActionDetail[id],/原设备故障/);}else assert.equal(result.error,undefined);
});
for(const kind of ['power','toggle'])test(`${kind}同设备在途重复动作不产生第二笔HTTP`,async()=>{
 setup();const hold=gate();respondWith(()=>calls.length===1?hold.promise:Promise.resolve(json({success:true})));run(command(kind));
 const duplicate=await run(command(kind));assert.equal(calls.length,1);assert.equal(duplicate.error?.name,'DeviceCommandPendingError');
});
for(const kind of ['power','toggle'])for(const status of [200,503])test(`${kind}旧${status}完成不反盖新同设备操作或解除其防重`,async()=>{
 setup();const oldGate=gate(),newGate=gate();respondWith(()=>calls.length===1?oldGate.promise:calls.length===2?newGate.promise:Promise.resolve(json({success:true})));
 const old=run(command(kind));await invalidate('host');const latest=run(command(kind));assert.equal(calls.length,2);
 oldGate.resolve(json(status===200?{success:true}:{detail:'旧操作故障'},status));await old;assert.equal(devices.lastActionResult[key(kind)],'pending');
 const duplicate=await run(command(kind));assert.equal(duplicate.error?.name,'DeviceCommandPendingError');assert.equal(calls.length,2);
 newGate.resolve(json({success:true}));assert.equal((await latest).error,undefined);assert.equal(devices.lastActionResult[key(kind)],'success');
});
test('不同设备在途操作互不阻塞',async()=>{
 setup();const hold=gate();respondWith(path=>path.includes('splice_screen')?hold.promise:Promise.resolve(json({success:true})));
 run(devices.power('splice_screen','on'));assert.equal((await run(devices.toggle('tv_left'))).error,undefined);assert.equal(calls.length,2);
});
test('切换门禁前置，零HTTP且不写设备失败记录',async()=>{
 setup();const hold=gate(),switching=run(client.switchServer({origin:'https://next-memory.invalid',displayName:'下一个'},{clearSession:()=>hold.promise}));
 const result=await run(devices.power('splice_screen','on'));assert.equal(result.error?.name,'ConnectionSwitchInProgressError');assert.equal(calls.length,0);assert.deepEqual(devices.lastActionResult,{});hold.resolve();await switching;
});
test('当前401仍触发全局退出，不改写新认证环境的设备记录',async()=>{
 setup();let cleared=0;onUnauthorized(()=>{cleared++;auth.clearLocal();});respondWith(()=>Promise.resolve(json({detail:'当前会话已失效'},401)));
 const result=await run(devices.power('splice_screen','on'));assert.equal(cleared,1);assert.equal(auth.user,null);assert.equal(result.error?.name,'UnauthorizedError');assert.notEqual(devices.lastActionResult.splice_screen,'error');assert.equal(devices.lastActionAt.splice_screen,undefined);
});
