/** 打包前的客户端媒体完整性闸门；不让缺失extraResources静默产出无直连能力的客户端。 */
import {createHash} from 'node:crypto';
import {readFile,stat} from 'node:fs/promises';
import {resolve,join} from 'node:path';
import {fileURLToPath} from 'node:url';
const root=fileURLToPath(new URL('../..',import.meta.url));
const directory=resolve(process.argv.find(value=>value.startsWith('--assets='))?.slice(9)||join(root,'.validation/client-preview-020/assets'));
const manifest=JSON.parse(await readFile(join(directory,'media-assets.json'),'utf8'));
for(const name of ['ScpCv.ClientPreviewHost.exe','ScpCv.ClientPreviewHost.runtimeconfig.json','hostfxr.dll','coreclr.dll','ffmpeg.exe','FFmpeg-LICENSE.txt']){
  const info=await stat(join(directory,name));if(!info.isFile()||info.size===0)throw Error(`客户端媒体资产缺失：${name}`);
}
const digest=createHash('sha256').update(await readFile(join(directory,'ffmpeg.exe'))).digest('hex');
if(digest!==manifest.ffmpeg_sha256||manifest.platform!=='win-x64')throw Error('客户端媒体资产摘要或平台不匹配');
if(!manifest.development_only){if(!manifest.corresponding_source_included)throw Error('发行资产缺匹配源码声明');await stat(join(directory,'FFmpeg-corresponding-source.zip'));}
if(process.argv.includes('--require-distributable')&&manifest.development_only)throw Error('当前仅本机验证资产，未满足公开发行门禁');
console.log(JSON.stringify({complete:true,platform:'win-x64',developmentOnly:Boolean(manifest.development_only),directory}));
