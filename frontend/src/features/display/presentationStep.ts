/**
 * 文件描述：文稿按钮与键盘使用的统一步进动作。
 * 项目：SCP-cv；文件：presentationStep.ts；作者：Qintsg；日期：2026-10-27。
 */
type Step = { kind: 'effect'; action: 'previous_effect' | 'next_effect'; command: 'PREVIOUS_EFFECT' | 'NEXT_EFFECT' }
  | { kind: 'slide'; action: 'prev' | 'next'; command: 'PREV' | 'NEXT' };

/** :param mode: 实际播放模式。:param direction: 用户步进方向。:returns: 既有播放合同中的动作。 */
export function presentationStep(mode: string | null | undefined, direction: 'prev' | 'next'): Step {
  if (mode === 'libreoffice') return direction === 'next'
    ? { kind: 'effect', action: 'next_effect', command: 'NEXT_EFFECT' }
    : { kind: 'effect', action: 'previous_effect', command: 'PREVIOUS_EFFECT' };
  return { kind: 'slide', action: direction, command: direction === 'next' ? 'NEXT' : 'PREV' };
}
