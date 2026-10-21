/** mpegts.js薄适配：无声MSE播放、有限缓冲、解码帧证据及彻底销毁。 */
import type { InjectionKey } from 'vue';
export interface PreviewPlayer { destroy(): void }
export type PreviewPlayerFactory = (video: HTMLVideoElement, url: string, frame: () => void, failed: () => void) => Promise<PreviewPlayer>;
export const PREVIEW_PLAYER_FACTORY: InjectionKey<PreviewPlayerFactory> = Symbol('preview-player-factory');
export class PreviewUnsupportedError extends Error {}
/** :param video: 共享池自有video。:param url: 原Host/受限原生租约流。:param frame: 实际解码帧回调。:param failed: 固定状态回调，不传原始异常。:returns: 无额外工具依赖的浏览器播放器。 */
export const createPreviewPlayer: PreviewPlayerFactory = async (video, url, frame, failed) => {
  const { default: mpegts } = await import('mpegts.js');
  if (!mpegts.getFeatureList().mseLivePlayback) throw new PreviewUnsupportedError('此浏览器不支持直播预览');
  video.muted = true; video.volume = 0;
  const absoluteUrl = new URL(url, window.location.href).href;
  const player = mpegts.createPlayer({ type: 'flv', url: absoluteUrl, isLive: true, hasAudio: false, hasVideo: true, withCredentials: true }, {
    enableWorker: true, enableStashBuffer: false, lazyLoad: false, liveBufferLatencyChasing: true, liveBufferLatencyMaxLatency: 1.5,
    liveBufferLatencyMinRemain: .4, autoCleanupSourceBuffer: true, autoCleanupMaxBackwardDuration: 10, autoCleanupMinBackwardDuration: 3,
  });
  let active = true, callback = 0, lastTime = -1;
  let fallback: ReturnType<typeof setInterval> | undefined;
  /** :returns: 仅实际解码的新帧算出画，不把HTTP200或MSE ready当出画。 */
  const decoded = (): void => { if (!active) return; frame(); callback = video.requestVideoFrameCallback(decoded); };
  if (typeof video.requestVideoFrameCallback === 'function') callback = video.requestVideoFrameCallback(decoded);
  else fallback = setInterval(() => { if (video.readyState >= 2 && video.currentTime !== lastTime) { lastTime = video.currentTime; frame(); } }, 500);
  player.on(mpegts.Events.ERROR, () => { if (active) failed(); });
  const managed: PreviewPlayer = { destroy() {
    if (!active) return; active = false; clearInterval(fallback);
    if (callback && typeof video.cancelVideoFrameCallback === 'function') video.cancelVideoFrameCallback(callback);
    for (const action of [() => player.pause(), () => player.unload(), () => player.detachMediaElement(), () => player.destroy()]) { try { action(); } catch { /* 前一步失败仍必须销毁后续资源。 */ } }
    video.removeAttribute('src'); video.load();
  } };
  try { player.attachMediaElement(video); player.load(); void Promise.resolve(player.play()).catch(() => { if (active) failed(); }); return managed; }
  catch (error) { managed.destroy(); throw error; }
};
