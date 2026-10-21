/** 当前一路摄像机预览/固定监控禁控；正式组件、内存HTTP，非真实设备出画或系统输入。 */
import assert from 'node:assert/strict';
import test from 'node:test';
import {mount,cameras,button,flush,browser,calls,respondWith,json,venue,loadModule} from './venue-actions/fixture.mjs';
test('只请求所选摄像机，监控可预览但没有云台控制',async()=>{
  const fixed={...cameras[0],id:'fixed',name:'监控01',path:'/Camera/监控01',source_id:76,movable:false,control_available:false};
  cameras.push(fixed);
  try{
    await mount('page');
    assert.equal(browser.document.querySelectorAll('.venue-camera-preview .client-preview').length,1);
    assert.match(browser.document.querySelector('.venue-camera-console').textContent,/中摄像机1/);
    const before=calls.filter(item=>item.path==='/api/stream-previews/'&&item.method==='POST');assert.ok(before.every(item=>item.body.source_id===1));
    respondWith(path=>Promise.resolve(path==='/api/venue/'?json(venue()):path==='/api/stream-previews/'?json({code:'preview_no_stream',detail:'本机无预览设备'},422):json({success:true})));
    button('预览画面').click();await flush();await flush();
    assert.equal(browser.document.querySelectorAll('.venue-camera-preview .client-preview').length,1);
    assert.match(browser.document.querySelector('.venue-camera-console').textContent,/监控01/);
    assert.equal(browser.document.querySelector('[aria-label*="监控01向上"]'),null);
    assert.equal(browser.document.querySelector('[aria-label*="中摄像机1向上"]'),null);
    const previews=calls.filter(item=>item.path==='/api/stream-previews/'&&item.method==='POST');assert.equal(previews.at(-1).body.source_id,76);
    assert.equal(previews.some(item=>item.body.source_id===2),false);assert.equal(calls.filter(item=>item.path.includes('/cameras/')).length,0);
  }finally{
    cameras.pop();(await loadModule('/src/features/preview/previewPool.ts')).getPreviewPool().dispose();
  }
});
