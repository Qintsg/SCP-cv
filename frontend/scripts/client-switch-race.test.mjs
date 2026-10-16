/** 主机切换事务寿命：实际coordinator，所有不可撤销阶段仅为内存门闩。 */
import assert from 'node:assert/strict';
import test from 'node:test';
import { setImmediate as nextTurn } from 'node:timers/promises';
import { ClientConnectionCoordinator } from '../src/platform/connection.ts';

/** :param name: 假主机名。:returns: 不会发网络的配置。 */
function profile(name) { return { origin:`https://${name}.invalid`,displayName:name }; }
/** :returns: 有明确释放者的在途阶段。 */
function gate() { let resolve,reject; const promise=new Promise((done,fail)=>{resolve=done;reject=fail;});return {promise,resolve,reject}; }
/** :param promise: 立即挂拒绝处理，避免先失败后断言产生异步告警。:returns: 不抛的结果供最后断言。 */
function outcome(promise) { return promise.then(value=>({value}),error=>({error})); }
/** :param result: 操作结果。:returns: 明确失效错误，不把旧成功/原失败当新结果。 */
function assertStale(result) { assert.ok(result.error,'旧切换必须拒绝，不借用新代次成功');assert.match(result.error.message,/切换|失效|归属|丢弃/); }

test('旧清会话迟到不能反盖新选择或使用新代次成功',async()=>{
  const c=new ClientConnectionCoordinator(),pending=gate(),calls=[];
  const first=outcome(c.switchServer(profile('older'),{clearSession:()=>pending.promise,clearStores:()=>calls.push('older-clear'),saveProfile:async()=>calls.push('older-save')}));
  await nextTurn();const latest=outcome(c.switchServer(profile('latest'),{clearStores:()=>calls.push('latest-clear'),saveProfile:async()=>calls.push('latest-save')}));
  pending.resolve();const [old,current]=await Promise.all([first,latest]);
  assertStale(old);assert.equal(current.value,2);assert.equal(c.profile.origin,profile('latest').origin);assert.deepEqual(calls,['latest-clear','latest-save']);
});
test('旧持久化实际结束前不开始新清理，最终有效写入不能被旧写入反盖',async()=>{
  const c=new ClientConnectionCoordinator(),pending=gate(),calls=[];let disk='';
  const first=outcome(c.switchServer(profile('older'),{saveProfile:async value=>{calls.push('older-start');await pending.promise;disk=value.origin;calls.push('older-end');}}));
  await nextTurn();const entered=calls.includes('older-start');
  const latest=outcome(c.switchServer(profile('latest'),{clearSession:async()=>calls.push('latest-clear'),saveProfile:async value=>{disk=value.origin;calls.push('latest-save');}}));
  await nextTurn();const premature=calls.includes('latest-clear');pending.resolve();const [old,current]=await Promise.all([first,latest]);
  assert.equal(entered,true);assert.equal(premature,false,'不可撤销阶段不能并行');assertStale(old);assert.equal(current.value,2);
  assert.equal(disk,profile('latest').origin);assert.deepEqual(calls,['older-start','older-end','latest-clear','latest-save']);
});
test('原生清会话阶段串行，旧阶段结束后跳过旧续写',async()=>{
  const c=new ClientConnectionCoordinator(),a=gate(),b=gate();let active=0,peak=0;
  const clear=pending=>async()=>{active++;peak=Math.max(peak,active);try{await pending.promise;}finally{active--;}};
  const first=outcome(c.switchServer(profile('a'),{clearSession:clear(a)}));await nextTurn();
  const second=outcome(c.switchServer(profile('b'),{clearSession:clear(b)}));await nextTurn();a.resolve();b.resolve();
  const [old,current]=await Promise.all([first,second]);assert.equal(peak,1);assertStale(old);assert.equal(current.value,2);assert.equal(active,0);
});
test('通知回调重入restore后，不再向后续订阅者发送旧代次或执行旧阶段',async()=>{
  const c=new ClientConnectionCoordinator(),versions=[],calls=[];
  c.onGenerationChanged(version=>{if(version===1)c.restoreProfile(profile('restored'));});c.onGenerationChanged(version=>versions.push(version));
  const result=await outcome(c.switchServer(profile('older'),{closeEvents:()=>calls.push('close'),clearSession:async()=>calls.push('clear')}));
  assertStale(result);assert.equal(c.profile.origin,profile('restored').origin);assert.deepEqual(versions,[2]);assert.deepEqual(calls,[]);
});
for(const phase of ['closeEvents','clearStores'])test(`${phase}同步重入新主机后不续写旧阶段`,async()=>{
  const c=new ClientConnectionCoordinator(),calls=[];
  const dependencies={clearSession:async()=>calls.push('session'),saveProfile:async()=>calls.push('save')};
  dependencies[phase]=()=>{calls.push(phase);c.restoreProfile(profile('restored'));};
  const result=await outcome(c.switchServer(profile('older'),dependencies));assertStale(result);
  assert.equal(c.profile.origin,profile('restored').origin);assert.ok(!calls.includes('save'));if(phase==='closeEvents')assert.ok(!calls.includes('session'));
});
test('当前清会话真实失败保留原错误，不清store/提交地址或重放',async()=>{
  const c=new ClientConnectionCoordinator(),failure=new Error('current-cleanup-failure'),calls=[];c.restoreProfile(profile('initial'));
  const result=await outcome(c.switchServer(profile('new'),{clearSession:async()=>{throw failure;},clearStores:()=>calls.push('stores'),saveProfile:async()=>calls.push('save')}));
  assert.equal(result.error,failure);assert.equal(c.profile.origin,profile('initial').origin);assert.deepEqual(calls,[]);assert.equal(c.isSwitching,false);
});
test('旧清会话失败在新意图之后到达，按旧归属拒绝且新意图仍可完成',async()=>{
  const c=new ClientConnectionCoordinator(),pending=gate(),failure=new Error('obsolete-failure');
  const first=outcome(c.switchServer(profile('older'),{clearSession:()=>pending.promise}));await nextTurn();const latest=outcome(c.switchServer(profile('latest')));
  pending.reject(failure);const [old,current]=await Promise.all([first,latest]);assertStale(old);assert.notEqual(old.error,failure);assert.equal(current.value,2);
});
test('当前保存失败保留已提交主机与原错误，不伪造回退或成功',async()=>{
  const c=new ClientConnectionCoordinator(),failure=new Error('current-save-failure');let writes=0;
  const result=await outcome(c.switchServer(profile('new'),{saveProfile:async()=>{writes++;throw failure;}}));
  assert.equal(result.error,failure);assert.equal(c.profile.origin,profile('new').origin);assert.equal(c.isSwitching,false);assert.equal(writes,1);
});
test('非法地址不递增代次、不进入切换门禁或触发任何清理',async()=>{
  const c=new ClientConnectionCoordinator(),calls=[];c.restoreProfile(profile('initial'));const generation=c.captureGeneration();
  await assert.rejects(c.switchServer({origin:'ftp://invalid',displayName:''},{closeEvents:()=>calls.push('close')}));
  assert.equal(c.captureGeneration(),generation);assert.equal(c.profile.origin,profile('initial').origin);assert.equal(c.isSwitching,false);assert.deepEqual(calls,[]);
});
test('restore使旧阶段失效但不会假称仍在途的原生清理已结束',async()=>{
  const c=new ClientConnectionCoordinator(),pending=gate(),calls=[];
  const first=outcome(c.switchServer(profile('older'),{clearSession:()=>pending.promise,clearStores:()=>calls.push('stores')}));await nextTurn();c.restoreProfile(profile('restored'));
  const pendingFlag=c.isSwitching;pending.resolve();const result=await first;assert.equal(pendingFlag,true);assertStale(result);
  assert.deepEqual(calls,[]);assert.equal(c.profile.origin,profile('restored').origin);assert.equal(c.isSwitching,false);
});
test('调用页面在清会话等待中失效，旧阶段不继续清store或保存',async()=>{
  const c=new ClientConnectionCoordinator(),pending=gate(),calls=[];let active=true;c.restoreProfile(profile('initial'));
  const first=outcome(c.switchServer(profile('older'),{canContinue:()=>active,clearSession:()=>pending.promise,clearStores:()=>calls.push('stores'),saveProfile:async()=>calls.push('save')}));
  await nextTurn();active=false;pending.resolve();assertStale(await first);assert.deepEqual(calls,[]);assert.equal(c.profile.origin,profile('initial').origin);
});
test('进入时调用意图已失效，不递增代次或关闭当前资源',async()=>{
  const c=new ClientConnectionCoordinator(),calls=[];c.restoreProfile(profile('initial'));const before=c.captureGeneration();
  const result=await outcome(c.switchServer(profile('new'),{canContinue:()=>false,closeEvents:()=>calls.push('close')}));
  assertStale(result);assert.equal(c.captureGeneration(),before);assert.deepEqual(calls,[]);assert.equal(c.isSwitching,false);
});
