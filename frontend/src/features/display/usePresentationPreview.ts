/**
 * 两处文稿预览共用的实际源/布局资源加载器，不从期望源或未知布局猜默认图。
 * 原排版与适配请求、在途响应和有限轮询均随同一个预览上下文失效。
 * @Project : SCP-cv
 * @File : usePresentationPreview.ts
 * @Author : Qintsg
 * @Date : 2026-10-03
 */
import { computed, onBeforeUnmount, ref, watch } from 'vue';
import { api, type PptResourceItem, type SessionSnapshot } from '@/services/api';

interface PresentationPreviewContext {
  sourceId: number;
  fitText: boolean;
  key: string;
}

/** :param adapter: 实际适配器标识。:returns: 是否为当前支持的文稿实际资源。 */
export function isPresentationPreviewAdapter(adapter: string | undefined): boolean {
  return ['libreoffice', 'slide_images', 'pdf'].includes(adapter ?? '');
}

/**
 * 只由已确认的实际观测选择预览，旧缺字段与null都保持未知。
 * :param session: 当前HTTP/SSE会话。
 * :returns: 可明确取图的上下文；否则不发送默认请求。
 */
function previewContext(session: SessionSnapshot | undefined): PresentationPreviewContext | null {
  const sourceId = session?.actual_source_id;
  if (!session || typeof sourceId !== 'number' || !Number.isSafeInteger(sourceId) || sourceId <= 0
    || typeof session.actual_fit_text !== 'boolean' || !isPresentationPreviewAdapter(session.actual_adapter_kind)) return null;
  // 心跳时间不是文稿身份；同源重开、输出窗和实际适配器必须各自失效旧请求。
  const key = JSON.stringify([session.window_id, session.actual_adapter_kind, sourceId,
    session.actual_fit_text, session.observed_generation ?? null]);
  return { sourceId, fitText: session.actual_fit_text, key };
}

/**
 * 保留初始加载与专注页有限轮询语义，统一按实际文稿/布局采用响应。
 * :param getSession: 公开props/store提供的会话读取器。
 * :param poll: 专注页允许补齐预览，监看条只执行初始读取。
 * :returns: 只读上下文及资源、错误、初始加载状态。
 */
export function usePresentationPreview(getSession: () => SessionSnapshot | undefined, poll = false) {
  const context = computed(() => previewContext(getSession()));
  const resources = ref<PptResourceItem[]>([]);
  const error = ref('');
  const loading = ref(false);
  const unavailable = computed(() => Boolean(context.value) && !loading.value && !error.value
    && resources.value.every((item) => !item.slide_image));
  let sequence = 0;
  let active = true;
  let pollTimer: number | null = null;
  let pollAttempts = 0;
  const inFlight = ref(false);

  /** :returns: 清除本模块自有轮询，不取消或伪称其它外部操作完成。 */
  function clearPoll(): void {
    if (pollTimer !== null) window.clearTimeout(pollTimer);
    pollTimer = null;
  }

  /** :returns: 当前实际页及已知总页数是否都有预览资源。 */
  function complete(): boolean {
    if (!resources.value.length) return false;
    const session = getSession();
    const current = session?.current_slide || 1;
    const total = session?.total_slides ?? 0;
    return resources.value.some((item) => item.page_index === current && Boolean(item.slide_image))
      && (total <= 0 || resources.value.length >= total)
      && resources.value.every((item) => Boolean(item.slide_image));
  }

  /** :param requested: 本次不可变源/布局。:param requestSequence: 本次请求代次。:returns: 是否仍可采用该响应。 */
  function current(requested: PresentationPreviewContext, requestSequence: number): boolean {
    return active && requestSequence === sequence && requested.key === context.value?.key;
  }

  /** :returns: 为当前上下文安排至多八次补齐读取，不并发轮询。 */
  function schedulePoll(): void {
    if (!poll || !active || !context.value || inFlight.value || pollTimer !== null || complete() || pollAttempts >= 8) return;
    pollTimer = window.setTimeout(() => {
      pollTimer = null;
      const requested = context.value;
      if (!requested || !active) return;
      pollAttempts++;
      void read(requested);
    }, 1000);
  }

  /**
   * 外部HTTP返回后再核上下文；旧成功、错误与加载结束均不能覆新预览。
   * :param requested: 开始读取时的实际文稿/布局。
   * :returns: 本次读取完成或被新的上下文丢弃。
   */
  async function read(requested: PresentationPreviewContext): Promise<void> {
    const requestSequence = ++sequence;
    inFlight.value = true;
    try {
      const payload = await api.listPptResources(requested.sourceId, requested.fitText);
      if (!current(requested, requestSequence)) return;
      resources.value = payload.resources;
      error.value = '';
    } catch (failure) {
      if (!current(requested, requestSequence)) return;
      error.value = failure instanceof Error ? failure.message : '实际文稿预览读取失败。';
    } finally {
      if (current(requested, requestSequence)) {
        loading.value = false;
        inFlight.value = false;
        schedulePoll();
      }
    }
  }

  watch(() => context.value?.key ?? '', () => {
    sequence++;
    clearPoll();
    pollAttempts = 0;
    inFlight.value = false;
    resources.value = [];
    error.value = '';
    const requested = context.value;
    loading.value = Boolean(requested);
    if (requested) void read(requested);
  }, { immediate: true });

  watch(() => [getSession()?.current_slide, getSession()?.total_slides] as const, schedulePoll);

  /**
   * 用户显式刷新只重读当前静态预览，不重开文稿，也不重置自动补齐预算。
   * :returns: 本次GET完成；未知上下文、卸载或已有在途请求时不重复下发。
   */
  async function refresh(): Promise<void> {
    const requested = context.value;
    if (!active || !requested || inFlight.value) return;
    clearPoll();
    resources.value = [];
    error.value = '';
    loading.value = true;
    await read(requested);
  }

  onBeforeUnmount(() => {
    active = false;
    sequence++;
    clearPoll();
  });
  return { context, resources, error, loading, unavailable, pending: inFlight, refresh };
}
