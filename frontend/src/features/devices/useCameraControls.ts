/** 云台交互归属：停止独立于在途start；位置确认冻结目标，认证/主机变更不借用后来会话续租。 */
import { computed, onBeforeUnmount, onMounted, ref, shallowRef, watch } from 'vue';
import { useAuthStore } from '@/stores/auth';
import { useRuntimeStore } from '@/stores/runtime';
import { clientConnection } from '@/platform/connection';
import { captureAuthenticationScope } from '@/services/authenticationScope';
import { isWritePending } from '@/services/writeFlights';
import { venueApi, type CameraDirection, type VenueCamera } from '@/services/venueApi';
import { useRequestContext } from '@/composables/useRequestContext';
import { CameraMotionLease } from './motionLease';

interface PositionIntent { camera: string; name: string; position: number; current(): boolean }

/** :param props: 当前公开摄像机属性。:returns: 本块控制、只读忙态与冻结位置确认，不改设备协议。 */
export function useCameraControls(props: Readonly<{ camera: VenueCamera; clockOffset?: number }>) {
  const runtime = useRuntimeStore(), auth = useAuthStore();
  const active = ref<CameraDirection | null>(null), error = ref(''), feedback = ref('');
  const position = ref<number | null>(1), busy = ref(false), revision = ref(0);
  const confirmation = shallowRef<PositionIntent | null>(null);
  let lease: CameraMotionLease | null = null, operation = 0;
  /** :returns: 清本地意图；stop能否发出由原传输归属判断，不能使用新会话。 */
  function reset(): void {
    revision.value++; operation++; confirmation.value = null; busy.value = false;
    stop(); feedback.value = ''; error.value = '';
  }
  const captureContext = useRequestContext(reset);
  const baseDisabled = computed(() => {
    void revision.value;
    return !auth.isAuthenticated || clientConnection.isSwitching || !props.camera.control_available
      || busy.value || isWritePending(`camera-position:${props.camera.id}`);
  });
  const disabled = computed(() => baseDisabled.value || confirmation.value !== null);
  const validPosition = computed(() => position.value !== null && Number.isInteger(position.value)
    && position.value >= 1 && position.value <= (props.camera.position_count ?? 6));
  const saveDisabled = computed(() => baseDisabled.value || !validPosition.value);
  /** :returns: 松手立即释放，不等待start响应，也不释放别人的新运动。 */
  function stop(): void { const owned = lease; lease = null; owned?.stop(); active.value = null; }
  /** :param direction: 明确方向。:returns: 原主机/完整认证范围内的有界租约。 */
  function start(direction: CameraDirection): void {
    if (disabled.value || clientConnection.isSwitching) return;
    stop(); error.value = ''; feedback.value = '';
    const current = captureContext(), camera = props.camera.id;
    const generation = clientConnection.captureGeneration(), projection = runtime.connectionGeneration;
    const authentication = captureAuthenticationScope();
    // 卸载/换摄像机仍可停止原目标；换主机/认证则只依赖原服务器900ms看门狗，不转发旧stop。
    const transportCurrent = () => generation === clientConnection.captureGeneration()
      && projection === runtime.connectionGeneration && authentication() && !clientConnection.isSwitching;
    const owned = () => current() && camera === props.camera.id;
    lease = new CameraMotionLease(request => {
      if (!transportCurrent() || (request.phase !== 'stop' && !owned())) return Promise.resolve();
      return venueApi.motion(camera, request);
    }, failure => { if (owned()) error.value = failure instanceof Error ? failure.message : '运动发送失败，请检查连接。'; },
    direction => { if (owned()) active.value = direction; }, () => Date.now() + (props.clockOffset ?? 0));
    lease.start(direction);
  }
  /** :param event: 原生按下。:param direction: 方向。:returns: 仅主键获取指针捕获。 */
  function pointerStart(event: PointerEvent, direction: CameraDirection): void {
    if (event.button !== 0 || disabled.value) return;
    (event.currentTarget as HTMLElement).setPointerCapture(event.pointerId); start(direction);
  }
  /** :param event: 原生键盘。:param direction: 方向。:returns: 重复keydown不重放start。 */
  function keyStart(event: KeyboardEvent, direction: CameraDirection): void {
    if (![' ', 'Enter'].includes(event.key)) return; event.preventDefault();
    if (!event.repeat) start(direction);
  }
  /** :param event: 原生松键。:returns: 独立停止。 */
  function keyStop(event: KeyboardEvent): void { if ([' ', 'Enter'].includes(event.key)) { event.preventDefault(); stop(); } }
  /** :returns: 页面不可见即释放原有效租约。 */
  function hidden(): void { if (document.visibilityState !== 'visible') stop(); }
  /** :returns: 点击时冻结的位置和完整归属，不读取确认时可变目标。 */
  function positionIntent(): PositionIntent {
    const current = captureContext(), camera = props.camera.id;
    return { camera, name: props.camera.name, position: position.value!, current: () => current() && camera === props.camera.id };
  }
  /** :param open: 原生确认开关。:returns: 先停运动再冻结确认，关闭不发送保存。 */
  function openConfirmation(open: boolean): void {
    if (!open) { confirmation.value = null; return; }
    if (saveDisabled.value || disabled.value) return;
    stop(); error.value = ''; feedback.value = ''; confirmation.value = positionIntent();
  }
  /** :param action: 一次调用/保存。:param intent: 点击或确认冻结的意图。:returns: 原请求终端才释放实际占用，旧finally不解新意图。 */
  async function applyPosition(action: 'call' | 'save', intent?: PositionIntent): Promise<void> {
    if (baseDisabled.value || clientConnection.isSwitching || (!intent && (!validPosition.value || confirmation.value))) return;
    const target = intent ?? positionIntent();
    if (!target.current()) return;
    stop(); const id = ++operation; busy.value = true; error.value = ''; feedback.value = '';
    try { const result = await venueApi.position(target.camera, target.position, action); if (target.current() && id === operation) feedback.value = result.camera.detail; }
    catch (failure) { if (target.current() && id === operation) error.value = failure instanceof Error ? failure.message : '位置操作未确认'; }
    finally { if (target.current() && id === operation) busy.value = false; }
  }
  /** :returns: 只提交仍有效的原确认；同步双击不能生成第二次保存。 */
  function saveConfirmed(): void {
    const target = confirmation.value; confirmation.value = null;
    if (target) void applyPosition('save', target);
  }
  watch(() => props.camera.id, reset, { flush: 'sync' });
  watch(() => props.camera.control_available, available => { if (!available) reset(); }, { flush: 'sync' });
  onMounted(() => { window.addEventListener('blur', stop); document.addEventListener('visibilitychange', hidden); });
  onBeforeUnmount(() => { stop(); window.removeEventListener('blur', stop); document.removeEventListener('visibilitychange', hidden); });
  return { active, error, feedback, position, busy, disabled, saveDisabled, confirmation, stop, pointerStart, keyStart, keyStop, applyPosition, openConfirmation, saveConfirmed };
}
