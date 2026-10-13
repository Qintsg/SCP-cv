/* 云台按住租约；松手先清除本地状态，迟到开始响应不能恢复心跳。 */
import type { CameraDirection, CameraMotionRequest } from '@/services/venueApi';

export class CameraMotionLease {
  private active: CameraMotionRequest | null = null;
  private timer: ReturnType<typeof setInterval> | null = null;
  private renewing = false;
  constructor(private readonly send: (request: CameraMotionRequest) => Promise<unknown>,
    private readonly report: (failure: unknown) => void, private readonly changed: (direction: CameraDirection | null) => void,
    private readonly clock: () => number = Date.now, private readonly id: () => string = () => crypto.randomUUID()) {}

  /** :param direction: 已绑定方向。:returns: 新令牌开始；旧运动先释放，失败不重发按下。 */
  start(direction: CameraDirection): void {
    this.stop();
    const request: CameraMotionRequest = { direction, motion_id: this.id(), phase: 'start', issued_at_ms: this.clock() };
    this.active = request; this.changed(direction);
    void this.send(request).then(() => {
      if (this.active?.motion_id !== request.motion_id) return;
      this.timer = setInterval(() => {
        if (this.renewing || this.active?.motion_id !== request.motion_id) return;
        this.renewing = true;
        void this.send({ ...request, phase: 'hold' }).catch(failure => {
          if (this.active?.motion_id === request.motion_id) { this.stop(); this.report(failure); }
        }).finally(() => { this.renewing = false; });
      }, 300);
    }).catch(failure => {
      if (this.active?.motion_id === request.motion_id) { this.stop(); this.report(failure); }
    });
  }

  /** :returns: 松开/失焦/卸载时独立发送停止，不等待仍在路上的开始响应。 */
  stop(): void {
    const request = this.active; this.active = null;
    if (this.timer !== null) clearInterval(this.timer);
    this.timer = null; this.changed(null);
    if (request) void this.send({ ...request, phase: 'stop' }).catch(failure => this.report(failure));
  }
}
