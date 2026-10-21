// 输入主组播分辨率只读查询回归；仅字节和回环，不访问现场。
const {test}=require('node:test');
const assert=require('node:assert/strict');
const net=require('node:net');
const {QUERY,INPUT_SUFFIXES,ReplyDecoder,queryInput}=require('./protocol.cjs');

/** 构造已留存的611A回读。:param suffix: 固定末段。:returns: 22字节响应。 */
function reply(suffix=63){
 const b=Buffer.from('fcfd611a0000000ae001013f13880f000870e3cafeff','hex');b[11]=suffix;return b;
}

test('固定查询精确12字节且目录排除75和输出节点',()=>{
 assert.equal(QUERY.toString('hex'),'fbfc611a000000000000fdfe');
 assert.equal(INPUT_SUFFIXES.length,31);assert.equal(new Set(INPUT_SUFFIXES).size,31);
 assert(!INPUT_SUFFIXES.includes(75));assert(!INPUT_SUFFIXES.includes(101));
});
test('逐字节半包、前导垃圾、粘包与错误来源不串尺寸',()=>{
 const d=new ReplyDecoder(63),bytes=Buffer.concat([Buffer.from('noise'),reply(64),reply(63)]);let found;
 for(const b of bytes)found=d.feed(Buffer.from([b]))||found;
 assert.equal(found.group,'224.1.1.63');assert.equal(found.width,3840);assert.equal(found.height,2160);
});
for(const [name,offset,value] of [['命令',3,0x20],['控制位',4,1],['长度',7,9],['组播',8,192],['端口',13,0x89],['包尾',21,0],['宽为零',14,0]]){
 test(`拒绝${name}不符`,()=>{
  const b=reply();b[offset]=value;if(name==='宽为零')b[15]=0;
  assert.equal(new ReplyDecoder(63).feed(b),null);
 });
}
test('未确认校验算法不虚称CRC；原两字节保留',()=>{
 const b=reply();b[18]=0x12;b[19]=0x34;const r=new ReplyDecoder(63).feed(b);
 assert.equal(r.reply_hex,b.toString('hex'));assert.equal(r.checksum_verified,false);
});
test('累计接收预算有界',()=>{
 const d=new ReplyDecoder(63);assert.throws(()=>d.feed(Buffer.alloc(65537)),/budget/);
});
test('未知设备在建立连接前拒绝',async()=>{
 let calls=0;await assert.rejects(queryInput(75,{connect:()=>{calls++;}}),/固定输入/);assert.equal(calls,0);
});

/** 运行TCP回环，记录请求和关闭。:param handler: 服务端行为。:param run: 查询断言。 */
async function loopback(handler,run){
 const sockets=new Set();const server=net.createServer(socket=>{sockets.add(socket);socket.on('close',()=>sockets.delete(socket));handler(socket);});
 await new Promise(resolve=>server.listen(0,'127.0.0.1',resolve));
 const port=server.address().port;
 try{await run(options=>net.createConnection({...options,host:'127.0.0.1',port}));}
 finally{for(const s of sockets)s.destroy();await new Promise(resolve=>server.close(resolve));}
}
test('TCP仅写固定查询，半包匹配后关闭连接',async()=>{
 let request=Buffer.alloc(0),connection;
 await loopback(socket=>{connection=socket;socket.on('data',b=>{request=Buffer.concat([request,b]);socket.write(reply().subarray(0,9));setImmediate(()=>socket.write(reply().subarray(9)));});},async connect=>{
  const result=await queryInput(63,{connect});assert.equal(result.status,'matched');assert.equal(result.width,3840);
  await new Promise(resolve=>connection.destroyed?resolve():connection.once('close',resolve));
 });assert.deepEqual(request,QUERY);
});
test('静默节点超时、畸形回复关闭，不自动重试',async()=>{
 let calls=0;await loopback(()=>{calls++;},async connect=>{
  const r=await queryInput(63,{connect,timeoutMs:30});assert.equal(r.status,'timeout');
 });assert.equal(calls,1);
 await loopback(s=>s.end(reply(64)),async connect=>assert.equal((await queryInput(63,{connect})).status,'invalid_reply'));
});
