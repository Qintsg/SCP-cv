/** 清晰度优先参数与实际质量投影；提示/上限不冒充实际值。SCP-cv 2026-10-12。 */
import type { ScreenShareVideoPolicy } from '@/services/screenSharing';
export const DEFAULT_VIDEO_POLICY: Readonly<ScreenShareVideoPolicy> = { full_hd_bitrate: 16_000_000, qhd_bitrate: 24_000_000, ultra_hd_bitrate: 48_000_000, max_frame_rate: 30 };
export const displayVideoConstraints = {
  resizeMode: 'none', frameRate: { ideal: 30, max: 30 },
} satisfies MediaTrackConstraints & { resizeMode: string };

export interface ShareQuality {
  captureWidth: number | null;
  captureHeight: number | null;
  sentWidth: number | null;
  sentHeight: number | null;
  fps: number | null;
  bitrateMbps: number | null;
  limitMbps: number | null;
  limitation: string;
  parameters: string;
}
interface ByteSample { id: string; timestamp: number; bytesSent: number }

/** :param value: 浏览器统计值。:returns: 有限正值，缺失保持未知。 */
export function positiveStat(value: unknown): number | null {
  return typeof value === 'number' && Number.isFinite(value) && value > 0 ? value : null;
}
/** :param settings: 本次捕获实际尺寸。:returns: 项目建议上限，不是实际码率。 */
export function videoBitrateLimit(settings: MediaTrackSettings, policy: Readonly<ScreenShareVideoPolicy> = DEFAULT_VIDEO_POLICY): number {
  const pixels = (settings.width ?? 1920) * (settings.height ?? 1080);
  return pixels <= 1920 * 1080 ? policy.full_hd_bitrate : pixels <= 2560 * 1440 ? policy.qhd_bitrate : policy.ultra_hd_bitrate;
}
/** :param policy: 主机发布的策略；旧主机使用共享默认值。:returns: 合法编码参数。 */
export function checkedVideoPolicy(policy?: ScreenShareVideoPolicy): Readonly<ScreenShareVideoPolicy> {
  if (!policy) return DEFAULT_VIDEO_POLICY;
  if (![policy.full_hd_bitrate, policy.qhd_bitrate, policy.ultra_hd_bitrate, policy.max_frame_rate].every(Number.isSafeInteger)
    || policy.full_hd_bitrate < 1_000_000 || policy.qhd_bitrate < policy.full_hd_bitrate || policy.ultra_hd_bitrate < policy.qhd_bitrate
    || policy.ultra_hd_bitrate > 200_000_000 || policy.max_frame_rate < 1 || policy.max_frame_rate > 30) throw new Error('share_video_policy_invalid');
  return policy;
}
/** :param previous: 上一次同report采样。:param current: 当前实际累计值。:returns: 差分Mbps，重置/首帧/缺字段为未知。 */
export function actualBitrateMbps(previous: ByteSample | undefined, current: ByteSample): number | null {
  if (!previous || previous.id !== current.id || !Number.isFinite(current.bytesSent) ||
    !Number.isFinite(previous.bytesSent) || !Number.isFinite(previous.timestamp) || !Number.isFinite(current.timestamp) ||
    current.timestamp <= previous.timestamp || current.bytesSent < previous.bytesSent) return null;
  return (current.bytesSent - previous.bytesSent) * 8 / ((current.timestamp - previous.timestamp) * 1000);
}
/** :param track: 本次视频。:returns: 只设置编码提示，不改变声音轨道或重新请求许可。 */
export function preferTextDetail(track: MediaStreamTrack): void {
  try {
    track.contentHint = 'text';
    if (track.contentHint !== 'text') track.contentHint = 'detail';
  } catch { /* 不支持提示不能破坏有效采集。 */ }
}
/** :param sender: 当前已协商的视频发送者。:param live: 本次资源寿命。:returns: 参数接受状态；兼容回退有界，停止不继续。 */
export async function configureVideoSender(sender: RTCRtpSender, live: () => boolean, policy: Readonly<ScreenShareVideoPolicy> = DEFAULT_VIDEO_POLICY): Promise<string> {
  if (sender.track?.kind !== 'video') return 'not-video';
  for (let mode = 0; mode < 3 && live(); mode++) {
    try {
      const parameters = sender.getParameters();
      if (!parameters.encodings?.length) return 'browser-default';
      if (mode === 0) parameters.degradationPreference = 'maintain-resolution';
      else delete parameters.degradationPreference;
      for (const encoding of parameters.encodings) {
        encoding.maxBitrate = videoBitrateLimit(sender.track.getSettings?.() ?? {}, policy);
        if (mode < 2) { encoding.scaleResolutionDownBy = 1; encoding.maxFramerate = policy.max_frame_rate; }
        else { delete encoding.scaleResolutionDownBy; delete encoding.maxFramerate; }
      }
      if (!live()) return 'closed';
      await sender.setParameters(parameters);
      return live() ? mode === 0 ? 'configured' : 'compatible' : 'closed';
    } catch (error) {
      if (!live()) return 'closed';
      const name = error instanceof Error ? error.name : '';
      if (!['NotSupportedError', 'InvalidModificationError', 'TypeError'].includes(name)) return 'browser-default';
    }
  }
  return 'browser-default';
}

/** :param peer: 当前连接。:param track: 捕获轨道。:param state: 参数状态。:returns: 每资源独立统计器，无地址/SDP泄漏。 */
export function qualitySampler(peer: RTCPeerConnection, track: MediaStreamTrack, state: () => string) {
  let previous: ByteSample | undefined;
  return async (): Promise<ShareQuality> => {
    const settings = track.getSettings?.() ?? {};
    const result: ShareQuality = {
      captureWidth: positiveStat(settings.width), captureHeight: positiveStat(settings.height),
      sentWidth: null, sentHeight: null, fps: null, bitrateMbps: null,
      limitMbps: null, limitation: 'unknown', parameters: state(),
    };
    const accepted = peer.getSenders().find(sender => sender.track?.kind === 'video')?.getParameters().encodings?.[0]?.maxBitrate;
    result.limitMbps = positiveStat(accepted) === null ? null : accepted! / 1_000_000;
    const stats = await peer.getStats();
    stats.forEach(report => {
      if (report.type !== 'outbound-rtp' || (report.kind ?? report.mediaType) !== 'video' || report.isRemote) return;
      result.sentWidth = positiveStat(report.frameWidth); result.sentHeight = positiveStat(report.frameHeight);
      result.fps = positiveStat(report.framesPerSecond);
      if (['none', 'bandwidth', 'cpu', 'other'].includes(report.qualityLimitationReason)) result.limitation = report.qualityLimitationReason;
      const sample = { id: report.id, timestamp: report.timestamp, bytesSent: report.bytesSent };
      result.bitrateMbps = actualBitrateMbps(previous, sample); previous = sample;
    });
    return result;
  };
}
