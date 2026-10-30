/** 文件描述：媒体预览、编辑与文件操作菜单。项目：SCP-cv；文件：useSourceActions.ts；作者：Qintsg；日期：2026-10-30。 */
import { h } from 'vue';
import { useI18n } from 'vue-i18n';
import FIcon from '@/design-system/FIcon.vue';
import type { ActionDropdownOption } from '@/design-system/dropdownActions';
import type { MediaSourceItem } from '@/services/api';
import { useSourceStore } from '@/stores/sources';
import { useBackgroundAudioStore } from '@/stores/backgroundAudio';
import { useDialog } from '@/composables/useDialog';
import { useToast } from '@/composables/useToast';
import { useSourceDownload } from './useSourceDownload';

/** :param edit: 编辑入口。:param open: 打开到卡片。:param moves: 普通目录目标。:returns: 只有select执行的菜单。 */
export function useSourceActions(edit: (source: MediaSourceItem) => void, open: (source: MediaSourceItem) => void,
  moves: (source: MediaSourceItem) => ActionDropdownOption[], preview: (source: MediaSourceItem) => void) {
  const { t } = useI18n(), store = useSourceStore(), audio = useBackgroundAudioStore(), dialog = useDialog(), toast = useToast();
  const download = useSourceDownload();
  /** :param source: 普通源。:returns: 确认后删除，失败仍保留记录/反馈。 */
  async function remove(source: MediaSourceItem): Promise<void> {
    const current = dialog.captureContext();
    if (source.is_protected || !await dialog.danger({ title: t('sources.deleteTitle', { name: source.name }),
      description: t('sources.deleteDesc'), confirmLabel: t('sources.deleteSource') }) || !current()) return;
    try { await store.deleteSource(source.id, current); if (current()) toast.success(t('sources.deletedOk')); }
    catch (error) { if (current()) toast.error(t('sources.deleteFail'), error instanceof Error ? error.message : t('common.retry')); }
  }
  /** :param source: 音频源。:param play: 立即播放或仅加入列表。:returns: 不走大屏窗口。 */
  async function useAudio(source: MediaSourceItem, play: boolean): Promise<void> {
    const current = dialog.captureContext(), name = source.name;
    try { if (play) await audio.playSource(source.id); else await audio.addSource(source.id);
      if (current()) toast.success(play ? t('backgroundAudio.playRequested', { name }) : t('sources.addedToBackgroundAudioOk', { name })); }
    catch (error) { if (current()) toast.error(t('sources.backgroundAudioFail'), error instanceof Error ? error.message : t('common.retry')); }
  }
  /** :param source: 当前行。:returns: 内置源仅播放，不暴露可写动作。 */
  function buildRowMenu(source: MediaSourceItem): ActionDropdownOption[] {
    const icon = (name: string) => () => h(FIcon, { name, size: 18 });
    const playable: ActionDropdownOption[] = source.source_type === 'audio' ? [
      { label: t('sources.playAsBackgroundAudio'), key: 'play-background-audio', action: () => useAudio(source, true) },
      { label: t('sources.addToBackgroundAudio'), key: 'add-background-audio', action: () => useAudio(source, false) },
    ] : [{ label: '打开到…', key: 'open-to', disabled: !source.is_available, action: () => open(source) }];
    const actions = [{ label: '预览', key: 'preview', action: () => preview(source) }, ...playable];
    if (source.is_protected) return actions;
    return [...actions, { type: 'divider', key: 'divider-1' },
      { label: t('common.edit'), key: 'edit', icon: icon('edit_24_regular'), action: () => edit(source) },
      { label: t('sources.moveToFolder'), key: 'move-to', icon: icon('folder_24_regular'), children: moves(source) },
      { label: t('sources.download'), key: 'download', disabled: !source.file_size, action: () => { if (source.file_size) void download(source); } },
      { label: t('sources.deleteSource'), key: 'delete', icon: icon('delete_24_regular'), action: () => remove(source),
        props: { style: 'color: var(--colorStatusDangerForeground1);' } },
    ];
  }
  return buildRowMenu;
}
