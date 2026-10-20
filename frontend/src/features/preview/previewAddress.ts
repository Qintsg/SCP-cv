/** 已登记墙面输入的本机预览地址；不猜测预留3/4输入，不经过播放Host。 */
import type { VideoWallMappingItem } from '@/services/apiTypes';
import { validIp } from '../runtime/wall/layoutModel';
/** :param input: 墙面输入。:param quality: 小窗低清、大窗高清。:returns: 不含凭据的设备RTSP地址，未配置返回空。 */
export function previewAddress(input: VideoWallMappingItem['input'] | undefined, quality: 'low' | 'high'): string {
  if (!input) return '';
  const ip = input.kind === 'window_1' ? '192.168.5.63' : input.kind === 'window_2' ? '192.168.5.64'
    : input.kind === 'laptop' ? '192.168.5.81' : input.kind === 'ip_stream' ? input.ip_address || '' : '';
  return validIp(ip) ? `rtsp://${ip}:554/live/chn${quality === 'high' ? 0 : 1}` : '';
}
export const PREVIEW_BRIDGE = 'http://127.0.0.1:18765';
const TOKEN_KEY = 'scp-cv.client-preview-token';
/** :returns: 只读取本标签页主动配置的本机桥令牌，不读取远端认证信息。 */
export function previewToken(): string { try { return sessionStorage.getItem(TOKEN_KEY) || ''; } catch { return ''; } }
/** :param token: 本机桥启动时生成的临时令牌。:returns: 仅当前标签页保存并通知预览组件。 */
export function configurePreview(token: string): void {
  sessionStorage.setItem(TOKEN_KEY, token.trim()); window.dispatchEvent(new Event('scp-client-preview-change'));
}
