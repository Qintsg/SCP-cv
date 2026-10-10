/** 单个网页授权尝试的所有者：许可先于API、本地先停止、迟到结果不复活。SCP-cv 2026-10-11。 */
import type { ScreenSharingApi, ScreenShareSnapshot } from '@/services/screenSharing';
import type { ScreenSharePeer } from './rtcPeer';

export interface ScreenShareView {
  phase: 'idle' | 'selecting' | 'connecting' | 'sharing' | 'stopping' | 'closed' | 'failed' | 'cancelled';
  errorCode: string;
  snapshot: ScreenShareSnapshot | null;
  captureActive: boolean;
  localHasAudio: boolean;
}
export interface PublisherDependencies {
  windowId(): number;
  availability(): string;
  isCurrent(): boolean;
  capture(options: DisplayMediaStreamOptions): Promise<MediaStream>;
  peer(stream: MediaStream, failed: () => void): ScreenSharePeer;
  api: ScreenSharingApi;
  changed(view: ScreenShareView): void;
}
interface Attempt {
  window: number;
  abort: AbortController;
  stream?: MediaStream;
  peer?: ScreenSharePeer;
  snapshot?: ScreenShareSnapshot;
  answered: boolean;
  ended?: () => void;
  deadline?: ReturnType<typeof setTimeout>;
  stopped?: Promise<void>;
}

/** :param signal: 本次寿命。:returns: 一秒后继续，停止时立即清理监听和计时器。 */
function nextPoll(signal: AbortSignal): Promise<void> {
  signal.throwIfAborted();
  return new Promise((resolve, reject) => {
    const ended = (): void => { clearTimeout(timer); signal.removeEventListener('abort', ended); reject(new Error('share_cancelled')); };
    const timer = setTimeout(() => { signal.removeEventListener('abort', ended); resolve(); }, 1000);
    signal.addEventListener('abort', ended, { once: true });
  });
}
/** :param error: 外部错误。:returns: 固定文案键，不把浏览器/信令异常详情写到页面。 */
function errorCode(error: unknown): string {
  if (error instanceof Error) {
    const known = new Set(['share_window_busy', 'share_player_offline', 'share_sdp_invalid', 'share_sdp_too_large',
      'share_ice_timeout', 'share_capture_empty', 'share_context_changed', 'share_identity_invalid', 'share_ended']);
    const code = 'code' in error && typeof error.code === 'string' ? error.code : error.message;
    if (known.has(code)) return code;
    if (error.name === 'UnauthorizedError') return 'share_login_required';
    if (error.name === 'StaleConnectionResponseError') return 'share_context_changed';
    if (error.name === 'NotAllowedError') return 'share_permission_denied';
    if (error.name === 'AbortError') return 'share_cancelled';
    if (error.name === 'NotReadableError') return 'share_capture_failed';
  }
  return 'share_connection_failed';
}

export class ScreenSharePublisher {
  view: ScreenShareView = { phase: 'idle', errorCode: '', snapshot: null, captureActive: false, localHasAudio: false };
  private attempt?: Attempt;
  private disposed = false;
  private readonly deps: PublisherDependencies;
  /** :param dependencies: 仅外部IO与当前页面身份边界。:returns: 尚未采集或预约的新所有者。 */
  constructor(dependencies: PublisherDependencies) { this.deps = dependencies; }

  /** :param patch: 当前尝试的可见状态。:returns: 发布新纯值视图，不导出peer/轨道或SDP以外私有信息。 */
  private update(patch: Partial<ScreenShareView>): void {
    this.view = { ...this.view, ...patch }; this.deps.changed(this.view);
  }
  /** :param attempt: 发起回调的资源。:returns: 只有当前账号/主机及未停止资源能继续协商或采样。 */
  private live(attempt: Attempt): boolean { return this.attempt === attempt && !attempt.abort.signal.aborted && !this.disposed && this.deps.isCurrent(); }
  /** :param attempt: 本次授权尝试。:returns: 本地先停止，计时器、ended监听、peer和轨道全部解除。 */
  private release(attempt: Attempt): void {
    attempt.abort.abort(); clearTimeout(attempt.deadline);
    if (attempt.ended) for (const track of attempt.stream?.getVideoTracks() ?? []) track.removeEventListener('ended', attempt.ended);
    attempt.peer?.close();
    for (const track of attempt.stream?.getTracks() ?? []) track.stop();
    this.update({ captureActive: false });
  }
  /** :param attempt: 原尝试。:param snapshot: HTTP结果。:returns: 相同会话、目标及代次；错误响应不覆盖原身份。 */
  private accept(attempt: Attempt, snapshot: ScreenShareSnapshot): void {
    if (!snapshot || snapshot.window_id !== attempt.window || !/^[\da-f]{8}(-[\da-f]{4}){3}-[\da-f]{12}$/i.test(snapshot.id) ||
      (attempt.snapshot && (snapshot.id !== attempt.snapshot.id || (attempt.snapshot.source_generation !== null && snapshot.source_generation !== attempt.snapshot.source_generation))) ||
      !['allocated', 'negotiating', 'receiving', 'stopping', 'closed', 'failed'].includes(snapshot.state) ||
      !Number.isSafeInteger(snapshot.actual_frames) || snapshot.actual_frames < 0) throw new Error('share_identity_invalid');
    attempt.snapshot = snapshot;
    this.update({ snapshot });
  }
  /** :param audio: 用户是否选取内容音频，不请求麦克风。:returns: 预约和offer受理；实际后帧由独立状态轮询授予。 */
  async start(audio: boolean): Promise<void> {
    if (this.disposed || this.attempt || this.view.phase === 'stopping') return;
    const unavailable = this.deps.availability();
    if (unavailable) { this.update({ phase: 'failed', errorCode: unavailable }); return; }
    const attempt: Attempt = { window: this.deps.windowId(), abort: new AbortController(), answered: false };
    this.attempt = attempt;
    this.update({ phase: 'selecting', errorCode: '', snapshot: null, captureActive: false, localHasAudio: false });
    try {
      // 必须在原点击栈直接调用，前面无await、HTTP或计时器；保留浏览器瞬时用户激活。
      const stream = await this.deps.capture({ video: { width: { ideal: 1920, max: 1920 }, height: { ideal: 1080, max: 1080 }, frameRate: { ideal: 30, max: 30 } }, audio });
      if (!this.live(attempt)) { for (const track of stream.getTracks()) track.stop(); return; }
      attempt.stream = stream;
      if (!stream.getVideoTracks().some((track) => track.readyState === 'live')) throw new Error('share_capture_empty');
      if (!audio) for (const track of stream.getAudioTracks()) track.stop();
      const hasAudio = stream.getAudioTracks().some((track) => track.readyState === 'live');
      this.update({ phase: 'connecting', captureActive: true, localHasAudio: hasAudio });
      attempt.ended = () => { if (this.attempt === attempt) void this.stop(); };
      for (const track of stream.getVideoTracks()) track.addEventListener('ended', attempt.ended, { once: true });
      attempt.deadline = setTimeout(() => { if (this.live(attempt)) void this.stop('share_first_frame_timeout'); }, 20000);
      const allocated = await this.deps.api.create(attempt.window, hasAudio, attempt.abort.signal);
      if (!this.live(attempt)) {
        // 停止可能发生在预约请求返回前；只撤销迟到的原预约，不发布它，更不能关闭新源。
        if (this.deps.isCurrent() && allocated.window_id === attempt.window && /^[\da-f]{8}(-[\da-f]{4}){3}-[\da-f]{12}$/i.test(allocated.id)) {
          await this.deps.api.stop(allocated.id);
        }
        return;
      }
      this.accept(attempt, allocated);
      if (!this.live(attempt)) { await this.stop(); return; }
      attempt.peer = this.deps.peer(stream, () => { if (this.live(attempt)) void this.stop('share_peer_disconnected'); });
      const offer = await attempt.peer.offer(attempt.abort.signal);
      if (!this.live(attempt)) return;
      const accepted = await this.deps.api.offer(allocated.id, offer, attempt.abort.signal);
      if (!this.live(attempt)) return;
      this.accept(attempt, accepted);
      void this.observe(attempt);
    } catch (error) {
      if (this.attempt !== attempt || attempt.abort.signal.aborted) return;
      await this.stop(errorCode(error));
    }
  }
  /** :param attempt: 原协商。:returns: 心跳只续寿命；receiving及正帧数才呈现实际画面，任何结束不自动重采。 */
  private async observe(attempt: Attempt): Promise<void> {
    let heartbeatAt = Date.now() + 8000;
    try {
      while (this.live(attempt)) {
        const snapshot = await this.deps.api.get(attempt.snapshot!.id, attempt.abort.signal);
        if (!this.live(attempt)) break;
        this.accept(attempt, snapshot);
        if (snapshot.state === 'closed' || snapshot.state === 'failed') {
          this.release(attempt); this.attempt = undefined;
          this.update({ phase: snapshot.state, errorCode: snapshot.state === 'failed' ? 'share_ended' : '' }); return;
        }
        if (snapshot.state === 'stopping') { await this.stop('share_ended'); return; }
        if (snapshot.answer && !attempt.answered) { await attempt.peer!.answer(snapshot.answer, attempt.abort.signal); attempt.answered = true; }
        if (!this.live(attempt)) break;
        if (snapshot.state === 'receiving' && snapshot.actual_frames > 0 && attempt.answered) {
          clearTimeout(attempt.deadline); this.update({ phase: 'sharing' });
        }
        if (Date.now() >= heartbeatAt) {
          await this.deps.api.heartbeat(snapshot.id, attempt.abort.signal); heartbeatAt = Date.now() + 8000;
        }
        await nextPoll(attempt.abort.signal);
      }
      if (this.attempt === attempt && !attempt.abort.signal.aborted) await this.stop('share_context_changed');
    } catch (error) {
      if (this.attempt === attempt && !attempt.abort.signal.aborted)
        await this.stop(this.deps.isCurrent() ? errorCode(error) : 'share_context_changed');
    }
  }
  /** :param reason: 可选固定失败键。:returns: 先释放采集再发一次DELETE，受理不是实际释放；不向新主机发送旧停止。 */
  stop(reason = ''): Promise<void> {
    const attempt = this.attempt;
    if (!attempt) return Promise.resolve();
    if (attempt.stopped) return attempt.stopped;
    this.release(attempt);
    this.update({ phase: attempt.snapshot ? 'stopping' : reason ? 'failed' : 'cancelled', errorCode: reason });
    attempt.stopped = this.stopRemote(attempt);
    return attempt.stopped;
  }
  /** :param attempt: 本地已结束的尝试。:returns: 原账号/主机可达时才发条件停止，不自动重复副作用。 */
  private async stopRemote(attempt: Attempt): Promise<void> {
    if (!attempt.snapshot || !this.deps.isCurrent()) {
      if (this.attempt === attempt) this.attempt = undefined;
      if (attempt.snapshot) this.update({ phase: 'failed', errorCode: 'share_context_changed' });
      return;
    }
    try {
      const snapshot = await this.deps.api.stop(attempt.snapshot.id);
      if (this.attempt !== attempt || !this.deps.isCurrent()) return;
      this.accept(attempt, snapshot);
      if (snapshot.state === 'closed' || snapshot.state === 'failed') {
        this.attempt = undefined; this.update({ phase: this.view.errorCode ? 'failed' : snapshot.state });
      }
    } catch { if (this.attempt === attempt) this.update({ phase: 'stopping', errorCode: 'share_stop_unconfirmed' }); }
  }
  /** :returns: 仅查询停止结果，绝不重发DELETE或重启采集。 */
  async refreshStop(): Promise<void> {
    const attempt = this.attempt;
    if (!attempt?.snapshot || this.view.phase !== 'stopping' || !this.deps.isCurrent()) return;
    try {
      const snapshot = await this.deps.api.get(attempt.snapshot.id);
      if (this.attempt !== attempt || !this.deps.isCurrent()) return;
      this.accept(attempt, snapshot);
      if (snapshot.state === 'closed' || snapshot.state === 'failed') {
        this.attempt = undefined; this.update({ phase: this.view.errorCode ? 'failed' : snapshot.state });
      }
    } catch {
      if (this.attempt === attempt && this.deps.isCurrent() && this.view.phase === 'stopping')
        this.update({ errorCode: 'share_stop_unconfirmed' });
    }
  }
  /** :returns: 页面退出先停全部本地采集；远端失败仍保持待确认，不清除事实。 */
  async dispose(): Promise<void> { this.disposed = true; await this.stop(); }
}
