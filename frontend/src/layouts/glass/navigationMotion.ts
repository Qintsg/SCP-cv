// 文件描述：底栏镜片的连续运动、松手选择与弹簧生命周期。
// 项目：SCP-cv；文件：navigationMotion.ts；作者：Qintsg；日期：2026-10-28。
import { animate, motionValue } from 'motion';

export interface NavigationFrame { position: number; pressed: number; stretch: number }
export interface NavigationMotionOptions {
  count: number;
  activeIndex: () => number;
  reducedMotion: () => boolean;
  paint: (frame: NavigationFrame) => void;
  select: (index: number) => Promise<void>;
}

/** :param options: 实际路由、绘制与选择接口。:returns: 可释放的镜片运动控制器。 */
export function createNavigationMotion(options: NavigationMotionOptions) {
  const position = motionValue(options.activeIndex()), pressed = motionValue(0), stretch = motionValue(0);
  const values = [position, pressed, stretch];
  let dragging = false, selecting = false, destroyed = false, previousTime = 0, previousPosition = 0;
  const clamp = (value: number) => Math.max(0, Math.min(options.count - 1, value));
  /** :returns: 将连续视觉状态写入组件，不更新业务状态。 */
  function paint(): void {
    if (!destroyed) options.paint({ position: position.get(), pressed: pressed.get(), stretch: stretch.get() });
  }
  const subscriptions = values.map(value => value.on('change', paint));
  /** :param value: 运动值。:param target: 目标值。:param damping: 阻尼。 */
  function spring(value: typeof position, target: number, damping = 25): void {
    value.stop();
    if (options.reducedMotion()) value.set(target);
    else animate(value, target, { type: 'spring', stiffness: 500, damping, mass: .7, restDelta: .001, restSpeed: .01 });
  }
  /** :returns: 同步到实际路由。 */
  function sync(): void {
    if (destroyed || dragging || selecting) return;
    spring(position, options.activeIndex(), 29);
  }
  /** :param value: 入口连续位置。:param time: 指针时间戳。:returns: 是否开始手势。 */
  function begin(value: number, time: number): boolean {
    if (destroyed || selecting || dragging) return false;
    dragging = true; previousTime = time; previousPosition = clamp(value);
    spring(position, previousPosition, 29); spring(pressed, 1); paint();
    return true;
  }
  /** :param value: 入口连续位置。:param time: 指针时间戳。 */
  function move(value: number, time: number): void {
    if (!dragging || destroyed) return;
    const next = clamp(value), elapsed = Math.max(1, time - previousTime);
    const speed = Math.abs(next - previousPosition) * 1000 / elapsed;
    previousTime = time; previousPosition = next;
    position.stop(); position.set(next); spring(stretch, Math.min(1, speed / 16), 30);
  }
  /** :returns: 终止手势并同步到实际路由。 */
  function cancel(): void {
    dragging = false;
    if (destroyed) return;
    spring(pressed, 0); spring(stretch, 0); sync();
  }
  /** :param index: 完成操作选中的入口。:returns: 实际导航完成。 */
  async function activate(index: number): Promise<void> {
    if (destroyed || selecting) return;
    if (!Number.isInteger(index) || index < 0 || index >= options.count) throw new RangeError('底栏入口不存在。');
    spring(position, index, 29);
    if (index === options.activeIndex() && index !== options.count - 1) return;
    selecting = true;
    try { await options.select(index); }
    finally { selecting = false; sync(); }
  }
  /** :returns: 一次手势最多提交一次选择。 */
  async function release(): Promise<void> {
    if (!dragging || destroyed) return;
    dragging = false; spring(pressed, 0); spring(stretch, 0);
    await activate(Math.round(previousPosition));
  }
  /** :returns: 停止全部动画与订阅。 */
  function destroy(): void {
    destroyed = true; dragging = false;
    subscriptions.forEach(unsubscribe => unsubscribe()); values.forEach(value => value.destroy());
  }
  paint();
  return { begin, move, release, activate, cancel, sync, paint, destroy };
}
