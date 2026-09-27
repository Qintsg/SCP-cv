/*
 * 媒体源下载：浏览器与原生宿主共用后端下载合同。
 */
import { useI18n } from 'vue-i18n';

import { useToast } from '@/composables/useToast';
import { api, type MediaSourceItem } from '@/services/api';
import { saveResponseFile } from '@/platform/files';
import { getNativePlatformAdapter } from '@/platform/native';

/** 创建可直接绑定到媒体源菜单的下载动作。 */
export function useSourceDownload() {
  const { t } = useI18n();
  const toast = useToast();
  return async function downloadSource(source: MediaSourceItem): Promise<void> {
    try {
      const response = await fetch(api.downloadSourceUrl(source.id), { credentials: 'include' });
      const suggestedName = source.original_filename || source.name;
      const adapter = getNativePlatformAdapter();
      if (adapter) {
        const saved = await saveResponseFile(adapter, response, suggestedName);
        if (!saved) return;
      } else {
        if (!response.ok) throw new Error(`HTTP ${response.status}`);
        const blobUrl = URL.createObjectURL(await response.blob());
        const anchor = document.createElement('a');
        anchor.href = blobUrl;
        anchor.download = suggestedName;
        anchor.hidden = true;
        document.body.append(anchor);
        anchor.click();
        anchor.remove();
        window.setTimeout(() => URL.revokeObjectURL(blobUrl), 0);
      }
      toast.success(t('sources.downloadedOk', { name: source.name }));
    } catch (error) {
      toast.error(t('sources.downloadFail'), error instanceof Error ? error.message : t('common.retry'));
    }
  };
}
