/** 文件描述：独立于页面可见性的共享轮询时钟。项目：SCP-cv；文件：shareClock.ts；作者：Qintsg；日期：2026-10-30。 */
const clocks = new WeakMap<AbortSignal, Worker>();

/** :param signal: 采集尝试的寿命。:returns: 下次工作线程时钟；停止立即清理工作线程与等待者。 */
export function waitShareTick(signal: AbortSignal): Promise<void> {
  signal.throwIfAborted();
  let clock = clocks.get(signal);
  if (!clock) {
    clock = new Worker(new URL('./shareClock.worker.ts', import.meta.url), { type: 'module', name: 'screen-share-clock' });
    clocks.set(signal, clock);
    signal.addEventListener('abort', () => { clock!.terminate(); clocks.delete(signal); }, { once: true });
  }
  const current = clock;
  return new Promise((resolve, reject) => {
    const clear = () => { current.removeEventListener('message', tick); current.removeEventListener('error', failed); signal.removeEventListener('abort', aborted); };
    const tick = () => { clear(); resolve(); };
    const aborted = () => { clear(); reject(signal.reason); };
    const failed = () => { clear(); reject(new Error('share_connection_failed')); };
    current.addEventListener('message', tick, { once: true }); current.addEventListener('error', failed, { once: true });
    signal.addEventListener('abort', aborted, { once: true });
    if (signal.aborted) aborted();
  });
}
