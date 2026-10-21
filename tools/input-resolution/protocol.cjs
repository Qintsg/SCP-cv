// 现场输入主组播分辨率：原客户端固定只读611A，不包含任何配置/映射写帧。
const net=require('node:net');
const QUERY_HEX='fbfc611a000000000000fdfe';
const INPUT_SUFFIXES=Object.freeze([...Array.from({length:24},(_,i)=>i+51),76,81,84,85,86,87,88]);

class ReplyDecoder{
 /** 创建有界解析器。:param suffix: 预期输入末段。 */
 constructor(suffix){this.suffix=suffix;this.buffer=Buffer.alloc(0);this.received=0;}
 /** 增量读取固定回读。:param chunk: TCP片段。:returns: 对应主组播尺寸或null。 */
 feed(chunk){
  this.received+=chunk.length;if(this.received>65536)throw new Error('budget');
  this.buffer=Buffer.concat([this.buffer,chunk]);
  while(this.buffer.length>=22){
   const b=this.buffer;
   if(b.readUInt32BE(0)===0xfcfd611a && b.readUInt16BE(4)===0 && b.readUInt16BE(6)===10 && b.readUInt16BE(20)===0xfeff){
    this.buffer=b.subarray(22);
    const group=[...b.subarray(8,12)].join('.'),port=b.readUInt16BE(12),width=b.readUInt16BE(14),height=b.readUInt16BE(16);
    if(group===`224.1.1.${this.suffix}`&&port===5000&&width>0&&height>0)
     return {group,port,width,height,reply_hex:b.subarray(0,22).toString('hex'),checksum_verified:false};
   }else this.buffer=b.subarray(1);
  }
  return null;
 }
}

/** 查询固定目录一路，失败不猜尺寸。:param suffix: 已登记末段。:param options: 有界时限及测试连接工厂。:returns: 状态、时间和已匹配元数据。 */
async function queryInput(suffix,{timeoutMs=1500,connect=net.createConnection}={}){
 if(!INPUT_SUFFIXES.includes(suffix))throw new Error('只允许固定输入目录，不查询75或输出节点。');
 if(!Number.isInteger(timeoutMs)||timeoutMs<1||timeoutMs>1500)throw new Error('查询时限必须在1–1500ms内。');
 return new Promise(resolve=>{
  const decoder=new ReplyDecoder(suffix),started=Date.now();let done=false,received=false;
  const socket=connect({host:`192.168.5.${suffix}`,port:4830});
  const timer=setTimeout(()=>finish('timeout'),timeoutMs);
  /** 结束并释放连接。:param status: 结果。:param detail: 匹配的分辨率字段。 */
  function finish(status,detail={}){
   if(done)return;done=true;clearTimeout(timer);socket.destroy();
   resolve({suffix,status,observed_at:new Date().toISOString(),elapsed_ms:Date.now()-started,...detail});
  }
  socket.once('connect',()=>socket.write(Buffer.from(QUERY_HEX,'hex')));
  socket.once('error',e=>finish(e.code||'socket_error'));
  socket.once('close',()=>finish(received?'invalid_reply':'closed'));
  socket.on('data',chunk=>{
   received=true;
   try{const detail=decoder.feed(chunk);if(detail)finish('matched',detail);}
   catch{finish('budget');}
  });
 });
}
module.exports={QUERY:Buffer.from(QUERY_HEX,'hex'),INPUT_SUFFIXES,ReplyDecoder,queryInput};
