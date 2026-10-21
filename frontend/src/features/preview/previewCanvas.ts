/** 单decoder的画布接收端：只按实际新帧绘制，改变几何不创建连接或视频实例。 */
import type { PreviewFrame } from './previewPool';
export interface PreviewCanvas { draw(video: PreviewFrame): void; clear(): void; dispose(): void }

/** :param canvas: 当前展示画布。:returns: 有界DPR/像素的contain画布，保留最近帧以便恢复。 */
export function createPreviewCanvas(canvas: HTMLCanvasElement): PreviewCanvas {
  let last: PreviewFrame | undefined, disposed = false;
  /** :param video: 共享解码元素。:returns: 仅当有真实像素时绘制，不使用页面截图。 */
  const draw = (video: PreviewFrame): void => {
    const sourceWidth = 'videoWidth' in video ? video.videoWidth : video.width;
    const sourceHeight = 'videoHeight' in video ? video.videoHeight : video.height;
    if (disposed || !sourceWidth || !sourceHeight) return;
    last = video;
    const box = canvas.getBoundingClientRect(), dpr = Math.min(2, window.devicePixelRatio || 1);
    const width = Math.max(1, Math.min(1920, Math.round((box.width || sourceWidth) * dpr)));
    const height = Math.max(1, Math.min(1080, Math.round((box.height || sourceHeight) * dpr)));
    if (canvas.width !== width) canvas.width = width; if (canvas.height !== height) canvas.height = height;
    const context = canvas.getContext('2d'); if (!context) return;
    const scale = Math.min(width / sourceWidth, height / sourceHeight);
    const w = sourceWidth * scale, h = sourceHeight * scale;
    try { context.clearRect(0, 0, width, height); context.drawImage(video, (width - w) / 2, (height - h) / 2, w, h); }
    catch { /* 元素正在关闭时的迟到帧不能中断其它消费者。 */ }
  };
  const observer = typeof ResizeObserver === 'undefined' ? undefined : new ResizeObserver(() => { if (last) draw(last); });
  observer?.observe(canvas);
  return { draw, clear() { last = undefined; canvas.getContext('2d')?.clearRect(0, 0, canvas.width, canvas.height); },
    dispose() { disposed = true; last = undefined; observer?.disconnect(); } };
}
