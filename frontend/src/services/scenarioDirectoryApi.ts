/* 预设逻辑目录API，复用认证/CSRF和主机代次边界。 */
import { requestJson, type MediaFolderItem } from './api';
export const scenarioDirectoryApi = {
  list: () => requestJson<{ folders: MediaFolderItem[] }>('/api/scenario-folders/'),
  create: (name: string, parent_id: number | null) => requestJson('/api/scenario-folders/', { method: 'POST', body: JSON.stringify({ name, parent_id }) }),
  update: (id: number, name: string, parent_id: number | null) => requestJson(`/api/scenario-folders/${id}/`, { method: 'PUT', body: JSON.stringify({ name, parent_id }) }),
  remove: (id: number) => requestJson(`/api/scenario-folders/${id}/`, { method: 'DELETE' }),
  move: (id: number, folder_id: number | null) => requestJson(`/api/scenarios/${id}/`, { method: 'PATCH', body: JSON.stringify({ folder_id }) }),
};
