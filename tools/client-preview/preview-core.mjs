/** 用户本机FFmpeg只读预览核心：受限目标、共享进程、有界帧和显式释放，不依赖D4。 */
import { spawn } from 'node:child_process';
import { createHash, randomBytes } from 'node:crypto';
import { isIP } from 'node:net';

/** :param raw: 请求body中的地址，不进入日志。:returns: 规范化受限RTSP/RTMP地址。 */
export function validateStream(raw) {
  if (typeof raw !== 'string' || raw.length > 4096 || /[\x00-\x20\x7f]/.test(raw)) throw new Error('invalid_stream');
  let url; try { url = new URL(raw); } catch { throw new Error('invalid_stream'); }
  if (!['rtsp:', 'rtmp:'].includes(url.protocol) || isIP(url.hostname) !== 4 || url.hash) throw new Error('invalid_stream');
  const [a, b] = url.hostname.split('.').map(Number);
  if (!(a === 10 || a === 127 || a === 192 && b === 168 || a === 172 && b >= 16 && b <= 31)) throw new Error('target_not_local');
  return url.href;
}

/** :param options: 本机进程和预算。:returns: 可共享且必须关闭的会话管理器。 */
export function createPreviewPool({ ffmpeg = 'ffmpeg', startProcess = spawn, maximum = 6,
  now = Date.now, firstFrameMs = 10000, staleMs = 8000, leaseMs = 30000 } = {}) {
  const workers = new Map(), sessions = new Map();
  let closed = false;
  /** :param worker: 本池拥有的子进程。:returns: 从池移除并只终止该精确进程。 */
  function remove(worker) {
    if (workers.get(worker.key) !== worker) return;
    workers.delete(worker.key); worker.child.kill(); worker.frame = null; worker.failed = true;
    for (const [id, session] of sessions) if (session.worker === worker) sessions.delete(id);
    for (const done of worker.waiters) done(false); worker.waiters.clear();
  }
  /** :param worker: 帧工作进程。:param chunk: pipe数据。:returns: 仅保留最新完整JPEG，有界缓存。 */
  function consume(worker, chunk) {
    worker.buffer = Buffer.concat([worker.buffer, chunk]);
    if (worker.buffer.length > 4 * 1024 * 1024) { remove(worker); return; }
    for (;;) {
      const start = worker.buffer.indexOf(Buffer.from([255, 216]));
      if (start < 0) { worker.buffer = worker.buffer.subarray(-1); return; }
      const end = worker.buffer.indexOf(Buffer.from([255, 217]), start + 2);
      if (end < 0) { worker.buffer = worker.buffer.subarray(start); return; }
      worker.frame = Buffer.from(worker.buffer.subarray(start, end + 2)); worker.lastFrame = now();
      worker.buffer = worker.buffer.subarray(end + 2);
      for (const done of worker.waiters) done(true); worker.waiters.clear();
    }
  }
  return {
    /** :param raw: 地址。:param quality: low/high。:returns: 随机订阅ID，不返回上游地址。 */
    async open(raw, quality) {
      if (closed) throw new Error('closed');
      const url = validateStream(raw);
      if (!['low', 'high'].includes(quality)) throw new Error('invalid_quality');
      if (sessions.size >= 64) throw new Error('too_many_sessions');
      const key = createHash('sha256').update(url).update(quality).digest('hex');
      let worker = workers.get(key);
      if (!worker) {
        if (workers.size >= maximum) throw new Error('preview_capacity');
        const args = ['-hide_banner', '-loglevel', 'error', '-nostdin', '-protocol_whitelist', 'rtsp,rtmp,tcp,udp',
          ...(url.startsWith('rtsp:') ? ['-rtsp_transport', 'tcp', '-timeout', '8000000'] : ['-rw_timeout', '8000000']), '-i', url,
          '-map', '0:v:0', '-an', '-sn', '-dn', '-vf', `fps=${quality === 'high' ? 4 : 2},scale=${quality === 'high' ? 1600 : 640}:-2`,
          '-q:v', quality === 'high' ? '3' : '5', '-f', 'image2pipe', '-vcodec', 'mjpeg', 'pipe:1'];
        const child = startProcess(ffmpeg, args, { shell: false, windowsHide: true, stdio: ['ignore', 'pipe', 'ignore'] });
        worker = { key, child, buffer: Buffer.alloc(0), frame: null, lastFrame: now(), waiters: new Set(), failed: false };
        const owner = worker; workers.set(key, worker);
        child.stdout.on('data', chunk => consume(owner, chunk));
        child.once('error', () => remove(owner)); child.once('exit', () => remove(owner));
      }
      const current = worker;
      if (!current.frame) {
        const ready = await new Promise(resolve => {
          const timeout = setTimeout(() => { current.waiters.delete(done); resolve(false); }, firstFrameMs);
          const done = success => { clearTimeout(timeout); resolve(success); }; current.waiters.add(done);
        });
        if (!ready || closed || current.failed) { remove(current); throw new Error('stream_unavailable'); }
      }
      // 等待首帧期间其它请求也可能取得订阅，恢复后必须再次核预算，不能并发穿透。
      if (sessions.size >= 64) throw new Error('too_many_sessions');
      const id = randomBytes(16).toString('hex'); sessions.set(id, { worker: current, touched: now() }); return id;
    },
    /** :param id: 本机订阅。:returns: 最新帧，不存在或停帧明确失败。 */
    frame(id) {
      const session = sessions.get(id);
      if (!session || session.worker.failed || !session.worker.frame) throw new Error('preview_missing');
      if (now() - session.worker.lastFrame > staleMs) { remove(session.worker); throw new Error('stream_stalled'); }
      session.touched = now(); return session.worker.frame;
    },
    /** :param id: 明确订阅。:returns: 仅释放其引用，最后一个订阅释放时终止上游。 */
    release(id) {
      const session = sessions.get(id); if (!session) return;
      sessions.delete(id);
      if (![...sessions.values()].some(item => item.worker === session.worker)) remove(session.worker);
    },
    /** :returns: 定期收回未续租订阅，页面异常退出也能终止拉流。 */
    sweep() { for (const [id, session] of sessions) if (now() - session.touched > leaseMs) this.release(id); },
    /** :returns: 关闭本池全部自有进程，不匹配或清扫其它FFmpeg。 */
    close() { closed = true; for (const worker of workers.values()) remove(worker); sessions.clear(); },
    /** :returns: 不包含地址和凭据的资源计数。 */
    stats() { return { workers: workers.size, sessions: sessions.size }; },
  };
}
