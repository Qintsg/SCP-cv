/** Electron接线：可信顶层IPC、同session认证描述符与私有app流，不开放任意网络代理。 */
import { createHash } from 'node:crypto';
import { join } from 'node:path';
import { app, ipcMain, type BrowserWindow, type IpcMainInvokeEvent, type Session } from 'electron';
import { IPC } from '../channels.js';
import { APP_ORIGIN, type StoredServerProfile } from '../security.js';
import { NativePreviewError, type NativeRequest } from './contracts.js';
import { NativePreviewRegistry } from './registry.js';
import { resolvePreviewAssets, startPreviewHelper } from './helperProcess.js';

export interface NativePreviewServices {
  stream(request: Request): Promise<Response>;
  setProfile(profile: StoredServerProfile | null): Promise<void>;
  invalidate(): Promise<void>;
  dispose(): Promise<void>;
}
/** :param response: 可信主机也必须有界读取。:returns: 最大32KiB JSON，不将URI写入错误。 */
async function readDescriptorJson(response: Response): Promise<unknown> {
  const reader = response.body?.getReader(); if (!reader) throw new NativePreviewError('native_preview_invalid_descriptor');
  const parts: Uint8Array[] = []; let size = 0;
  try {
    while (true) {
      const next = await reader.read(); if (next.done) break;
      size += next.value.byteLength;
      if (size > 32768) throw new NativePreviewError('native_preview_invalid_descriptor');
      parts.push(next.value);
    }
    return JSON.parse(Buffer.concat(parts).toString('utf8')) as unknown;
  } finally { await reader.cancel().catch(() => {}); }
}
/** :param session: 与实际renderer相同的持久session。:param currentWindow: 当前唯一顶层窗口。:param trust: 既有可信URL门禁。:returns: 私有预览服务。 */
export function setupNativePreview(session: Session, currentWindow: () => BrowserWindow | null,
  trust: (event: IpcMainInvokeEvent) => void): NativePreviewServices {
  let profile: StoredServerProfile | null = null, profileGeneration = 0;
  /** :returns: 真实HTTPS origin、完整票据分块与活跃创建者摘要；从不返回／持久化口令。 */
  const owner = async (): Promise<string> => {
    const window = currentWindow(), selected = profile, revision = profileGeneration;
    if (!window || window.isDestroyed() || window.webContents.isDestroyed() || !selected) return '';
    const name = selected.origin.startsWith('https:') ? 'sessionid' : 'sessionid_http';
    const cookies = await session.cookies.get({ url: selected.origin });
    const parts = cookies.filter(cookie => cookie.name === name || new RegExp(`^${name}C\\d+$`).test(cookie.name)).sort((a, b) => a.name.localeCompare(b.name));
    if (!parts.some(cookie => cookie.name === name) || revision !== profileGeneration || currentWindow() !== window) return '';
    return `${revision}:${window.webContents.id}:${selected.origin}:${createHash('sha256').update(JSON.stringify(parts.map(cookie => [cookie.name, cookie.value]))).digest('hex')}`;
  };
  /** :param request: 受限来源身份。:param signal: 旧主机/取消围栏。:returns: 仅主进程消费的敏感描述符。 */
  const descriptor = async (request: NativeRequest, signal: AbortSignal): Promise<unknown> => {
    const selected = profile, revision = profileGeneration;
    if (!selected) throw new NativePreviewError('native_preview_no_server');
    const timeout = AbortSignal.timeout(8000), combined = AbortSignal.any([signal, timeout]);
    try {
      const csrf = await session.fetch(`${selected.origin}/api/auth/csrf/`, { credentials: 'include', redirect: 'error', signal: combined,
        headers: { Origin: APP_ORIGIN, Accept: 'application/json' } });
      if (!csrf.ok) throw new NativePreviewError('native_preview_authentication_failed');
      const token = await readDescriptorJson(csrf) as { csrfToken?: unknown };
      if (typeof token.csrfToken !== 'string' || !/^[\w-]{40,64}$/.test(token.csrfToken) || revision !== profileGeneration) throw new NativePreviewError('native_preview_stale_owner');
      const response = await session.fetch(`${selected.origin}/api/stream-previews/native/resolve/`, {
        method: 'POST', credentials: 'include', redirect: 'error', signal: combined,
        headers: { Origin: APP_ORIGIN, Accept: 'application/json', 'Content-Type': 'application/json', 'X-CSRFToken': token.csrfToken }, body: JSON.stringify(request),
      });
      if (!response.ok || !response.headers.get('content-type')?.includes('application/json') || revision !== profileGeneration) throw new NativePreviewError('native_preview_descriptor_unavailable');
      return await readDescriptorJson(response);
    } catch (error) { if (error instanceof NativePreviewError) throw error; throw new NativePreviewError('native_preview_descriptor_unavailable'); }
  };
  const registry = new NativePreviewRegistry({ owner, resolve: descriptor, produce(uri, mode) {
    // 开发验证只接受显式专有资产目录；生产绝不读环境PATH或从renderer取路径。
    const directory = app.isPackaged ? join(process.resourcesPath, 'client-preview')
      : process.env.SCP_CV_CLIENT_PREVIEW_DEV_ASSETS || join(app.getAppPath(), '.native-preview');
    return startPreviewHelper(resolvePreviewAssets(directory), uri, mode);
  } });
  /** :param event: 当前IPC。:returns: 禁止子frame借同源URL取得原生能力。 */
  const assert = (event: IpcMainInvokeEvent): void => {
    trust(event); const window = currentWindow();
    if (!window || event.sender !== window.webContents || event.senderFrame !== event.sender.mainFrame) throw new NativePreviewError('native_preview_untrusted_caller');
  };
  ipcMain.handle(IPC.previewAcquire, (event, request: unknown) => { assert(event); return registry.acquire(request); });
  ipcMain.handle(IPC.previewHeartbeat, (event, id: unknown, aliases?: unknown) => { assert(event); return registry.heartbeat(id, aliases); });
  ipcMain.handle(IPC.previewRelease, (event, id: unknown) => { assert(event); return registry.release(id); });
  ipcMain.handle(IPC.previewFallback, (event, id: unknown) => { assert(event); return registry.fallback(id); });
  ipcMain.handle(IPC.previewInvalidate, event => { assert(event); return registry.invalidate(); });
  const cookieChanged = (_event: Electron.Event, cookie: Electron.Cookie): void => {
    if (/^sessionid(?:_http)?(?:C\d+)?$/.test(cookie.name)) void registry.invalidate().catch(() => {});
  };
  session.cookies.on('changed', cookieChanged);
  return { stream: request => registry.stream(request), invalidate: () => registry.invalidate(),
    async setProfile(next) { profileGeneration++; profile = next; await registry.invalidate(); },
    async dispose() { session.cookies.removeListener('changed', cookieChanged); await registry.dispose(); },
  };
}
