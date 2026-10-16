/*
 * 媒体源 Store：列表、按类型筛选、上传、删除、文件夹管理。
 *
 *   - 文件夹支持层级组织，可创建、重命名、删除和移动源；
 *   - 直播源聚合 srt_stream / rtsp_stream / custom_stream 三种 source_type；
 *   - UI 提供「上传文件 / 网页 / 直播流」三种添加入口；
 *   - audio 源作为背景音乐入口展示，不再复用窗口播放控制。
 */
import { defineStore } from 'pinia';
import { runSourceDeletion } from './sourceDeletion';
import { captureStoreRequestScope, assertStoreRequestScope } from './requestScope';
import { inDirectory, sourcePath } from '@/features/directories/directoryPath';

import { api, type MediaFolderItem, type MediaSourceItem, type MediaSourceUpdate, type StreamSourceCreate, type UploadOptions } from '@/services/api';

/** UI 可视的源大类；与后端 source_type 不一一映射，直播由前端聚合。 */
export type SourceCategory = 'all' | 'ppt' | 'video' | 'audio' | 'image' | 'web' | 'stream';

/**
 * 后端真实 source_type 字段映射到前端大类的策略。
 * 注意：`audio` 有独立背景音乐大类，不再出现在窗口打开动作里。
 */
const SOURCE_TYPE_TO_CATEGORY: Record<string, SourceCategory> = {
  ppt: 'ppt',
  video: 'video',
  image: 'image',
  audio: 'audio',
  web: 'web',
  srt_stream: 'stream',
  rtsp_stream: 'stream',
  custom_stream: 'stream',
};

/**
 * 判断后端返回的源是否对 UI 可见。
 * @param source 后端返回的媒体源
 * @return 是否纳入 store 展示列表
 */
function isVisibleSource(source: MediaSourceItem): boolean {
  return Boolean(source.source_type);
}

interface SourceState {
  sources: MediaSourceItem[];
  /** 当前会话引用但不进入媒体库的临时上传源。 */
  transientSources: MediaSourceItem[];
  /** 当前选中的类型 Tab；默认 all。 */
  category: SourceCategory;
  /** 名称/URL 即时搜索关键字。 */
  searchKeyword: string;
  /** 全部文件夹列表（扁平）。 */
  folders: MediaFolderItem[];
  /** 当前浏览的文件夹 ID；null 表示根目录。 */
  currentFolderId: number | null;
  searchSubfolders: boolean;
}

const refreshOwners = new WeakMap<SourceState, symbol>();

export const useSourceStore = defineStore('sources', {
  state: (): SourceState => ({
    sources: [],
    transientSources: [],
    category: 'all',
    searchKeyword: '',
    folders: [],
    currentFolderId: null,
    searchSubfolders: true,
  }),
  getters: {
    findById: (state) => (sourceId: number | null): MediaSourceItem | undefined => (
      sourceId === null
        ? undefined
        : [...state.transientSources, ...state.sources].find((source) => source.id === sourceId)
    ),
    /** 类型计数：用于侧栏 NavList / Pills 数字徽章。 */
    countByCategory(state): Record<SourceCategory, number> {
      const result: Record<SourceCategory, number> = {
        all: 0,
        ppt: 0,
        video: 0,
        audio: 0,
        image: 0,
        web: 0,
        stream: 0,
      };
      for (const source of state.sources) {
        if (source.folder_id !== state.currentFolderId) continue;
        result.all += 1;
        const category = SOURCE_TYPE_TO_CATEGORY[source.source_type];
        if (category && category !== 'all') result[category] += 1;
      }
      return result;
    },
    /**
     * 当前 Tab + 搜索过滤后的列表，按显示名称首字母升序排列，
     * 中文走 `localeCompare('zh-Hans-CN')` 自动按拼音首字母分组，
     * 英文则按字母顺序，确保大量源时操作员能快速定位。
     */
    filtered(state): MediaSourceItem[] {
      const keyword = state.searchKeyword.trim().toLowerCase();
      const matched = state.sources.filter((source) => {
        if (!inDirectory(source.folder_id, state.currentFolderId, state.folders, !!keyword && state.searchSubfolders)) return false;
        if (state.category !== 'all') {
          const cat = SOURCE_TYPE_TO_CATEGORY[source.source_type];
          if (cat !== state.category) return false;
        }
        if (!keyword) return true;
        const name = source.name?.toLowerCase() ?? '';
        const uri = source.uri?.toLowerCase() ?? '';
        const original = source.original_filename?.toLowerCase() ?? '';
        return name.includes(keyword) || uri.includes(keyword) || original.includes(keyword)
          || sourcePath(source, state.folders).toLowerCase().includes(keyword);
      });
      // 优先使用 `co-pinyin` collation 按汉语拼音首字母对中文排序，与英文混排；
      // 不支持该 collation 的运行时（部分 Node / 旧 Chromium）会自动 fallback 到 'zh-Hans-CN'。
      const collator = new Intl.Collator(['zh-Hans-CN-u-co-pinyin', 'zh-Hans-CN'], { numeric: true, sensitivity: 'base' });
      return [...matched].sort((a, b) => collator.compare(a.name ?? '', b.name ?? ''));
    },
    /** 占用空间统计（仅文件型源）。 */
    totalBytes(state): number {
      return state.sources.reduce((acc, source) => acc + (source.file_size || 0), 0);
    },
    /** 当前文件夹下的子文件夹列表。 */
    childFolders(state): MediaFolderItem[] {
      return state.folders
        .filter((folder) => folder.parent_id === state.currentFolderId)
        .sort((a, b) => a.name.localeCompare(b.name, 'zh-Hans-CN'));
    },
    /** 当前文件夹的面包屑路径（从根到当前）。 */
    folderBreadcrumbs(state): MediaFolderItem[] {
      const crumbs: MediaFolderItem[] = [];
      let current = state.folders.find((folder) => folder.id === state.currentFolderId);
      while (current) {
        crumbs.unshift(current);
        current = state.folders.find((folder) => folder.id === current!.parent_id);
      }
      return crumbs;
    },
  },
  actions: {
    setCategory(category: SourceCategory): void {
      this.category = category;
    },
    setSearchKeyword(keyword: string): void {
      this.searchKeyword = keyword;
    },
    /** :returns: 仅最新有效读取原子提交两个索引，过期读取不投影。 */
    async refresh(): Promise<void> {
      const current = captureStoreRequestScope(), owner = Symbol('source-refresh');
      refreshOwners.set(this, owner);
      try {
        const [sourcePayload, folderPayload] = await Promise.all([api.listSources(), api.listFolders()]);
        if (!current() || refreshOwners.get(this) !== owner) return;
        // 同次完整索引仅提交给仍属于原认证的最新读取。
        this.$patch(state => {
          state.sources = sourcePayload.sources.filter(isVisibleSource);
          state.folders = folderPayload.folders;
        });
      } finally { if (refreshOwners.get(this) === owner) refreshOwners.delete(this); }
    },
    /** 切换到指定文件夹；null 返回根目录。 */
    setCurrentFolder(folderId: number | null): void {
      this.currentFolderId = folderId;
    },
    /** :param name: 冻结名称。:param parentId: 冻结父目录。:returns: 当前归属的已创建目录。 */
    async createFolder(name: string, parentId: number | null = null): Promise<MediaFolderItem> {
      const current = captureStoreRequestScope();
      const payload = await api.createFolder({ name, parent_id: parentId });
      assertStoreRequestScope(current);
      this.folders = [...this.folders, payload.folder];
      return payload.folder;
    },
    /** :param folderId: 目录ID。:param name: 冻结名称。:param canContinue: 可选页面寿命。:returns: 有效归属才续读完整路径索引。 */
    async renameFolder(folderId: number, name: string, canContinue?: () => boolean): Promise<void> {
      const current = captureStoreRequestScope();
      await api.updateFolder(folderId, { name });
      assertStoreRequestScope(current);
      if (canContinue && !canContinue()) return;
      await this.refresh();
    },
    /** :param folderId: 目录ID。:param parentId: 目标父目录。:param canContinue: 可选页面寿命。:returns: 不向已离开的页面续读。 */
    async moveFolder(folderId: number, parentId: number | null, canContinue?: () => boolean): Promise<void> {
      const current = captureStoreRequestScope();
      await api.updateFolder(folderId, { parent_id: parentId });
      assertStoreRequestScope(current);
      if (canContinue && !canContinue()) return;
      await this.refresh();
    },
    /** :param folderId: 目录ID。:param deleteContents: 冻结的递归选项。:param canContinue: 可选页面寿命。:returns: 当前页面才变更路径并续读。 */
    async deleteFolder(folderId: number, deleteContents: boolean = false, canContinue?: () => boolean): Promise<void> {
      const current = captureStoreRequestScope();
      const parent = this.folders.find((folder) => folder.id === folderId)?.parent_id ?? null;
      await api.deleteFolder(folderId, deleteContents);
      assertStoreRequestScope(current);
      if (canContinue && !canContinue()) return;
      // 删除子树后当前路径可能已失效，统一返回被删根目录的父级。
      let cursor = this.currentFolderId;
      while (cursor !== null) {
        if (cursor === folderId) { this.currentFolderId = parent; break; }
        cursor = this.folders.find((folder) => folder.id === cursor)?.parent_id ?? null;
      }
      await this.refresh();
    },
    /** :param sourceId: 原源ID。:param folderId: 目标目录。:returns: 当前归属的源，不以旧结果覆盖新账号。 */
    async moveSource(sourceId: number, folderId: number | null): Promise<MediaSourceItem> {
      const current = captureStoreRequestScope();
      const payload = await api.moveSource(sourceId, folderId);
      assertStoreRequestScope(current);
      this.sources = this.sources.map((item) => item.id === sourceId ? payload.source : item);
      return payload.source;
    },
    /** 上传源；支持「上传但不保存（is_temporary）」/「上传并保存」两种语义。 */
    async upload(file: File, options: { name?: string; isTemporary?: boolean; preheatEnabled?: boolean; folderId?: number | null; onProgress?: (percent: number) => void }): Promise<MediaSourceItem> {
      const current = captureStoreRequestScope();
      const formData = new FormData();
      formData.append('file', file);
      if (options.name) formData.append('name', options.name);
      if (options.isTemporary) formData.append('is_temporary', 'true');
      if (options.preheatEnabled !== undefined) formData.append('preheat_enabled', String(options.preheatEnabled));
      if (options.folderId !== undefined && options.folderId !== null) formData.append('folder_id', String(options.folderId));
      const uploadOptions: UploadOptions = options.onProgress ? { onProgress: options.onProgress } : {};
      const payload = await api.uploadSource(formData, uploadOptions);
      assertStoreRequestScope(current);
      // 仅持久源加入列表；临时源不入列表（避免出现在管理页）。
      if (options.isTemporary && isVisibleSource(payload.source)) {
        this.transientSources = [
          payload.source,
          ...this.transientSources.filter((source) => source.id !== payload.source.id),
        ].slice(0, 8);
      } else if (isVisibleSource(payload.source)) {
        this.sources = [payload.source, ...this.sources];
      }
      return payload.source;
    },
    /**
     * 添加网页/URL 源。
     * @param url 网页地址或 ip:port
     * @param name 显示名称；省略时后端用 URL 截前 80 字符作名称
     * @param preheatEnabled 启动时是否预热网页源（默认 true）
     * @param folderId 目标文件夹 ID；null 为根目录
     */
    async addWebSource(url: string, name?: string, preheatEnabled: boolean = true, folderId: number | null = null): Promise<MediaSourceItem> {
      const current = captureStoreRequestScope();
      const payload = await api.addWebSource({ url, name, preheat_enabled: preheatEnabled, folder_id: folderId });
      assertStoreRequestScope(current);
      // 网页源不会是 audio，直接前置即可。
      this.sources = [payload.source, ...this.sources];
      return payload.source;
    },
    /** 登记可尝试播放的流地址，实际连通与画面须由目标窗口验证。 */
    async addStreamSource(input: StreamSourceCreate): Promise<MediaSourceItem> {
      const current = captureStoreRequestScope();
      const payload = await api.addStreamSource(input);
      assertStoreRequestScope(current);
      this.sources = [payload.source, ...this.sources];
      return payload.source;
    },
    /**
     * 更新已存在源的可编辑字段。
     * @param sourceId 源主键
     * @param patch 仅传入需要修改的字段；未传字段保持后端原值
     */
    async updateSource(sourceId: number, patch: MediaSourceUpdate): Promise<MediaSourceItem> {
      const current = captureStoreRequestScope();
      const payload = await api.updateSource(sourceId, patch);
      assertStoreRequestScope(current);
      this.sources = this.sources.map((item) => (item.id === sourceId ? payload.source : item));
      return payload.source;
    },
    async retryPptImages(sourceId: number, fitText = false): Promise<MediaSourceItem> {
      const current = captureStoreRequestScope();
      const payload = await api.preparePptImages(sourceId, fitText);
      assertStoreRequestScope(current);
      this.sources = this.sources.map((item) => item.id === sourceId ? payload.source : item);
      return payload.source;
    },
    /**
     * 删除源；失败时同步真实列表并保留原始错误。
     * :param sourceId: 媒体源主键。
     * :param canContinue: 可选页面寿命；页面已离开时不再发恢复读取。
     * :returns: 删除完成或重新抛出错误。
     */
    async deleteSource(sourceId: number, canContinue?: () => boolean): Promise<void> {
      const current = captureStoreRequestScope();
      await runSourceDeletion(async () => { await api.deleteSource(sourceId); }, async () => {
        if (current() && (!canContinue || canContinue())) await this.refresh();
      });
      assertStoreRequestScope(current);
      this.sources = this.sources.filter((source) => source.id !== sourceId);
    },
    /**
     * 把后端 source_type 字段映射到前端 UI 大类，便于在视图层判断。
     * @param sourceType 后端 source_type
     * @return 前端 UI 大类
     */
    resolveCategory(sourceType: string): SourceCategory {
      return SOURCE_TYPE_TO_CATEGORY[sourceType] ?? 'all';
    },
  },
});

export { SOURCE_TYPE_TO_CATEGORY };
