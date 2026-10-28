// 文件描述：在真实浏览器动画中验证生产镜片控制器与文件提交。
// 项目：SCP-cv；文件：glass-motion-page.ts；作者：Qintsg；日期：2026-10-28。
import { createNavigationMotion, type NavigationFrame } from '../src/layouts/glass/navigationMotion';
import 'simple-liquid-glass/web-component';

/** :param condition: 实际检查结果。:param message: 失败原因。 */
function check(condition: boolean, message: string): asserts condition { if (!condition) throw new Error(message); }
/** :param milliseconds: 真实浏览器等待。:returns: 等待完成。 */
function wait(milliseconds: number): Promise<void> { return new Promise(resolve => setTimeout(resolve, milliseconds)); }
/** :returns: 真实动画、提交与资源释放结果。 */
async function run() {
  let active = 0, reduced = false, paints = 0;
  let frame: NavigationFrame = { position: 0, pressed: 0, stretch: 0 };
  const records: number[] = [], lens = document.querySelector<HTMLElement>('liquid-glass')!;
  const motion = createNavigationMotion({ count: 5, activeIndex: () => active, reducedMotion: () => reduced,
    paint: value => { frame = value; paints++; lens.style.transform = `translateX(${value.position * 90}px) scale(${1 + value.pressed * .08 + value.stretch * .16},${1 + value.pressed * .14 - value.stretch * .07})`; },
    select: async index => {
      const response = await fetch('/record', { method: 'POST', body: JSON.stringify(index) });
      check(response.ok, '真实文件提交失败'); records.push(index); active = index;
    } });
  try {
    check(motion.begin(0, performance.now()), '开始手势失败');
    for (let index = 0; index <= 24; index++) {
      motion.move(index / 10, performance.now());
      await new Promise(requestAnimationFrame);
    }
    check(frame.position === 2.4 && frame.pressed > .9 && frame.stretch > 0, '连续跟随或按压形变失败');
    check(records.length === 0, '拖动期间提前导航');
    await motion.release(); await wait(1600);
    check(records.join() === '2' && Math.abs(frame.position - 2) < .001, '松手吸附与单次提交失败');
    motion.begin(2, performance.now()); await motion.release();
    check(records.join() === '2', '同页选择重复导航');
    motion.begin(4, performance.now()); motion.move(1.7, performance.now()); motion.cancel(); await wait(1600);
    check(records.join() === '2' && Math.abs(frame.position - 2) < .001, '取消后页面或镜片错误');
    motion.begin(4, performance.now()); await motion.release();
    check(records.join() === '2,4', '快速按压松手未选择目标');
    await motion.activate(4); check(records.join() === '2,4,4', '更多入口无法再次打开');
    active = 1; motion.sync(); await wait(1600);
    check(Math.abs(frame.position - 1) < .001, '实际路由同步失败');
    const idlePaints = paints; await wait(700); check(paints === idlePaints, '空闲后继续更新镜片');
    reduced = true; motion.begin(3, performance.now()); motion.move(3.6, performance.now()); await motion.release();
    check(records.join() === '2,4,4,4' && frame.position === 4 && frame.pressed === 0, '减弱动态模式选择错误');
    motion.begin(2, performance.now()); motion.destroy(); const lastPaints = paints;
    await wait(300); await motion.activate(0);
    check(paints === lastPaints && records.length === 4, '卸载后继续绘制或导航');
    check(lens.dataset.glassStrategy === 'svg', '真实Chromium未启用SVG折射');
    check(lens.shadowRoot!.querySelector('feDisplacementMap') !== null, '玻璃折射滤镜缺失');
    return { records, paints, idlePaints, strategy: lens.dataset.glassStrategy, tests: 11 };
  } finally { motion.destroy(); }
}
Object.assign(window, { glassVerification: run() });
