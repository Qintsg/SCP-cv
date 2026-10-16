/** 原生恢复阶段顺序：每个await后复核调用寿命，失效不续读或建SSE。 */
export interface ResumeDependencies {
  canContinue?(): boolean;
  hasServerProfile(): boolean;
  disconnectEvents(): void;
  probeSession(): Promise<boolean>;
  refreshSnapshot(): Promise<void>;
  connectEvents(): void;
}

/** :param dependencies: 当前恢复阶段与归属检查。:returns: 原身份完成恢复；失效不续读或建SSE。 */
export async function resumeClientConnection(dependencies: ResumeDependencies): Promise<boolean> {
  const current = dependencies.canContinue ?? (() => true);
  if (!current()) return false;
  dependencies.disconnectEvents();
  if (!current() || !dependencies.hasServerProfile()) return false;
  const authenticated = await dependencies.probeSession();
  if (!current() || !authenticated) return false;
  await dependencies.refreshSnapshot();
  if (!current()) return false;
  dependencies.connectEvents();
  return true;
}
