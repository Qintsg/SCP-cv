/** 本机预览核心/HTTP门禁：虚构进程与回环HTTP，不连接现场。 */
import assert from 'node:assert/strict';
import test from 'node:test';
import { EventEmitter } from 'node:events';
import { PassThrough } from 'node:stream';
import { createPreviewPool, validateStream } from './preview-core.mjs';
import { createPreviewServer } from './server.mjs';
/** :returns: 只产生固定JPEG边界的本机内存进程，不启动任何外部程序。 */
function fixture() {
  const children = [], calls = [];
  const spawn = (file, args, options) => {
    const child = new EventEmitter(); child.stdout = new PassThrough(); child.killed = false;
    child.kill = () => { child.killed = true; queueMicrotask(() => child.emit('exit', 0)); };
    children.push(child); calls.push({ file, args, options });
    setImmediate(() => child.stdout.write(Buffer.from([255,216,1,2,3,255,217]))); return child;
  };
  return { children, calls, spawn };
}
test('地址仅允许明确本地IPv4 RTSP/RTMP，拒绝文件/公网/DNS/控制字符', () => {
  for (const value of ['file:///secret', 'http://127.0.0.1/x', 'rtsp://example.org/x', 'rtsp://8.8.8.8/x', 'rtsp://127.0.0.1/a\n']) assert.throws(() => validateStream(value));
  assert.ok(validateStream('rtsp://fixture:pass@192.168.1.2:554/live').includes('192.168.1.2'));
});
test('同源共享进程，低/高清独立，无声无shell，最后释放清理', async () => {
  const f = fixture(), pool = createPreviewPool({ startProcess: f.spawn });
  try {
    const [first, second] = await Promise.all([pool.open('rtsp://127.0.0.1:15554/live/chn1','low'),pool.open('rtsp://127.0.0.1:15554/live/chn1','low')]);
    const high = await pool.open('rtsp://127.0.0.1:15554/live/chn0','high');
    assert.deepEqual(pool.stats(),{ workers:2,sessions:3 }); assert.equal(pool.frame(first)[0],255);
    for (const call of f.calls) { assert.equal(call.options.shell,false); assert.equal(call.options.windowsHide,true); assert.ok(call.args.includes('-an')); }
    assert.ok(f.calls[0].args.includes('-timeout')); assert.equal(f.calls[0].args.includes('-rw_timeout'),false,'RTSP demuxer不接受通用rw_timeout参数');
    pool.release(first); assert.equal(f.children[0].killed,false); pool.release(second); assert.equal(f.children[0].killed,true); pool.release(high);
    assert.deepEqual(pool.stats(),{ workers:0,sessions:0 });
  } finally { pool.close(); }
});
test('停帧/租约/容量限制均释放或明确拒绝', async () => {
  const f = fixture(); let now = 0; const pool = createPreviewPool({ startProcess:f.spawn,maximum:1,now:()=>now,staleMs:10,leaseMs:20 });
  try { const id = await pool.open('rtmp://127.0.0.1:1935/live/chn1','low');
    await assert.rejects(pool.open('rtmp://127.0.0.1:1935/live/chn0','high'));
    now=11; assert.throws(()=>pool.frame(id)); assert.equal(f.children[0].killed,true);
    const next = await pool.open('rtmp://127.0.0.1:1935/live/chn0','high'); now=40; pool.sweep(); assert.throws(()=>pool.frame(next));
  } finally { pool.close(); }
});
test('HTTP拒绝错误Origin/token，错误不回显凭据，帧失败不能返回200', async () => {
  const pool={open:async()=>{throw Error('fixture:secret@source');},frame:()=>{throw Error('fixture:secret@source');},release:()=>{},sweep:()=>{},close:()=>{}};
  const server=createPreviewServer({origins:['https://console.invalid'],token:'fixture',pool,port:18766});
  await new Promise(resolve=>server.listen(18766,'127.0.0.1',resolve));
  try {
    const path='http://127.0.0.1:18766/sessions/'+ 'a'.repeat(32)+'/frame';
    assert.equal((await fetch(path)).status,403);
    assert.equal((await fetch(path,{headers:{Origin:'https://console.invalid'}})).status,401);
    const response=await fetch(path,{headers:{Origin:'https://console.invalid',Authorization:'Bearer fixture'}});
    assert.equal(response.status,503); assert.ok(!(await response.text()).includes('secret'));
  } finally { server.closeAllConnections(); await new Promise(resolve=>server.close(resolve)); }
});
test('不把封装客户端的不透明null来源或含路径凭据URL当作网页Origin', () => {
  for(const origin of ['file:///local','app://scp-cv','https://user:pass@console.invalid','https://console.invalid/path'])
    assert.throws(()=>createPreviewServer({origins:[origin],token:'fixture'}));
});
test('50格加首页卡片可共享同一上游，首帧并发也不能穿透64订阅预算', async () => {
  const f=fixture(),pool=createPreviewPool({startProcess:f.spawn});
  try{const results=await Promise.allSettled(Array.from({length:65},()=>pool.open('rtsp://127.0.0.1:15554/live/chn1','low')));
    assert.equal(results.filter(item=>item.status==='fulfilled').length,64);assert.equal(results.filter(item=>item.status==='rejected').length,1);
    assert.deepEqual(pool.stats(),{workers:1,sessions:64});
  }finally{pool.close();assert.ok(f.children.every(child=>child.killed));}
});
