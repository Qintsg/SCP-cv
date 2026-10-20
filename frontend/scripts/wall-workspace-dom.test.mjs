/** 真实Vue/Ant的网格/短码交互；仅内存DOM事件，不产生电脑输入或设备请求。 */
import assert from 'node:assert/strict';
import test,{before,after,afterEach} from 'node:test';
import {setImmediate as nextTurn} from 'node:timers/promises';
import {Window} from 'happy-dom';
import {createDomTestServer,visibleText} from './dom-test-server.mjs';
const browser=new Window({url:'http://localhost:5173/dashboard'});
const names=['window','document','navigator','HTMLElement','HTMLInputElement','HTMLTextAreaElement','Element','Node','SVGElement','Document','MutationObserver','ResizeObserver','Image','InputEvent','getComputedStyle','requestAnimationFrame','cancelAnimationFrame','sessionStorage'];
const globals=new Map(names.map(name=>[name,Object.getOwnPropertyDescriptor(globalThis,name)]));
for(const name of names)Object.defineProperty(globalThis,name,{configurable:true,writable:true,value:typeof browser[name]==='function'&&/^[a-z]/.test(name)?browser[name].bind(browser):browser[name]});
const originalFetch=globalThis.fetch;
let server,vue,Grid,Composer,codec,app,value;
before(async()=>{vue=await import('vue');server=await createDomTestServer();({default:Grid}=await server.ssrLoadModule('/src/features/runtime/wall/WallGridEditor.vue'));({default:Composer}=await server.ssrLoadModule('/src/features/runtime/wall/WallLayoutComposer.vue'));codec=await server.ssrLoadModule('/src/features/runtime/wall/layoutCode.ts');});
afterEach(async()=>{app?.unmount();await flush();browser.document.body.replaceChildren();await browser.happyDOM.abort();globalThis.fetch=originalFetch;});
after(async()=>{await server.close();await browser.happyDOM.close();for(const[name,descriptor]of globals){if(descriptor)Object.defineProperty(globalThis,name,descriptor);else delete globalThis[name];}});
/** :returns: 完成Vue与HTTP微任务。 */
async function flush(){await vue.nextTick();await nextTurn();await vue.nextTick();}
/** :param text: 公开按钮文字。:returns: 真实Ant/原生按钮。 */
function button(text){return [...browser.document.querySelectorAll('button')].find(node=>visibleText(node.textContent)===visibleText(text));}
/** :param kind: 网格或完整编辑器。:param initial: 本地数据。:returns: 正式组件，任何网络均失败测试。 */
async function mount(kind='grid',initial=[]){value=vue.ref(initial);globalThis.fetch=()=>{assert.fail('此编辑不应请求设备/Host');};const host=browser.document.createElement('div');browser.document.body.append(host);app=vue.createApp({render:()=>vue.h(kind==='grid'?Grid:Composer,{modelValue:value.value,'onUpdate:modelValue':next=>{value.value=next;}})});app.mount(host);await flush();}
/** :param x: 横坐标。:param y: 纵坐标。:returns: 本地指针参数。 */
function pointer(type,x,y,target){target.dispatchEvent(new browser.PointerEvent(type,{bubbles:true,button:0,pointerId:1,clientX:x,clientY:y}));}
/** :returns: 为内存DOM提供精确几何；不是系统窗口。 */
function gridGeometry(){const grid=browser.document.querySelector('.wall-grid');grid.getBoundingClientRect=()=>({left:0,top:0,width:1000,height:500,right:1000,bottom:500});return grid;}
test('拖选矩形→选择来源→块预览→删除回到空格，无网络',async()=>{
  await mount();assert.doesNotMatch(browser.document.body.textContent,/在空白区域拖选矩形/);assert.equal(browser.document.querySelector('.wall-grid__footer'),null);
  const grid=gridGeometry();pointer('pointerdown',10,10,grid);pointer('pointermove',290,290,grid);pointer('pointerup',290,290,grid);await flush();
  assert.match(browser.document.body.textContent,/已选 3列 × 3行/);button('选择窗口').click();await flush();button('确定').click();await flush();
  assert.deepEqual(JSON.parse(JSON.stringify(value.value)),[{region:'grid',rect:{column:0,row:0,columns:3,rows:3},input:{kind:'window_1',ip_address:''}}]);
  browser.document.querySelector('.wall-grid__block').click();await flush();button('删除显示块').click();await flush();assert.equal(value.value.length,0);assert.equal(browser.document.querySelectorAll('.wall-grid__cell:disabled').length,0);
});
test('跨越已有块的矩形整体拒绝，不覆盖、不出现选源动作',async()=>{
  await mount('grid',[{region:'grid',rect:{column:2,row:2,columns:2,rows:2},input:{kind:'window_1'}}]);const grid=gridGeometry();pointer('pointerdown',10,10,grid);pointer('pointermove',490,490,grid);pointer('pointerup',490,490,grid);await flush();assert.equal(value.value.length,1);assert.match(browser.document.body.textContent,/不能覆盖已有显示块/);assert.equal(button('选择窗口'),undefined);
});
test('输入短码整体替换本地布局，损坏代码保留原值，不自动应用',async()=>{
  await mount('composer',[{region:'grid',rect:{column:0,row:0,columns:5,rows:5},input:{kind:'window_1'}}]);
  const expected=[{region:'grid',rect:{column:5,row:0,columns:5,rows:5},input:{kind:'window_2'}}],code=codec.encodeLayout(expected);
  const input=browser.document.querySelector('input[aria-label="自定义布局代码"]');input.value=code;input.dispatchEvent(new browser.Event('input',{bubbles:true}));await flush();button('读取代码').click();await flush();assert.deepEqual(JSON.parse(JSON.stringify(value.value)),codec.decodeLayout(code));
  input.value='broken!';input.dispatchEvent(new browser.Event('input',{bubbles:true}));await flush();button('读取代码').click();await flush();assert.match(browser.document.body.textContent,/代码格式无效/);assert.deepEqual(JSON.parse(JSON.stringify(value.value)),codec.decodeLayout(code));
});
test('自定义块添加和删除后代码实时更新且仍可独立解析',async()=>{
  await mount('composer',[]);const initial=browser.document.querySelector('input[aria-label="自定义布局代码"]').value;
  const grid=gridGeometry();pointer('pointerdown',10,10,grid);pointer('pointermove',190,190,grid);pointer('pointerup',190,190,grid);await flush();button('选择窗口').click();await flush();button('确定').click();await flush();
  const updated=browser.document.querySelector('input[aria-label="自定义布局代码"]').value;assert.notEqual(updated,initial);assert.equal(updated,codec.encodeLayout(value.value));
  browser.document.querySelector('.wall-grid__block').click();await flush();button('删除显示块').click();await flush();assert.equal(browser.document.querySelector('input[aria-label="自定义布局代码"]').value,initial);
});
test('Shift方向键框选后Enter选源，保持完整矩形而非仅当前单元格',async()=>{
  await mount();const first=browser.document.querySelector('[data-cell="0"]');first.focus();
  first.dispatchEvent(new browser.KeyboardEvent('keydown',{key:'ArrowRight',shiftKey:true,bubbles:true,cancelable:true}));await flush();
  browser.document.activeElement.dispatchEvent(new browser.KeyboardEvent('keydown',{key:'Enter',bubbles:true,cancelable:true}));await flush();button('确定').click();await flush();
  assert.deepEqual(JSON.parse(JSON.stringify(value.value[0].rect)),{column:0,row:0,columns:2,rows:1});
});
