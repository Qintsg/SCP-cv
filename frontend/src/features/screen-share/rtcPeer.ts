/** 发布者独占WebRTC；无外部STUN/TURN、不获取麦克风，不用连接状态冒充帧。SCP-cv 2026-10-11。 */
import type { ScreenShareSdp, ScreenShareVideoPolicy } from '@/services/screenSharing';
import { checkedVideoPolicy, configureVideoSender, preferTextDetail, qualitySampler, videoBitrateLimit, type ShareQuality } from './shareQuality';

export interface ScreenSharePeer {
  offer(signal: AbortSignal): Promise<ScreenShareSdp>;
  answer(description: ScreenShareSdp, signal: AbortSignal): Promise<void>;
  close(): void;
  quality?(): Promise<ShareQuality>;
}

/** :param peer: 当前连接。:param signal: 当前尝试寿命。:returns: 最多12秒聚合ICE，停止后移除自有监听/计时器。 */
function gatherIce(peer: RTCPeerConnection, signal: AbortSignal): Promise<void> {
  signal.throwIfAborted();
  if (peer.iceGatheringState === 'complete') return Promise.resolve();
  return new Promise((resolve, reject) => {
    const finish = (error?: Error): void => {
      clearTimeout(timer); peer.removeEventListener('icegatheringstatechange', changed); signal.removeEventListener('abort', aborted);
      if (error) reject(error); else resolve();
    };
    const changed = (): void => { if (peer.iceGatheringState === 'complete') finish(); };
    const aborted = (): void => finish(new Error('share_cancelled'));
    const timer = setTimeout(() => finish(new Error('share_ice_timeout')), 12000);
    peer.addEventListener('icegatheringstatechange', changed); signal.addEventListener('abort', aborted, { once: true });
    // 注册期间也可能已完成或取消，补查当前状态。
    if (signal.aborted) aborted(); else changed();
  });
}

/** :param stream: 用户本次授权的内容轨道。:param failed: 断线通知，只结束当前实例。:returns: 由发布者拥有并显式close的连接。 */
export function createScreenSharePeer(stream: MediaStream, failed: () => void, suppliedPolicy?: ScreenShareVideoPolicy): ScreenSharePeer {
  const policy = checkedVideoPolicy(suppliedPolicy);
  const peer = new RTCPeerConnection({ iceServers: [] });
  let closed = false;
  let parameters = 'pending';
  const video = stream.getVideoTracks().find(track => track.readyState === 'live');
  if (video) preferTextDetail(video);
  const sample = video ? qualitySampler(peer, video, () => parameters) : undefined;
  let attemptedBitrate: number | undefined;
  let configuring: Promise<void> | undefined;
  /** :returns: 只在捕获进入新码率档位时更新发送参数。 */
  async function configure(signal?: AbortSignal): Promise<void> {
    if (closed || signal?.aborted || !video) return;
    if (configuring) return configuring;
    const limit = videoBitrateLimit(video.getSettings(), policy);
    if (limit === attemptedBitrate) return;
    attemptedBitrate = limit;
    const operation = (async () => {
      for (const sender of peer.getSenders()) {
        if (closed || signal?.aborted) return;
        if (sender.track?.kind === 'video') parameters = await configureVideoSender(sender, () => !closed && !signal?.aborted, policy);
      }
    })();
    configuring = operation;
    try { await operation; } finally { if (configuring === operation) configuring = undefined; }
  }
  const connectionChanged = (): void => {
    if (!closed && ['disconnected', 'failed', 'closed'].includes(peer.connectionState)) failed();
  };
  peer.addEventListener('connectionstatechange', connectionChanged);
  try { for (const track of stream.getTracks()) if (track.readyState === 'live') peer.addTrack(track, stream); }
  catch (error) { peer.removeEventListener('connectionstatechange', connectionChanged); peer.close(); throw error; }
  return {
    async offer(signal) {
      signal.throwIfAborted();
      await peer.setLocalDescription(await peer.createOffer());
      await gatherIce(peer, signal); signal.throwIfAborted();
      const sdp = peer.localDescription?.sdp;
      if (!sdp || new TextEncoder().encode(sdp).byteLength > 65536) throw new Error('share_sdp_invalid');
      return { type: 'offer', sdp };
    },
    async answer(description, signal) {
      signal.throwIfAborted();
      if (description.type !== 'answer' || new TextEncoder().encode(description.sdp).byteLength > 65536) throw new Error('share_sdp_invalid');
      await peer.setRemoteDescription(description); signal.throwIfAborted();
      await configure(signal);
      signal.throwIfAborted();
    },
    quality: sample ? async () => { await configure(); return sample(); } : undefined,
    close() {
      if (closed) return;
      closed = true; peer.removeEventListener('connectionstatechange', connectionChanged); peer.close();
    },
  };
}
