/** 实际Vite dev/preview的CORS权威回归；仅随机回环假后端，不连接D4或发送设备命令。 */
import assert from 'node:assert/strict';
import test from 'node:test';
import {createServer as httpServer} from 'node:http';
import {mkdtemp,writeFile,rm} from 'node:fs/promises';
import {tmpdir} from 'node:os';
import {join,resolve,basename,sep} from 'node:path';
import {fileURLToPath} from 'node:url';
import {loadConfigFromFile,createServer,preview} from 'vite';
const configFile=fileURLToPath(new URL('../vite.config.ts',import.meta.url));
const loaded=await loadConfigFromFile({command:'build',mode:'web'},configFile);
assert.ok(loaded,'使用仓库正式配置，而非重复硬编码配置');
for(const mode of ['server','preview'])test(`${mode}预检交由ControlHost精确来源策略，不被Vite默认CORS吞掉`,async()=>{
  let requests=0,frontend;const directory=await mkdtemp(join(tmpdir(),'scp-cv-cors020-'));
  const backend=httpServer((request,response)=>{
    requests++;assert.equal(request.url,'/api/stream-previews/native/resolve/');assert.equal(request.method,'OPTIONS');
    if(request.headers.origin!=='app://scp-cv'){response.writeHead(403);response.end();return;}
    response.writeHead(204,{'Access-Control-Allow-Origin':'app://scp-cv','Access-Control-Allow-Credentials':'true','Access-Control-Allow-Methods':'POST','Access-Control-Allow-Headers':'Content-Type,X-CSRFToken'});response.end();
  });
  try{
    await new Promise(done=>backend.listen(0,'127.0.0.1',done));
    await writeFile(join(directory,'index.html'),'<!doctype html><title>只读回环夹具</title>','utf8');
    const server={host:'127.0.0.1',port:0,strictPort:true,cors:loaded.config[mode]?.cors,proxy:{'/api':{target:`http://127.0.0.1:${backend.address().port}`,changeOrigin:false}},hmr:false,ws:false,watch:null};
    const config={configFile:false,envDir:false,root:directory,build:{outDir:directory},server,preview:server,logLevel:'error'};
    frontend=mode==='server'?await createServer(config):await preview(config);
    if(mode==='server')await frontend.listen();
    const url=`http://127.0.0.1:${frontend.httpServer.address().port}/api/stream-previews/native/resolve/`;
    const response=await fetch(url,{method:'OPTIONS',headers:{Origin:'app://scp-cv','Access-Control-Request-Method':'POST','Access-Control-Request-Headers':'content-type,x-csrftoken'}});
    assert.equal(response.status,204);assert.equal(response.headers.get('access-control-allow-origin'),'app://scp-cv');assert.equal(response.headers.get('access-control-allow-credentials'),'true');
    const rejected=await fetch(url,{method:'OPTIONS',headers:{Origin:'app://forged','Access-Control-Request-Method':'POST'}});
    assert.equal(rejected.status,403);assert.equal(rejected.headers.get('access-control-allow-origin'),null);assert.equal(requests,2,'两次预检必须真的抵达后端');
  }finally{
    if(frontend){frontend.httpServer.closeAllConnections();if(mode==='server')await frontend.close();else{await frontend.close();}}
    backend.closeAllConnections();await new Promise(done=>backend.close(done));
    const absolute=resolve(directory);assert.ok(absolute.startsWith(resolve(tmpdir())+sep)&&basename(absolute).startsWith('scp-cv-cors020-'));
    await rm(absolute,{recursive:true,force:true});
  }
});
