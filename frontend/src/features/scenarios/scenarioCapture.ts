/** 从实际播放快照生成独立预案草稿；未知窗口保持原状，不保存失败请求或发送命令。 */
import type { RuntimeSnapshot, SessionSnapshot } from '@/services/api';
import { createEmptyDraft, type ScenarioDraft } from './scenarioModel';

interface CaptureInput {
  sessions: readonly SessionSnapshot[];
  runtime: Pick<RuntimeSnapshot, 'big_screen_mode'> | null;
  volume: { level: number; systemSynced: boolean };
  folderId: number | null;
}

interface CaptureResult {
  draft: ScenarioDraft | null;
  sharedWindowId: number | null;
  omittedWindowIds: number[];
  omittedSystemVolume: boolean;
}

/** :param input: 已有状态快照。:returns: 独立草稿及未捕获项；临时共享整次拒绝。 */
export function captureScenarioState(input: CaptureInput): CaptureResult {
  const shared = input.sessions.find(session => session.share_id || session.actual_share_id
    || session.actual_adapter_kind === 'screen_share');
  if (shared) return { draft: null, sharedWindowId: shared.window_id, omittedWindowIds: [], omittedSystemVolume: false };

  const draft = createEmptyDraft();
  draft.folderId = input.folderId;
  const mode = input.runtime?.big_screen_mode;
  if (mode === 'single' || mode === 'double') {
    draft.bigScreenModeState = 'set';
    draft.bigScreenMode = mode;
  }
  const volumeKnown = input.volume.systemSynced && Number.isFinite(input.volume.level)
    && input.volume.level >= 0 && input.volume.level <= 100;
  if (volumeKnown) {
    draft.volumeState = 'set';
    draft.volumeLevel = input.volume.level;
  }
  const omittedWindowIds: number[] = [];
  draft.windows = draft.windows.map(window => {
    const session = input.sessions.find(item => item.window_id === window.windowId);
    const stable = session?.player_online && !session.pending_command && !session.cleanup_pending;
    if (stable && session.playback_state === 'idle' && session.actual_source_id === null) return window;
    const actualId = session?.actual_source_id;
    if (!stable || !Number.isSafeInteger(actualId) || (actualId ?? 0) <= 0
      || !Number.isSafeInteger(session.observed_generation) || (session.observed_generation ?? 0) <= 0
      || !['playing', 'paused'].includes(session.playback_state)) {
      omittedWindowIds.push(window.windowId);
      return window;
    }
    return { ...window, sourceState: 'set', sourceId: actualId!, autoplay: session.playback_state === 'playing', resume: false };
  });
  return { draft, sharedWindowId: null, omittedWindowIds, omittedSystemVolume: !volumeKnown };
}
