/** 目录路径与范围纯逻辑；不同页面各持当前位置。SCP-cv 2026-10-12。 */
export interface DirectoryEntry { id: number; name: string; parent_id: number | null; relative_path?: string; is_protected?: boolean }

/** :param id: 当前目录，null为根。:param folders: 扁平树。:returns: 有界父链，从/开始。 */
export function directoryPath(id: number | null, folders: DirectoryEntry[]): string {
  const parts: string[] = [], seen = new Set<number>();
  while (id !== null && !seen.has(id)) {
    seen.add(id); const folder = folders.find(item => item.id === id);
    if (!folder) break;
    parts.unshift(folder.name); id = folder.parent_id;
  }
  return '/' + parts.join('/');
}
/** :param id: 源目录。:param current: 当前位置。:param folders: 目录树。:param recursive: 是否包含子树。:returns: 此源是否在浏览范围。 */
export function inDirectory(id: number | null, current: number | null, folders: DirectoryEntry[], recursive: boolean): boolean {
  id = id ?? null; // 旧主机/旧来源未返回folder_id时属于根目录。
  if (id === current) return true;
  if (!recursive) return false;
  if (current === null) return true;
  const seen = new Set<number>();
  while (id !== null && !seen.has(id)) {
    seen.add(id); id = folders.find(item => item.id === id)?.parent_id ?? null;
    if (id === current) return true;
  }
  return false;
}
/** :param source: 媒体投影。:param folders: 目录树。:returns: 后端实际路径优先，旧主机仅逻辑回退。 */
export function sourcePath(source: { path?: string; folder_id: number | null; name: string; original_filename?: string }, folders: DirectoryEntry[]): string {
  if (source.path?.startsWith('/')) return source.path;
  const parent = directoryPath(source.folder_id, folders);
  return (parent === '/' ? parent : parent + '/') + (source.original_filename || source.name);
}
