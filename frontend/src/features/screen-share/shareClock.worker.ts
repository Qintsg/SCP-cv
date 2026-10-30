/** 文件描述：共享心跳专用工作线程时钟。项目：SCP-cv；文件：shareClock.worker.ts；作者：Qintsg；日期：2026-10-30。 */
setInterval(() => globalThis.postMessage(null), 1000);
export {};
