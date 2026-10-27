/*
 * 已确认空输出的真实Vue/Pinia/Naive UI导航保护；只替换外部HTTP，不模拟组件内部行为。
 * @Project : SCP-cv
 * @File : presentation-empty-output.test.mjs
 * @Author : Qintsg
 * @Date : 2026-10-03
 */
import { browserKeyCode, visibleText } from './dom-test-server.mjs';

import assert from 'node:assert/strict';
import test from 'node:test';
import {browser,PlaybackControl,PptFocusView,mountPreview,flushUi} from './presentation-preview/fixture.mjs';

/** :param label: 可见导航名称。:returns: 真实按钮，不读取组件私有状态。 */
function button(label){const value=[...browser.document.querySelectorAll('button')].find(item=>visibleText(item.textContent) === visibleText(label));assert.ok(value,label);return value;}

test('确认无实际文稿时禁用两颗步进按钮，不发送导航POST',async()=>{
  await mountPreview(PlaybackControl,{observation:{actual_source_id:null,actual_fit_text:null,actual_adapter_kind:'',
    playback_mode:'',playback_state:'error',error_message:'候选切入失败，确认空输出',current_slide:0,total_slides:0}});
  const previousFetch=globalThis.fetch;const posts=[];
  globalThis.fetch=async(url,options={})=>{if(options.method==='POST')posts.push(String(url));return previousFetch(url,options);};
  for(const label of ['上一步','下一步']){assert.equal(button(label).disabled,true,label);button(label).click();}
  await flushUi();assert.deepEqual(posts,[]);
});

test('专注页确认无实际源时方向键不发送导航POST',async()=>{
  await mountPreview(PptFocusView,{observation:{actual_source_id:null,actual_fit_text:null,actual_adapter_kind:'',
    playback_mode:'',playback_state:'error',current_slide:0,total_slides:0}});
  const previousFetch=globalThis.fetch;const posts=[];
  globalThis.fetch=async(url,options={})=>{if(options.method==='POST'){posts.push(String(url));return new Response(JSON.stringify({success:true,sessions:[]}),{headers:{'Content-Type':'application/json'}});}return previousFetch(url,options);};
  browser.document.body.dispatchEvent(new browser.KeyboardEvent('keydown',{ keyCode: browserKeyCode('ArrowRight'), which: browserKeyCode('ArrowRight'), key:'ArrowRight',bubbles:true,cancelable:true}));
  await flushUi();assert.deepEqual(posts,[]);
});

test('已观测实际文稿恢复后翻页重新可用，不用空输出错误永久锁住控制',async()=>{
  await mountPreview(PlaybackControl,{observation:{actual_source_id:40,actual_fit_text:true,actual_adapter_kind:'libreoffice',
    playback_mode:'libreoffice',playback_state:'playing',current_slide:1,total_slides:7}});
  for(const label of ['上一步','下一步'])assert.equal(button(label).disabled,false,label);
});

test('专注页请求文稿但实际为VLC时方向键不发送导航POST',async()=>{
  await mountPreview(PptFocusView,{observation:{actual_source_id:40,actual_adapter_kind:'vlc',actual_fit_text:null}});
  const previousFetch=globalThis.fetch;const posts=[];
  globalThis.fetch=async(url,options={})=>{if(options.method==='POST'){posts.push(String(url));return new Response(JSON.stringify({success:true,sessions:[]}),{headers:{'Content-Type':'application/json'}});}return previousFetch(url,options);};
  browser.document.body.dispatchEvent(new browser.KeyboardEvent('keydown',{ keyCode: browserKeyCode('ArrowRight'), which: browserKeyCode('ArrowRight'), key:'ArrowRight',bubbles:true,cancelable:true}));
  await flushUi();assert.deepEqual(posts,[]);
});
