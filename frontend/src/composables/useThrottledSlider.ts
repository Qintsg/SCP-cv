/*
 * 滑块拖动 ↔ 服务端同步的统一节流策略，专门用于「系统音量 / 窗口音量」一类
 * 高频改动且服务端写入有 RTT 的场景，解决以下问题：
 *   1. 拖动时连续 input 事件 → 多发请求 → 服务端响应回写覆盖正在拖动的位置 → 滑块回弹；
 *   2. 网络毛刺时旧请求晚于新请求落地 → 滑块跳到旧值；
 *   3. 拖动结束后保证最终值一定提交一次（避免节流间隔吞掉最后一次值）。
 *
 * 关键策略：
 *   - 拖动期间维护本地 `value`，服务端推送在拖动 / 飞行 / 待发期间一律不覆盖；
 *   - 同一时刻只允许一发请求在飞，落地后若有 pending 立即续发；
 *   - 拖动结束（input.change）触发一次确定性 flush，确保最终值与 UI 一致。
 * @Project : SCP-cv
 * @File : useThrottledSlider.ts
 * @Author : Qintsg
 * @Date : 2026-09-29
 */
import { onBeforeUnmount, ref, watch, type Ref } from 'vue';

/** 调用方需要提供的提交回调与节流参数。 */
export interface ThrottledSliderOptions {
  /** 节流间隔，毫秒；默认 120 ms。 */
  throttleMs?: number;
  /** 提交最终值给服务端；返回 Promise 时本 composable 会等其结束再放行下一发。 */
  commit: (value: number) => Promise<unknown> | unknown;
  /** commit 抛错时的副作用，用于业务侧弹 Toast。 */
  onError?: (error: unknown) => void;
  /** 可操作边界；从可操作变为不可操作时丢弃本地未发送缓冲。 */
  isActive?: () => boolean;
  /** 稳定的目标标识；源/窗口改变时旧缓冲不得转投新目标。 */
  scope?: () => unknown;
}

/** 暴露给视图绑定的接口：value 用于显示，handleInput/handleChange 用于事件。 */
export interface ThrottledSliderHandle {
  /** 直接绑定到 n-slider 的 model-value；拖动期间立即更新，服务端响应不覆盖。 */
  value: Ref<number>;
  /** 绑定到 n-slider 的 update:modelValue：拖动中触发，节流上报。 */
  handleInput: (next: number) => void;
  /** 绑定到 n-slider 的 change：抬手或键盘 commit 时触发，立即上报最终值。 */
  handleChange: (next: number) => void;
}

/**
 * 创建一个滑块节流绑定。
 * :param getRemoteValue: 取当前服务端值（用于实时反向同步）。
 * :param options: 外部提交、节流以及可选操作域配置。
 * :returns: 公开滑块句柄；仅取消未发送缓冲，不承诺撤销已受理 HTTP。
 */
export function useThrottledSlider(
  getRemoteValue: () => number,
  options: ThrottledSliderOptions,
): ThrottledSliderHandle {
  const throttleMs = options.throttleMs ?? 120;
  const value = ref<number>(getRemoteValue());

  // 拖动锁：用户按住滑块期间，禁止任何外部 watch 同步覆盖本地值。
  let dragging = false;
  // 待提交值：节流窗口内的最新一次本地值；同一时刻最多一发请求在飞。
  let pendingValue: number | null = null;
  // 是否有请求未返回。
  let inFlight = false;
  // 上一次发请求的时间戳，用于强约束「最小间隔」。
  let lastSentAt = 0;
  // 节流定时器引用。
  let throttleTimer: number | null = null;
  let scopeVersion = 0;
  let disposed = false;

  /**
   * 检查当前组件和外部操作边界是否仍允许新提交。
   * :returns: 当前可操作时为真。
   */
  function canCommit(): boolean {
    return !disposed && (options.isActive?.() ?? true);
  }

  /**
   * 丢弃未发送缓冲，并隔离旧响应与当前作用域的状态推进。
   * :returns: 本地缓冲已清理；在途 HTTP 仍由原调用方正常完成。
   */
  function discardPending(): void {
    scopeVersion += 1;
    pendingValue = null;
    dragging = false;
    inFlight = false;
    lastSentAt = 0;
    clearThrottleTimer();
    value.value = getRemoteValue();
  }

  watch(
    [() => options.scope?.(), () => options.isActive?.() ?? true],
    ([scope, active], [previousScope]) => {
      if (!active || scope !== previousScope) discardPending();
    },
    { flush: 'sync' },
  );

  // 服务端值变化：仅在「不在拖动 + 没有飞行中请求 + 没有待发」时同步到本地。
  watch(getRemoteValue, (next: number): void => {
    if (dragging || inFlight || pendingValue !== null) return;
    if (value.value !== next) value.value = next;
  });

  function clearThrottleTimer(): void {
    if (throttleTimer !== null) {
      window.clearTimeout(throttleTimer);
      throttleTimer = null;
    }
  }

  /**
   * 把 pendingValue 提交给 commit；严格保证两次 commit 间隔 ≥ throttleMs：
   *   - 距上一次发送不足 throttleMs：排定定时器，到期后再 flush；
   *   - 已超过 throttleMs：立即起飞；
   *   - 回程时若 pending 仍有新值，再走一遍 flush（自然保留最小间隔）。
   * :returns: 当前作用域的一次提交完成或尚未满足提交条件。
   */
  async function flush(): Promise<void> {
    if (!canCommit() || pendingValue === null || inFlight) return;
    const elapsed = Date.now() - lastSentAt;
    if (elapsed < throttleMs) {
      if (throttleTimer === null) {
        throttleTimer = window.setTimeout((): void => {
          throttleTimer = null;
          void flush();
        }, throttleMs - elapsed);
      }
      return;
    }
    const next = pendingValue;
    pendingValue = null;
    clearThrottleTimer();
    inFlight = true;
    const requestScopeVersion = scopeVersion;
    lastSentAt = Date.now();
    try {
      await Promise.resolve(options.commit(next));
    } catch (error) {
      if (requestScopeVersion === scopeVersion && canCommit()) options.onError?.(error);
    } finally {
      if (requestScopeVersion !== scopeVersion || !canCommit()) return;
      inFlight = false;
      if (pendingValue !== null) {
        // 回程时仍有新值；若节流间隔尚未走完，flush 内部会自动改用 timer。
        void flush();
      }
    }
  }

  /**
   * 当前操作域拖动期间立即更新本地值，提交走节流。
   * :param next: 最新用户输入。
   * :returns: 输入已缓存或因不可操作被拒绝。
   */
  function schedule(next: number): void {
    if (!canCommit()) return;
    pendingValue = next;
    value.value = next;
    void flush();
  }

  /**
   * 接收当前操作域的拖动输入。
   * :param next: 期望值。
   * :returns: 当前合法输入已处理。
   */
  function handleInput(next: number): void {
    if (!canCommit()) return;
    dragging = true;
    schedule(next);
  }

  /**
   * 抬手 / 键盘 commit：本地值立即更新；为确保最终值最快上报，
   * 把 lastSentAt 重置到 0，跳过节流间隔约束。
   * 若有飞行中请求，会在其回程时由 finally 路径自动续发到最终值。
   * :param next: 用户最终确认值。
   * :returns: 合法输入已提交或在当前域等待。
   */
  function handleChange(next: number): void {
    if (!canCommit()) return;
    dragging = false;
    pendingValue = next;
    value.value = next;
    clearThrottleTimer();
    lastSentAt = 0;
    if (inFlight) return;
    void flush();
  }

  onBeforeUnmount((): void => {
    disposed = true;
    discardPending();
  });

  return { value, handleInput, handleChange };
}
