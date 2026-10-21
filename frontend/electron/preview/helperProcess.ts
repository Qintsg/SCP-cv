/** 客户端私有宿主进程；固定资产、无shell、短心跳，等待真实退出后才允许替换。 */
import { spawn, type ChildProcessWithoutNullStreams } from 'node:child_process';
import { existsSync, realpathSync } from 'node:fs';
import { join, resolve } from 'node:path';
import type { Readable } from 'node:stream';
import { NativePreviewError } from './contracts.js';

export interface PreviewMediaProcess { output: Readable; completed: Promise<void>; stop(): Promise<void> }
export interface PreviewAssetPaths { helper: string; directory: string }
/** :param directory: 主进程固定资源根，不能取自IPC。:returns: 同目录私有宿主/FFmpeg，不查询PATH。 */
export function resolvePreviewAssets(directory: string): PreviewAssetPaths {
  const root = realpathSync(resolve(directory)), helper = join(root, 'ScpCv.ClientPreviewHost.exe');
  for (const path of [helper, join(root, 'ffmpeg.exe'), join(root, 'ScpCv.ClientPreviewHost.runtimeconfig.json')]) {
    if (!existsSync(path) || realpathSync(path) !== path) throw new NativePreviewError('native_preview_dependency_missing');
  }
  return { helper, directory: root };
}
/** :param assets: 已验证包内资产。:param uri: 当前主机受限描述符。:param mode: 原码或单次兼容。:returns: 有确认退出语义的自有进程。 */
export function startPreviewHelper(assets: PreviewAssetPaths, uri: string, mode: 'copy' | 'compatible'): PreviewMediaProcess {
  const child: ChildProcessWithoutNullStreams = spawn(assets.helper, [], { cwd: assets.directory, shell: false, windowsHide: true,
    stdio: ['pipe', 'pipe', 'pipe'] });
  let ended = false, stopping: Promise<void> | undefined;
  let end: () => void = () => {}, fail: (error: Error) => void = () => {};
  const completed = new Promise<void>((resolveCompleted, reject) => { end = resolveCompleted; fail = reject; });
  // 消费失败，防止早期spawn拒绝造成未处理Promise；调用者仍可等待同一个失败结果。
  void completed.catch(() => {});
  const pulse = setInterval(() => { if (!ended && child.stdin.writable) child.stdin.write('heartbeat\n'); }, 1000);
  child.stderr.resume(); // helper只写固定状态；不把媒体运行时原文搬进日志。
  child.stdin.on('error', () => {});
  child.once('error', () => { ended = true; clearInterval(pulse); fail(new NativePreviewError('native_preview_process_failed')); });
  child.once('exit', () => { ended = true; clearInterval(pulse); end(); });
  child.stdin.write(`${JSON.stringify({ uri, mode })}\n`);
  return { output: child.stdout, completed,
    stop() {
      if (stopping) return stopping;
      stopping = (async () => {
        clearInterval(pulse);
        if (ended) { await completed; return; }
        child.stdin.end();
        let timeout: ReturnType<typeof setTimeout> | undefined;
        try {
          await Promise.race([completed, new Promise<void>((_, reject) => { timeout = setTimeout(() => {
            // 精确对象kill关闭helper的自有Job，不按名称杀其它FFmpeg。
            child.kill(); reject(new NativePreviewError('native_preview_cleanup_pending'));
          }, 3000); })]);
        } finally { clearTimeout(timeout); }
      })();
      return stopping;
    },
  };
}
