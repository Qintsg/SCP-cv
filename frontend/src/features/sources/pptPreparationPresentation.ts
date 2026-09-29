/**
 * PPT 页图准备状态的稳定文案键；不把页图缺失误解为原件不存在。
 * @Project : SCP-cv
 * @File : pptPreparationPresentation.ts
 * @Author : Qintsg
 * @Date : 2026-09-29
 */
const PREPARATION_LABEL_KEYS: Readonly<Record<string, string>> = {
  queued: 'sources.preparationState.queued',
  running: 'sources.preparationState.running',
  ready: 'sources.preparationState.ready',
  failed: 'sources.preparationState.failed',
  uncertain: 'sources.preparationState.uncertain',
  missing: 'sources.preparationState.missing',
};

/**
 * 为已知准备状态提供中文键，未知值由界面原样保留用于诊断。
 * :param state: 后端准备状态；旧记录可能尚未提供。
 * :returns: 已知文案键，未知非空状态返回空值。
 */
export function pptPreparationLabelKey(state: string | null | undefined): string | null {
  const key = state || 'missing';
  return Object.hasOwn(PREPARATION_LABEL_KEYS, key) ? PREPARATION_LABEL_KEYS[key]! : null;
}
