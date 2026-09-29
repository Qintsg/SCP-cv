/**
 * 媒体源的类型、图标与状态标签投影，供列表和选源入口共用。
 * @Project : SCP-cv
 * @File : sourcePresentation.ts
 * @Author : Qintsg
 * @Date : 2026-09-29
 */
import { t } from '@/locales';
import type { MediaSourceItem } from '@/services/api';
import { SOURCE_TYPE_TO_CATEGORY, type SourceCategory } from '@/stores/sources';
import { pptPreparationLabelKey } from './pptPreparationPresentation';

/** NUI NTag 兼容的 type；'default' 对齐原 'subtle'。 */
export type SourceTagType = 'default' | 'info' | 'success' | 'warning' | 'error';

/** 将后端 source_type 聚合成前端可视大类。 */
export function resolveSourceCategory(source: MediaSourceItem): SourceCategory {
  return SOURCE_TYPE_TO_CATEGORY[source.source_type] ?? 'all';
}

/** 媒体源类型标签文案，保持 SourcesView 与 SourcePicker 一致。 */
export function sourceCategoryLabel(source: MediaSourceItem): string {
  switch (resolveSourceCategory(source)) {
    case 'ppt':
      return t('sources.typeLabel.ppt');
    case 'video':
      return t('sources.typeLabel.video');
    case 'audio':
      return t('sources.typeLabel.audio');
    case 'image':
      return t('sources.typeLabel.image');
    case 'web':
      return t('sources.typeLabel.web');
    case 'stream':
      return source.metadata?.stream_status === 'unverified'
        ? t('sources.typeLabel.streamUnverified')
        : t('sources.typeLabel.stream');
    default:
      return t('sources.typeLabel.other');
  }
}

/** 媒体源类型图标，供缩略图缺失或源不可预览时回退。 */
export function sourceCategoryIcon(source: MediaSourceItem): string {
  switch (resolveSourceCategory(source)) {
    case 'ppt':
      return 'document_24_regular';
    case 'video':
      return 'video_24_regular';
    case 'audio':
      return 'music_note_2_24_regular';
    case 'image':
      return 'image_24_regular';
    case 'web':
      return 'globe_24_regular';
    case 'stream':
      return 'live_24_regular';
    default:
      return 'document_24_regular';
  }
}

/** 媒体源类型标签色彩；登记可用不冒充直播已出画。 */
export function sourceCategoryTone(source: MediaSourceItem): SourceTagType {
  switch (resolveSourceCategory(source)) {
    case 'ppt':
      return 'info';
    case 'video':
      return 'success';
    case 'audio':
      return 'info';
    case 'image':
    case 'web':
      return 'default';
    case 'stream':
      return source.is_available ? 'warning' : 'error';
    default:
      return 'default';
  }
}

/**
 * 投影准备状态文案，未知非空值保留原码以便诊断。
 * :param state: 后端页图准备状态。
 * :returns: 可读状态；缺失表示页图尚未准备，不判断原件存在性。
 */
export function pptPreparationStateLabel(state: string | null | undefined): string {
  const key = pptPreparationLabelKey(state);
  return key ? t(key) : state || t('sources.preparationState.missing');
}

/**
 * 区分 PPT 准备与真正离线，供源列表和切换面板共用。
 * :param source: 不可用的媒体记录。
 * :returns: 可读准备/故障原因。
 */
export function sourceAvailabilityLabel(source: MediaSourceItem): string {
  if (source.source_type !== 'ppt' || source.playback_mode === 'pdf') return t('sources.offline');
  if (source.preparation_state === 'queued' || source.preparation_state === 'running') return t('sources.preparing');
  if (source.preparation_state === 'uncertain') return t('sources.prepareUncertain');
  if (source.preparation_state === 'failed') return t('sources.prepareFailed');
  // 实验模式的源仍可报告 slide_images；就绪页图不等于原件当前可用。
  if (source.preparation_state === 'ready') return t('sources.preparedSourceUnavailable');
  if (source.preparation_state && source.preparation_state !== 'missing') return source.preparation_state;
  return t('sources.prepareMissing');
}
