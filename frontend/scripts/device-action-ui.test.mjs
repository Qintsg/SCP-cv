/** 首页/设置真实Ant电源与静音交互：旧反馈、防重和危险确认；无实体网络。 */
import assert from 'node:assert/strict';
import test from 'node:test';
import {mount,gate,json,respondWith,invalidate,flush,button,muteSwitch,decrease,calls,auth,runtime,toasts,dialog,browser,visibleText} from './device-actions/fixture.mjs';
/** :param page: 页面。:param action: 控件。:returns: 当前真实DOM节点。 */
function control(page,action){
 if(action==='mute')return muteSwitch();
 if(action==='power')return button('开机');
 if(page==='settings')return button('电视左 切换');
 const row=[...browser.document.querySelectorAll('.dashboard-power__row')].find(node=>visibleText(node.textContent).startsWith('电视左'));return row?.querySelector('button');
}
/** :returns: 实际业务写入，不把初始化读取/CSRF作为设备操作。 */
function writes(){return calls.filter(item=>item.method==='POST'&&item.path.startsWith('/api/devices/')||item.method==='PATCH'&&item.path==='/api/volume/');}
/** :param status: 注入响应。:returns: 当前业务HTTP门闩。 */
function holdWrites(status=200){const hold=gate();respondWith((path,options)=>options.method&&(path.startsWith('/api/devices/')||path==='/api/volume/')?hold.promise:Promise.resolve(json(path==='/api/video-wall/layout/'?{layout:{draft:null}}:path==='/api/volume/'?{volume:{level:50,muted:status===200,system_synced:false,backend:'simulation'}}:{csrfToken:'memory-token',success:true})));return async()=>{hold.resolve(json(status===200?{success:true,volume:{level:50,muted:true,system_synced:false,backend:'simulation'}}:{detail:'原始设备故障'},status));await flush();await flush();};}
for(const page of ['dashboard','settings'])for(const action of page==='dashboard'?['power','toggle','mute']:['power','toggle']){
 for(const change of ['route','host','account','relogin','unmount'])for(const status of [200,503])test(`${page} ${action} ${change}后旧${status}不串提示`,async()=>{
  await mount(page);const finish=holdWrites(status);const node=control(page,action);assert.ok(node);node.click();await flush();assert.equal(writes().length,1);
  await invalidate(change);toasts.clear();await finish();assert.deepEqual(toasts.items,[]);assert.equal(writes().length,1);
 });
 for(const status of [200,503])test(`${page} ${action}当前${status}保留原反馈并解除忙态`,async()=>{
  await mount(page);const finish=holdWrites(status);control(page,action).click();await flush();await finish();
  if(status===503)assert.match(toasts.items.at(-1)?.description||'',/原始设备故障/);
  else if(action!=='mute')assert.ok(toasts.items.length>0);
  assert.equal(control(page,action).disabled,false);assert.equal(writes().length,1);
 });
 test(`${page} ${action}同渲染轮双击及在途点击只发一次`,async()=>{
  await mount(page);const finish=holdWrites();const node=control(page,action);node.click();node.click();await flush();control(page,action).click();await flush();assert.equal(writes().length,1);
  await finish();
 });
}
for(const page of ['dashboard','settings']){
 test(`${page}关机确认前零HTTP，确认只发一次`,async()=>{
  await mount(page);const finish=holdWrites();button('关机').click();button('关机').click();await flush();assert.equal(writes().length,0);assert.equal(dialog.open,true);
  assert.equal(button('关机').disabled,false,'确认阶段须保留原生入口供Modal交接焦点');button('开机').click();await flush();assert.equal(writes().length,0,'背景按钮仍被逻辑意图防重');
  const confirm=button('确认关机',browser.document.querySelector('[role="dialog"]'));assert.ok(confirm);confirm.click();await flush();assert.equal(writes().length,1);assert.ok(writes()[0].path.endsWith('/power/off/'));
  assert.equal(button('关机').disabled,false,'loading期间保持Modal原生回焦');assert.equal(button('关机').getAttribute('aria-disabled'),'true');button('关机').click();await flush();assert.equal(writes().length,1,'可聚焦不等于可重复发送');await finish();
  assert.match(toasts.items.at(-1)?.description||'',/发送流程已完成/);assert.doesNotMatch(toasts.items.at(-1)?.description||'',/5\s*秒/);
 });
 for(const choice of ['cancel','host','account'])test(`${page}关机${choice}不发送`,async()=>{
  await mount(page);button('关机').click();await flush();assert.equal(dialog.open,true);
  if(choice==='cancel')button('取消',browser.document.querySelector('[role="dialog"]')).click();else await invalidate(choice);
  await flush();assert.equal(dialog.open,false);assert.equal(writes().length,0);assert.equal(toasts.items.length,0);
 });
}
test('静音在途禁止交错拖音量，完成后新调节保留静音',async()=>{
 await mount();const hold=gate();respondWith((path,options)=>writes().length===1?hold.promise:Promise.resolve(json({volume:{...JSON.parse(options.body),system_synced:false,backend:'simulation'}})));
 muteSwitch().click();await flush();await decrease();assert.equal(writes().length,1);
 hold.resolve(json({volume:{level:50,muted:true,system_synced:false,backend:'simulation'}}));await flush();await decrease();
 assert.equal(writes().length,2);assert.equal(JSON.parse(writes()[1].body).muted,true);
});
test('音量有在途/缓冲时不交错静音，最终输入发送后才可操作',async()=>{
 await mount();const hold=gate();respondWith((path,options)=>writes().length===1?hold.promise:Promise.resolve(json({volume:{...JSON.parse(options.body),system_synced:false,backend:'simulation'}})));
 await decrease();await decrease();muteSwitch().click();await flush();assert.equal(writes().length,1);
 hold.resolve(json({volume:{level:49,muted:false,system_synced:false,backend:'simulation'}}));await flush();await flush();assert.equal(writes().length,2);
 muteSwitch().click();await flush();assert.equal(writes().length,3);assert.equal(JSON.parse(writes()[2].body).muted,true);assert.equal(JSON.parse(writes()[2].body).level,48);
});
test('旧静音finally不解除不同后端中的新静音忙态',async()=>{
 await mount();const old=gate(),latest=gate();respondWith(()=>writes().length===1?old.promise:latest.promise);
 muteSwitch().click();await flush();await invalidate('host');muteSwitch().click();await flush();assert.equal(writes().length,2);
 old.resolve(json({detail:'旧静音故障'},503));await flush();assert.equal(muteSwitch().disabled,true);assert.equal(toasts.items.length,0);
 latest.resolve(json({volume:{level:50,muted:true,system_synced:false,backend:'simulation'}}));await flush();assert.equal(muteSwitch().disabled,false);
});
for(const page of ['dashboard','settings'])for(const action of page==='dashboard'?['power','toggle','mute']:['power','toggle'])for(const status of [200,503]){
 test(`${page} ${action}私有认证修订变化后旧${status}不反馈`,async()=>{
  await mount(page);const finish=holdWrites(status);control(page,action).click();await flush();await invalidate('passwordRevision');toasts.clear();await finish();assert.equal(toasts.items.length,0);assert.equal(writes().length,1);
 });
}
for(const page of ['dashboard','settings'])for(const action of ['power','toggle'])for(const status of [200,503]){
 test(`${page} ${action}旧${status}的finally不解锁新操作`,async()=>{
  await mount(page);const old=gate(),latest=gate();respondWith(()=>writes().length===1?old.promise:latest.promise);
  control(page,action).click();await flush();await invalidate('host');control(page,action).click();await flush();assert.equal(writes().length,2);
  old.resolve(json(status===200?{success:true}:{detail:'旧故障'},status));await flush();assert.equal(control(page,action).disabled,true);assert.equal(toasts.items.length,0);
  latest.resolve(json({success:true}));await flush();assert.equal(control(page,action).disabled,false);assert.equal(writes().length,2);
 });
}
for(const status of [200,503])test(`首页请求在途进入设置，同设备锁保持；原${status}只更新当前主机记录`,async()=>{
 await mount();const finish=holdWrites(status);button('开机').click();await flush();await invalidate('route');
 // 通过同一正式路由器打开共用Store的设置页，不新建认证/连接环境。
 const {router}=await import('./device-actions/fixture.mjs');await router.push('/power');await flush();assert.equal(button('开机').disabled,true);
 await finish();assert.equal(button('开机').disabled,false);assert.equal(toasts.items.length,0);assert.equal(writes().length,1);
});
for(const status of [200,503])test(`设置页签切换后旧电源${status}不串账户提示，返回后读当前记录`,async()=>{
 await mount('fullSettings');const tab=label=>[...browser.document.querySelectorAll('[role="tab"]')].find(node=>visibleText(node.textContent)===label);
 tab('设备电源').click();await flush();const finish=holdWrites(status);button('开机').click();await flush();assert.equal(writes().length,1);
 tab('账户').click();await flush();toasts.clear();await finish();assert.equal(toasts.items.length,0);
 tab('设备电源').click();await flush();assert.equal(button('开机').disabled,false);assert.equal(writes().length,1);
});
