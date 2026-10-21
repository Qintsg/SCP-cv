/** 无输入模拟的真renderer探针：只消费自己的合成媒体和受限平台桥。 */
void (async () => {
  const started = performance.now(), preview = window.scpCvElectron?.preview, video = document.querySelector('video');
  let lease, player, frames = 0, firstFrame = 0, done = false;
  const failures = [];
  /** :param success: 只有实际帧才能成功。:returns: 清理订阅后发固定事实，不输出凭据。 */
  const finish = async success => {
    if (done) return; done = true;
    const width = video.videoWidth, height = video.videoHeight;
    try { player?.destroy(); if (lease) await preview.release(lease.id); } catch { failures.push('release-failed'); }
    console.log(`NATIVE_RESULT:${JSON.stringify({ success, frames, firstFrameMilliseconds: Math.round(firstFrame), width,
      height, failures, nodeExposed: typeof window.require !== 'undefined' })}`);
  };
  try {
    if (!preview || !window.mpegts) throw new Error('native-capability-missing');
    lease = await preview.acquire({ input: { kind: 'window_1' }, quality: 'high' });
    if (!lease.streamUrl.startsWith('app://scp-cv/__preview/') || /rtsp|password|127\.0\.0\.1/.test(JSON.stringify(lease))) throw new Error('lease-boundary');
    video.muted = true;
    player = window.mpegts.createPlayer({ type: 'flv', url: lease.streamUrl, isLive: true, hasAudio: false, hasVideo: true }, {
      enableWorker: true, enableStashBuffer: false, lazyLoad: false, liveBufferLatencyChasing: true,
      liveBufferLatencyMaxLatency: 1, liveBufferLatencyMinRemain: 0.2,
    });
    player.on(window.mpegts.Events.ERROR, () => { failures.push('mpegts-error'); void finish(false); });
    const frame = () => {
      frames++; if (!firstFrame) firstFrame = performance.now() - started;
      if (frames >= 12) { void finish(video.videoWidth === 1280 && video.videoHeight === 720); return; }
      video.requestVideoFrameCallback(frame);
    };
    video.requestVideoFrameCallback(frame);
    player.attachMediaElement(video); player.load(); await player.play();
  } catch (error) { failures.push(String(error?.message || 'native-probe-failed').replace(/rtsp:\/\/\S+/g, '[redacted]').slice(-240)); await finish(false); }
})();
