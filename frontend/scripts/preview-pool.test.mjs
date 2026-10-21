/** 020共享池回归：独立假传输/decoder证明计数与寿命，不连接设备，不冒称真实媒体解码。 */
import assert from 'node:assert/strict';
import test, {before,after,afterEach} from 'node:test';
import {setImmediate as turn,setTimeout as delay} from 'node:timers/promises';
import {Window} from 'happy-dom';
import {createDomTestServer} from './dom-test-server.mjs';
const browser=new Window({url:'https://console.invalid/dashboard'});
const names=['window','document','navigator','HTMLElement','Element','Node','SVGElement','MutationObserver','ResizeObserver','getComputedStyle'];
const restored=new Map(names.map(name=>[name,Object.getOwnPropertyDescriptor(globalThis,name)]));
for(const name of names)Object.defineProperty(globalThis,name,{configurable:true,writable:true,value:typeof browser[name]==='function'&&/^[a-z]/.test(name)?browser[name].bind(browser):browser[name]});
let server,Pool,pool,created,closed,players,renewed,leases;
before(async()=>{server=await createDomTestServer();({PreviewPool:Pool}=await server.ssrLoadModule('/src/features/preview/previewPool.ts'));});
afterEach(async()=>{pool?.dispose();await flush();browser.document.body.replaceChildren();await browser.happyDOM.abort();});
after(async()=>{await server.close();await browser.happyDOM.close();for(const[name,value]of restored){if(value)Object.defineProperty(globalThis,name,value);else delete globalThis[name];}});
/** :returns: 排空真实promise工作，不伪造播放器新帧。 */
async function flush(){for(let index=0;index<8;index++)await turn();}
/** :param options: 受控假响应。:returns: 独立共享池，不经过外部网络。 */
function fixture(options={}){
  created=[];closed=[];players=[];renewed=[];leases=new Map();let serial=0;
  const acquire=async(target,current,signal)=>{if(options.acquire)return options.acquire(target,current,signal);
    const key=options.key?.(target)??(target.source_id?`source:${target.source_id}`:target.input.kind);
    const lease={id:String(++serial),sharingKey:key,streamUrl:`https://console.invalid/stream/${serial}`,expiresAt:new Date(Date.now()+30000).toISOString()};
    created.push({lease,target});leases.set(lease.id,target);return lease;};
  pool=new Pool(async(video,url,frame,failed)=>{const player={video,url,frame,failed,destroyed:false,destroy(){this.destroyed=true;}};players.push(player);if(options.frame!==false)frame();return player;},{acquire,
    async heartbeat(lease,current,signal,aliases){renewed.push({id:lease.id,aliases});await options.heartbeat?.(lease,aliases);},
    async release(lease){closed.push(lease.id);leases.delete(lease.id);},...(options.fallback?{fallback:options.fallback}:{})},
  {idleGrace:25,heartbeat:options.heartbeatMs??100000,monitor:100000,stale:3000,failed:20000,retry:100000,...options.timing});
  return pool;
}
/** :returns: 可审计帧/状态/清空接收端。 */
function sink(){return{frames:[],states:[],clears:0,frame(video){this.frames.push(video);},state(state){this.states.push(state);},clear(){this.clears++;}};}
const one={input:{kind:'window_1'},quality:'low'};
test('65个相同target只两份租约（一验证一消费）、一个decoder，移除64个不停止',async()=>{
  fixture();const sinks=Array.from({length:65},sink),release=sinks.map(item=>pool.subscribe(one,item,()=>true));await flush();
  assert.equal(created.length,2);assert.equal(players.length,1);assert.deepEqual(pool.diagnostics,{targets:1,streams:1,decoders:1});
  assert.ok(sinks.every(item=>item.states.at(-1)==='live'&&item.frames.length===1));
  release.slice(0,64).forEach(close=>close());await delay(35);assert.equal(players[0].destroyed,false);assert.equal(closed.length,0);
  release[64]();await delay(40);assert.equal(players[0].destroyed,true);assert.equal(closed.length,2);assert.deepEqual(pool.diagnostics,{targets:0,streams:0,decoders:0});
});
test('改变几何的组件重建与quality提示不重新拉流，新画布立即取得帧',async()=>{
  fixture();const first=sink(),close=pool.subscribe(one,first,()=>true);await flush();close();
  const second=sink();pool.subscribe({...one,quality:'high'},second,()=>true);await flush();await delay(35);
  assert.equal(created.length,2);assert.equal(players.length,1);assert.equal(players[0].destroyed,false);assert.equal(second.frames.length,1);
});
test('不同别名在sharing_key后原子合并，非消费别名独立保留修订心跳',async()=>{
  fixture({key:()=> 'same-actual-uri',heartbeatMs:20});pool.subscribe(one,sink(),()=>true);
  pool.subscribe({input:{kind:'ip_stream',ip_address:'192.168.5.63'},quality:'low'},sink(),()=>true);await flush();await delay(30);await flush();
  assert.equal(created.length,3);assert.equal(players.length,1);assert.equal(pool.diagnostics.targets,2);
  assert.ok(renewed.some(item=>item.aliases?.length===2));assert.equal(renewed.filter(item=>!item.aliases).length,2);
});
test('换一个来源不销毁另一路，真实不同流不合并',async()=>{
  fixture();const old=sink(),keep=sink();const close=pool.subscribe(one,old,()=>true);pool.subscribe({input:{kind:'window_2'},quality:'low'},keep,()=>true);await flush();
  const preserved=players[1];close();pool.subscribe({source_id:9,quality:'low'},sink(),()=>true);await flush();await delay(35);
  assert.equal(players.length,3);assert.equal(preserved.destroyed,false);assert.equal(players[0].destroyed,true);
});
test('主机或同ID重新登录失效立即停止、清像素，迟到租约不能创建decoder',async()=>{
  let current=true,done;fixture({acquire:()=>new Promise(resolve=>{done=resolve;})});const target=sink();pool.subscribe(one,target,()=>current);await flush();
  current=false;pool.invalidateObsolete();done({id:'late',sharingKey:'late',streamUrl:'https://console.invalid/late',expiresAt:new Date().toISOString()});await flush();
  assert.equal(players.length,0);assert.equal(target.clears,1);assert.ok(closed.includes('late'));assert.equal(pool.diagnostics.targets,0);
});
test('应用销毁不等短宽限，所有decoder/续租都释放',async()=>{
  fixture();pool.subscribe(one,sink(),()=>true);await flush();pool.dispose();await flush();
  assert.equal(players[0].destroyed,true);assert.equal(closed.length,2);assert.equal(browser.document.querySelector('video'),null);
});
test('只有实际frame回调才绿色，HTTP/工厂完成不等于出画',async()=>{
  fixture({frame:false});const target=sink();pool.subscribe(one,target,()=>true);await flush();
  assert.equal(target.states.at(-1),'connecting');players[0].frame();assert.equal(target.states.at(-1),'live');
  players[0].failed();await flush();assert.equal(target.states.at(-1),'reconnecting');
});
test('一个别名源修订变化只迁该别名，原decoder与其它消费者不变',async()=>{
  let revision=1,invalid=false;fixture({key:target=>target.source_id?`uri-${revision}`:'uri-1',heartbeatMs:20,
    heartbeat(lease,aliases){if(!aliases&&leases.get(lease.id)?.source_id&&invalid){invalid=false;revision=2;throw Error('revision changed');}}});
  const keep=sink(),edited=sink();pool.subscribe(one,keep,()=>true);pool.subscribe({source_id:7,quality:'low'},edited,()=>true);await flush();
  const original=players[0];invalid=true;await delay(30);await flush();
  assert.equal(players.length,2);assert.equal(original.destroyed,false);assert.equal(keep.clears,0);assert.ok(edited.frames.length>=2);
});

test('待创建期间移除/失败/再加入，旧空闲定时器不能杀死新引用',async()=>{
  let reject,serial=0;fixture({acquire:()=>++serial===1?new Promise((_,fail)=>{reject=fail;}):Promise.resolve({id:String(serial),sharingKey:'one',streamUrl:'https://console.invalid/stream',expiresAt:new Date(Date.now()+30000).toISOString()})});
  const close=pool.subscribe(one,sink(),()=>true);close();reject(Error('pending fixture failed'));await flush();
  const target=sink();pool.subscribe(one,target,()=>true);await flush();await delay(40);
  assert.equal(pool.diagnostics.targets,1);assert.equal(players.length,1);assert.equal(players[0].destroyed,false);assert.equal(target.states.at(-1),'live');
});

test('重复销毁/播放器退出异常仍释放两份原租约',async()=>{
  fixture();pool.subscribe(one,sink(),()=>true);await flush();players[0].destroy=()=>{throw Error('fixture exit');};
  pool.dispose();pool.dispose();await flush();assert.equal(closed.length,2);assert.equal(pool.diagnostics.decoders,0);assert.equal(browser.document.querySelector('video'),null);
});

test('原生copy解码失败只兼容切换一次，并恢复别名/消费续租',async()=>{
  let switched=0;fixture({frame:false,heartbeatMs:20,fallback:async()=>{switched++;}});
  const target=sink();pool.subscribe(one,target,()=>true);await flush();
  players[0].failed();players[0].failed();await flush();
  assert.equal(switched,1);assert.equal(players.length,2);assert.equal(players[0].destroyed,true);assert.equal(created.length,2);
  players[1].frame();await delay(30);await flush();assert.equal(target.states.at(-1),'live');assert.ok(renewed.some(item=>item.aliases?.length===1));
});

test('重连新video未解码时新消费者仍取得有界最后帧，不把新空video当画面',async()=>{
  const prototype=browser.HTMLCanvasElement.prototype,original=prototype.getContext;
  prototype.getContext=()=>({drawImage(){}});
  try{
    fixture({frame:false,timing:{retry:10}});pool.subscribe(one,sink(),()=>true);await flush();
    Object.defineProperties(players[0].video,{videoWidth:{configurable:true,value:2},videoHeight:{configurable:true,value:32767}});
    players[0].frame();players[0].failed();await delay(20);await flush();assert.equal(players.length,2);
    const target=sink();pool.subscribe(one,target,()=>true);await flush();assert.equal(target.frames[0].tagName,'CANVAS');
    assert.ok(target.frames[0].width<=1280&&target.frames[0].height<=720);assert.equal(target.states.at(-1),'reconnecting');
  }finally{pool.dispose();prototype.getContext=original;}
});
