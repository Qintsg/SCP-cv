/*
 * 删除请求失败后重新同步媒体库；服务端可能已提交删除但隔离区清理失败。
 */

/**
 * 保留原始删除错误，并尽力刷新服务端真实记录。
 * :param remove: 删除请求。
 * :param refresh: 媒体库刷新请求。
 * :returns: 删除成功时正常返回；失败时继续抛出原始错误。
 */
export async function runSourceDeletion(remove: () => Promise<void>, refresh: () => Promise<void>): Promise<void> {
  try {
    await remove();
  } catch (error) {
    try { await refresh(); } catch { /* 刷新失败不能覆盖原始删除错误。 */ }
    throw error;
  }
}
