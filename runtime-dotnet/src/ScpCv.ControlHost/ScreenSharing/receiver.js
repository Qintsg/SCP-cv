/* 两窗WebRTC接收：真实后帧、内存授权、有限信令和明确释放。SCP-cv 2026-10-10。 */
(() => {
  'use strict';
  const parameters = new URLSearchParams(location.search);
  const shareId = location.pathname.split('/').filter(Boolean).at(-1);
  const windowId = Number(parameters.get('window_id'));
  const generation = Number(parameters.get('source_generation'));
  const token = location.hash.slice(1);
  const endpoint = location.pathname + location.search;
  const video = document.querySelector('#screen');
  const peer = new RTCPeerConnection({ iceServers: [] });
  const remote = new MediaStream();
  const controllers = new Set();
  const lifetime = new AbortController();
  let stopped = false;
  let activated = false;
  let negotiated = false;
  let frames = 0;
  let callbackId = null;
  let lastNativeFrames = 0;
  let lastNativeActivated = false;
  let lastReportAt = 0;
  let reporting = false;
  let polling = false;
  let statusTimer = null;
  // fragment只在本机内存中使用，移除后不能从地址/历史/HTTP访问日志读到凭据。
  history.replaceState(null, '', endpoint);

  /** :param type: 固定消息类型；:param fields: 不含SDP或授权的实际资源字段。 */
  function notify(type, fields = {}) {
    window.chrome?.webview?.postMessage({ type, share_id: shareId, window_id: windowId, source_generation: generation, ...fields });
  }

  /** :param suffix: 固定相对端点；:returns: 有界且不携带用户cookie的JSON响应。 */
  async function request(suffix, method = 'GET', body = null) {
    const controller = new AbortController();
    controllers.add(controller);
    const timeout = setTimeout(() => controller.abort(), 4000);
    try {
      const headers = { Authorization: `Bearer ${token}` };
      if (body !== null) headers['Content-Type'] = 'application/json';
      const result = await fetch(location.pathname + suffix + location.search, {
        method, headers, body: body === null ? null : JSON.stringify(body),
        signal: controller.signal, credentials: 'omit', cache: 'no-store', redirect: 'error',
      });
      if (!result.ok) throw new Error('receiver_request_rejected');
      return await result.json();
    } finally {
      clearTimeout(timeout);
      controllers.delete(controller);
    }
  }

  /** :returns: 所有接收媒体和异步活动均已在当前页面释放；不等于WebView宿主Dispose证明。 */
  function stop() {
    if (stopped) return;
    stopped = true;
    lifetime.abort();
    clearInterval(statusTimer);
    if (callbackId !== null) video.cancelVideoFrameCallback(callbackId);
    for (const controller of controllers) controller.abort();
    controllers.clear();
    peer.close();
    for (const track of remote.getTracks()) track.stop();
    video.pause();
    video.srcObject = null;
    notify('closed', { frames, has_stream: false });
  }

  /** :param code: 固定错误码，不转发引擎异常中的SDP或地址。 */
  function fail(code) {
    if (stopped) return;
    notify('failed', { code });
    stop();
    void request('end/', 'POST', {}).catch(() => {});
  }

  /** :param volume: 窗口音量0..100；:param muted: 实际静音选择。 */
  function setAudio(volume, muted) {
    if (stopped || !Number.isInteger(volume) || volume < 0 || volume > 100 || typeof muted !== 'boolean')
      return { accepted: false };
    video.volume = volume / 100;
    video.muted = muted || remote.getAudioTracks().length === 0;
    return { accepted: true, volume: video.volume * 100, muted: video.muted, has_audio: remote.getAudioTracks().length > 0 };
  }

  /** :returns: 此后首个实际视频帧才可授予实际播出，不把预备帧或连接当作激活。 */
  function activate(volume, muted) {
    const audio = setAudio(volume, muted);
    if (!audio.accepted) return audio;
    activated = true;
    return { ...audio, frames };
  }
  window.scpScreenShare = Object.freeze({ activate, setAudio, stop });

  /** :param metadata: 浏览器实际提交的视频帧，不由连接心跳构造。 */
  function observeFrame(_now, metadata) {
    if (stopped) return;
    frames++;
    const hasAudio = remote.getAudioTracks().some(track => track.readyState === 'live');
    if (frames === 1 || frames - lastNativeFrames >= 30 || activated !== lastNativeActivated) {
      lastNativeFrames = frames;
      lastNativeActivated = activated;
      notify('frame', { frames, media_time: metadata.mediaTime, activated, has_audio: hasAudio });
    }
    if (activated && negotiated && !reporting && (lastReportAt === 0 || performance.now() - lastReportAt >= 1000)) {
      reporting = true;
      lastReportAt = performance.now();
      void request('frames/', 'POST', { frames, has_audio: hasAudio, activated: true })
        .catch(() => fail('share_receiver_report_failed')).finally(() => { reporting = false; });
    }
    callbackId = video.requestVideoFrameCallback(observeFrame);
  }

  peer.addEventListener('track', event => {
    if (stopped) { event.track.stop(); return; }
    remote.addTrack(event.track);
    video.srcObject = remote;
    if (event.track.kind === 'video' && callbackId === null) {
      callbackId = video.requestVideoFrameCallback(observeFrame);
      void video.play().catch(() => fail('share_video_play_failed'));
    }
    event.track.addEventListener('ended', () => fail('share_remote_track_ended'), { once: true });
  });
  peer.addEventListener('connectionstatechange', () => {
    if (peer.connectionState === 'failed' || peer.connectionState === 'disconnected') fail('share_peer_disconnected');
  });
  window.addEventListener('pagehide', stop, { once: true });

  /** :returns: 单侧ICE聚合最多12秒，无外部服务器或无限候选缓存。 */
  async function gatherIce() {
    if (peer.iceGatheringState === 'complete') return;
    await new Promise((resolve, reject) => {
      let settled = false;
      const timeout = setTimeout(() => finish(new Error('ice_timeout')), 12000);
      const check = () => { if (peer.iceGatheringState === 'complete') finish(); };
      const cancelled = () => finish(new Error('receiver_stopped'));
      function finish(error) {
        if (settled) return;
        settled = true;
        clearTimeout(timeout);
        peer.removeEventListener('icegatheringstatechange', check);
        lifetime.signal.removeEventListener('abort', cancelled);
        error ? reject(error) : resolve();
      }
      peer.addEventListener('icegatheringstatechange', check);
      lifetime.signal.addEventListener('abort', cancelled, { once: true });
      lifetime.signal.aborted ? cancelled() : check();
    });
  }

  /** :returns: 协商已完成但仍须原生宿主激活和后帧，不能导航完成就宣称playing。 */
  async function start() {
    if (!/^[a-f0-9]{64}$/.test(token) || ![1, 2].includes(windowId) || !Number.isSafeInteger(generation) || generation <= 0)
      throw new Error('invalid_receiver_identity');
    const offer = await request('offer/');
    if (stopped) return;
    await peer.setRemoteDescription(offer);
    if (stopped) return;
    await peer.setLocalDescription(await peer.createAnswer());
    await gatherIce();
    if (stopped) return;
    await request('answer/', 'POST', { type: peer.localDescription.type, sdp: peer.localDescription.sdp });
    if (stopped) return;
    negotiated = true;
    notify('ready', { frames });
    statusTimer = setInterval(() => {
      if (polling || stopped) return;
      polling = true;
      void request('status/').catch(() => fail('share_authorization_ended')).finally(() => { polling = false; });
    }, 1000);
  }
  void start().catch(() => fail('share_negotiation_failed'));
})();
