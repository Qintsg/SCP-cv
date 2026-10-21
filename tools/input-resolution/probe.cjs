// 独立只读诊断入口：逐路611A，不修改配置、不拉视频、不发送墙/电视控制。
const {INPUT_SUFFIXES,queryInput}=require('./protocol.cjs');

/** 调查固定31路并输出JSON。:returns: 完整调查；离线仍保留独立状态。 */
async function main(){
 if(process.argv.length!==2)throw new Error('本工具不接受目标/写入参数；请直接运行node tools/input-resolution/probe.cjs。');
 const started_at=new Date().toISOString(),results=[];
 for(const suffix of INPUT_SUFFIXES)results.push(await queryInput(suffix));
 process.stdout.write(JSON.stringify({started_at,finished_at:new Date().toISOString(),method:'611A input primary multicast configuration',read_only:true,results},null,2)+'\n');
}
if(require.main===module)main().catch(error=>{process.stderr.write(error.message+'\n');process.exitCode=1;});
