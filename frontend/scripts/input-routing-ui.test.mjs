/** 正式A菜单/电视/发送进度的Vue与Ant回归；全部HTTP在内存截断，不是电脑输入。 */
import assert from 'node:assert/strict';
import test from 'node:test';
import {mount,gate,json,respondWith,invalidate,flush,button,calls,browser,loadModule,run,visibleText} from './device-actions/fixture.mjs';
const outputs = Object.fromEntries(['wall','tv_left','tv_right'].map(target=>[target,{target,status:'unknown',last_sent:null}]));
const inputs = Array.from({length:24},(_,index)=>({id:`workstation:D${Math.floor(index/4)+1}-${index%4+1}`,name:`D${Math.floor(index/4)+1}-${index%4+1}`,group:'workstation',ip_suffix:index+51,
  preview_input:{kind:index>=12&&index<=15?`window_${index-11}`:'ip_stream',ip_address:index>=12&&index<=15?'':`192.168.5.${index+51}`},source_id:index+1,availability:'reachable',is_available:true,checked_at:null,aliases:[]}));
/** :param path: 正式API。:returns: 仅内存读响应，设备写必须由用例显式拦截。 */
function normal(path){return json(path.startsWith('/api/inputs')?{success:true,inputs}:path==='/api/display-routing/'?{success:true,outputs}:path==='/api/video-wall/layout/'?{layout:{draft:null}}:path==='/api/auth/csrf/'?{csrfToken:'memory-token'}:{success:true});}
/** :returns: 正式输入Store，与当前页面共用Pinia。 */
async function catalog(){return(await loadModule('/src/stores/inputCatalog.ts')).useInputCatalogStore();}
test('只读目录GET在途时显式展开会升级为一次合并刷新，不漏探测',async()=>{
  await mount();const store=await catalog(),hold=gate();respondWith(path=>path==='/api/inputs/'?hold.promise:Promise.resolve(normal(path)));
  const reading=run(store.load()),refreshing=run(store.load(true));await flush();assert.equal(calls.filter(item=>item.path==='/api/inputs/refresh/').length,0);
  hold.resolve(json({inputs}));await reading;await refreshing;assert.equal(calls.filter(item=>item.path==='/api/inputs/refresh/').length,1);assert.equal(store.items.length,24);
});
test('GET升级等待中换认证，不借新认证继续旧刷新意图',async()=>{
  await mount();const store=await catalog(),hold=gate();respondWith(path=>path==='/api/inputs/'?hold.promise:Promise.resolve(normal(path)));
  const reading=run(store.load()),refreshing=run(store.load(true));await invalidate('relogin');hold.resolve(json({inputs}));await reading;await refreshing;
  assert.equal(calls.filter(item=>item.path==='/api/inputs/refresh/').length,0);assert.deepEqual(store.items,[]);
});
test('正式A其他输入二级菜单含搜索/滚动，选择只改表单不发设备包',async()=>{
  await mount();respondWith(path=>Promise.resolve(normal(path)));browser.document.querySelector('button[aria-label="全屏输入来源"],button[aria-label="大屏左输入来源"]').click();await flush();
  button('其他输入').click();await flush();await flush();
  assert.ok(browser.document.querySelector('input[aria-label="搜索其他输入"]'));assert.equal(browser.document.querySelectorAll('.input-source-list>button').length,24);
  const search=browser.document.querySelector('input[aria-label="搜索其他输入"]');search.value='D6-4';search.dispatchEvent(new browser.Event('input',{bubbles:true}));await flush();
  assert.equal(browser.document.querySelectorAll('.input-source-list>button').length,1);assert.match(browser.document.querySelector('.input-source-list').textContent,/D6-4.*IP74/s);
  browser.document.querySelector('.input-source-list>button').click();await flush();
  assert.equal(calls.filter(item=>item.path.includes('/devices/')||item.path.includes('/televisions/')||item.path.endsWith('/layout/apply/')).length,0);
});
test('换认证关闭来源弹层并清除旧自定义IP意图',async()=>{
  await mount();respondWith(path=>Promise.resolve(normal(path)));const trigger=browser.document.querySelector('button[aria-label="全屏输入来源"],button[aria-label="大屏左输入来源"]');trigger.click();await flush();button('自定义 IP').click();await flush();
  const input=browser.document.querySelector('input[aria-label="自定义输入IPv4"]');input.value='192.168.5.74';input.dispatchEvent(new browser.Event('input',{bubbles:true}));await invalidate('relogin');
  const currentTrigger=browser.document.querySelector('button[aria-label="全屏输入来源"],button[aria-label="大屏左输入来源"]');
  assert.notEqual(currentTrigger,trigger,'墙面归属变化已重建编辑器，不能检查已脱离文档的旧按钮');
  assert.equal(currentTrigger.getAttribute('aria-expanded'),'false');
  assert.equal(calls.filter(item=>item.path.includes('/devices/')||item.path.includes('/televisions/')).length,0);
});
test('两次手动应用同一电视输入均force发送，另一电视不受影响',async()=>{
  await mount();respondWith((path,options)=>Promise.resolve(path.includes('/televisions/')?json({success:true,output:{...outputs.tv_left,status:'sent_unconfirmed',last_sent:{input:JSON.parse(options.body).input}},result:'sent_unconfirmed'}):normal(path)));
  const apply=()=>browser.document.querySelector('button[aria-label="应用电视左内容"]');apply().click();await flush();await flush();apply().click();await flush();
  const writes=calls.filter(item=>item.path.includes('/televisions/'));assert.equal(writes.length,2);assert.ok(writes.every(item=>item.path.endsWith('/tv_left/')&&JSON.parse(item.body).force));
});
test('Popover缓存关闭后再选快捷来源，自定义输入按当前值重新建立而非旧default-value',async()=>{
  await mount();respondWith(path=>Promise.resolve(normal(path)));
  const trigger=browser.document.querySelector('button[aria-label="全屏输入来源"],button[aria-label="大屏左输入来源"]');trigger.click();await flush();button('自定义 IP').click();await flush();
  const field=browser.document.querySelector('input[aria-label="自定义输入IPv4"]');field.value='192.168.5.74';field.dispatchEvent(new browser.Event('input',{bubbles:true}));await flush();
  trigger.click();await flush();trigger.click();await flush();
  const menu=browser.document.getElementById(trigger.getAttribute('aria-controls'));[...menu.querySelectorAll('.input-source-roots>button')].find(node=>node.querySelector('span')?.textContent==='窗口 2').click();await flush();
  trigger.click();await flush();button('自定义 IP').click();await flush();
  assert.equal(browser.document.querySelector('input[aria-label="自定义输入IPv4"]').value,'');
  assert.equal(calls.filter(item=>item.path.includes('/devices/')||item.path.includes('/televisions/')).length,0);
});
for(const change of ['host','relogin','passwordRevision','route'])test(`电视${change}后旧失败不覆盖新环境反馈`,async()=>{
  await mount();const hold=gate();respondWith(path=>path.includes('/televisions/')?hold.promise:Promise.resolve(normal(path)));
  browser.document.querySelector('button[aria-label="应用电视左内容"]').click();await flush();await invalidate(change);hold.resolve(json({detail:'旧电视请求故障'},503));await flush();await flush();
  assert.doesNotMatch(browser.document.body.textContent,/旧电视请求故障/);assert.equal(calls.filter(item=>item.path.includes('/televisions/')).length,1);
});
test('拼接屏按钮内只有非数值发送进度，原意图锁仍拒绝双击',async()=>{
  await mount();const hold=gate();respondWith(path=>path.includes('/devices/')?hold.promise:Promise.resolve(normal(path)));
  button('开机').click();button('开机').click();await flush();
  const progress=browser.document.querySelector('.dashboard-power__action [role="progressbar"]');assert.ok(progress);assert.equal(progress.hasAttribute('aria-valuenow'),false);assert.match(progress.getAttribute('aria-label'),/指令发送中/);
  assert.equal(calls.filter(item=>item.path.includes('/devices/')).length,1);hold.resolve(json({success:true}));await flush();assert.equal(browser.document.querySelector('.dashboard-power__progress'),null);
});
