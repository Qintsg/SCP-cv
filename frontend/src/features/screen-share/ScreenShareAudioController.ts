/** 当前共享的有界音频控制；命令成功与实际属性两条证据，不自动重发。SCP-cv 2026-10-11。 */
import type { PlaybackCommandSnapshot } from '@/services/apiTypes';
import type { ScreenSharingApi, ScreenShareAudioSubmission, ScreenShareSnapshot } from '@/services/screenSharing';

export interface ScreenShareAudioView {
  phase: 'idle' | 'pending' | 'confirmed' | 'unconfirmed' | 'failed';
  errorCode: string;
}
interface AudioDependencies {
  api: Pick<ScreenSharingApi, 'setAudio' | 'getAudioCommand'>;
  current(): ScreenShareSnapshot | null;
  read(signal: AbortSignal): Promise<ScreenShareSnapshot | null>;
  changed(view: ScreenShareAudioView): void;
}
interface AudioAction { snapshot: ScreenShareSnapshot; abort: AbortController }
const guid = /^[\da-f]{8}(-[\da-f]{4}){3}-[\da-f]{12}$/i;

/** :param promise: 外部IO。:param signal: 操作寿命。:returns: IO无视取消时仍有界结束，并移除监听。 */
async function bounded<T>(promise: Promise<T>, signal: AbortSignal): Promise<T> {
  signal.throwIfAborted();
  let cancel: () => void = () => {};
  const aborted = new Promise<never>((_resolve, reject) => {
    cancel = () => reject(signal.reason);
    signal.addEventListener('abort', cancel, { once: true });
  });
  try { return await Promise.race([promise, aborted]); }
  finally { signal.removeEventListener('abort', cancel); }
}
/** :param signal: 当前操作。:returns: 仅等待下一次只读查询，不重发控制。 */
function nextRead(signal: AbortSignal): Promise<void> {
  signal.throwIfAborted();
  return new Promise((resolve, reject) => {
    const cancelled = (): void => { clearTimeout(timer); signal.removeEventListener('abort', cancelled); reject(signal.reason); };
    const timer = setTimeout(() => { signal.removeEventListener('abort', cancelled); resolve(); }, 200);
    signal.addEventListener('abort', cancelled, { once: true });
  });
}
/** :param submission: 202回执。:param snapshot: 原分享。:returns: 只有原身份及有效命令ID可以查询。 */
function validSubmission(submission: ScreenShareAudioSubmission, snapshot: ScreenShareSnapshot): boolean {
  return Boolean(submission && typeof submission.submitted_command_id === 'string' && guid.test(submission.submitted_command_id) &&
    submission.share_id === snapshot.id && submission.window_id === snapshot.window_id && submission.source_generation === snapshot.source_generation);
}
/** :param command: 精确只读命令。:param submission: 本次受理。:returns: 身份/命令/代次及终态时间合法，不从其他命令借成功。 */
function validCommand(command: PlaybackCommandSnapshot, submission: ScreenShareAudioSubmission): boolean {
  if (!command || command.command_id !== submission.submitted_command_id || command.window_id !== submission.window_id ||
    command.command !== 'SCREEN_SHARE_AUDIO' || command.source_generation !== submission.source_generation || typeof command.result_code !== 'string' ||
    !['pending', 'processing', 'completed', 'failed', 'superseded', 'uncertain'].includes(command.status)) return false;
  return ['pending', 'processing'].includes(command.status) ||
    (typeof command.completed_at === 'string' && Number.isFinite(Date.parse(command.completed_at)));
}

export class ScreenShareAudioController {
  private action?: AudioAction;
  private readonly deps: AudioDependencies;
  /** :param dependencies: 当前分享身份、认证IO及共用有序回读。:returns: 不拥有视频资源的音频控制器。 */
  constructor(dependencies: AudioDependencies) { this.deps = dependencies; }
  /** :param action: 原操作。:returns: 当前仍是同一可控制分享，取消/换源不授予新寿命。 */
  private owns(action: AudioAction): boolean {
    const current = this.deps.current();
    return this.action === action && current !== null && current.id === action.snapshot.id &&
      current.window_id === action.snapshot.window_id && current.source_generation === action.snapshot.source_generation;
  }
  /** :returns: 视频停止或音轨/身份失效时立即取消IO；迟到结果不能更新视图。 */
  reset(): void {
    this.action?.abort.abort(new Error('share_audio_cancelled')); this.action = undefined;
    this.deps.changed({ phase: 'idle', errorCode: '' });
  }
  /** :returns: 每次实际状态更新后核音轨和身份，不把失效动作留在等待态。 */
  reconcile(): void { if (this.action && !this.owns(this.action)) this.reset(); }
  /** :param volume: 设置整数0..100。:param muted: 明确静音意图。:returns: 一次受理加精确终态/实际属性回读，有界且不影响有效视频。 */
  async set(volume: number, muted: boolean): Promise<void> {
    if (this.action) return;
    if (!Number.isInteger(volume) || volume < 0 || volume > 100 || typeof muted !== 'boolean') {
      this.deps.changed({ phase: 'failed', errorCode: 'share_audio_invalid' }); return;
    }
    const snapshot = this.deps.current();
    if (!snapshot) { this.deps.changed({ phase: 'failed', errorCode: 'share_audio_unavailable' }); return; }
    const action: AudioAction = { snapshot, abort: new AbortController() }; this.action = action;
    this.deps.changed({ phase: 'pending', errorCode: '' });
    const timer = setTimeout(() => action.abort.abort(new Error('share_audio_unconfirmed')), 8000);
    try {
      const submission = await bounded(this.deps.api.setAudio(snapshot.id, volume, muted, action.abort.signal), action.abort.signal);
      if (!this.owns(action)) return;
      if (!validSubmission(submission, snapshot)) throw new Error('share_audio_identity_invalid');
      let completed = false;
      while (this.owns(action)) {
        if (!completed) {
          const command = await bounded(this.deps.api.getAudioCommand(snapshot.window_id, submission.submitted_command_id, action.abort.signal), action.abort.signal);
          if (!this.owns(action)) return;
          if (!validCommand(command, submission)) throw new Error('share_audio_identity_invalid');
          if (command.status === 'uncertain') throw new Error('share_audio_unconfirmed');
          if (['failed', 'superseded'].includes(command.status) || (command.status === 'completed' && command.result_code !== 'ok')) throw new Error('share_audio_failed');
          completed = command.status === 'completed';
        }
        if (completed) {
          const actual = await bounded(this.deps.read(action.abort.signal), action.abort.signal);
          if (!this.owns(action)) return;
          if (actual?.actual_volume === volume && actual.actual_muted === muted) {
            this.deps.changed({ phase: 'confirmed', errorCode: '' }); return;
          }
        }
        await nextRead(action.abort.signal);
      }
    } catch (error) {
      if (!this.owns(action)) return;
      const raw = error instanceof Error ? ('code' in error && typeof error.code === 'string' ? error.code : error.message) : '';
      const known = new Set(['share_audio_invalid', 'share_audio_identity_invalid', 'share_audio_failed', 'share_audio_unavailable', 'share_player_offline', 'share_ended']);
      const code = known.has(raw) ? raw : 'share_audio_unconfirmed';
      this.deps.changed({ phase: code === 'share_audio_unconfirmed' ? 'unconfirmed' : 'failed', errorCode: code });
    } finally {
      clearTimeout(timer);
      if (this.action === action) this.action = undefined;
    }
  }
}
