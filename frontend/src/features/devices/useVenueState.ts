/** 现场设备页状态：当前范围自行读取；旧请求不串反馈/续读，真实声音占用跨页面与认证保留。 */
import { computed, onBeforeUnmount, onMounted, ref } from 'vue';
import { useAuthStore } from '@/stores/auth';
import { useSourceStore } from '@/stores/sources';
import { clientConnection } from '@/platform/connection';
import { useRequestContext } from '@/composables/useRequestContext';
import { isWritePending } from '@/services/writeFlights';
import { RequestScopeChangedError } from '@/services/authenticationScope';
import { microphoneVolume, venueApi, type VenueState, type VenueAudioSettings, type VenueAudioResult } from '@/services/venueApi';

/** :param resetSelection: 清理该页旧打开卡片。:returns: 当前声音草稿/设备投影与单次显式动作，不改物理确认语义。 */
export function useVenueState(resetSelection: () => void) {
  const auth = useAuthStore(), sources = useSourceStore();
  const state = ref<VenueState | null>(null), result = ref<VenueAudioResult | null>(null), error = ref('');
  const volume = ref<number | null>(null), computer = ref<number | null>(null), handheld = ref<number | null>(null), headset = ref<number | null>(null);
  const audioSource = ref<VenueAudioSettings['source']>(null), clockOffset = ref(0), localBusy = ref(false);
  let alive = true, operation = 0, reload = 0;
  const busy = computed(() => localBusy.value || isWritePending('venue-audio'));
  const captureContext = useRequestContext(() => {
    operation++; const ticket = ++reload; localBusy.value = false;
    state.value = null; result.value = null; error.value = ''; clockOffset.value = 0;
    volume.value = null; computer.value = null; handheld.value = null; headset.value = null; audioSource.value = null;
    resetSelection();
    // 恢复属于新范围，不从旧finally发起；再次失效或卸载则整项丢弃。
    queueMicrotask(() => { if (alive && ticket === reload && auth.isAuthenticated && !clientConnection.isSwitching) void run('load'); });
  });
  /** :param action: 读取/实际读回/显式应用。:param settings: 点击时冻结的增量。:returns: 当前意图完成，旧finally不解锁新操作或自动重放。 */
  async function run(action: 'load' | 'read' | 'apply', settings?: VenueAudioSettings): Promise<void> {
    if (!alive || localBusy.value || !auth.isAuthenticated || clientConnection.isSwitching
      || (action !== 'load' && isWritePending('venue-audio')) || (action === 'apply' && !settings)) return;
    const scope = captureContext(), id = ++operation, fields = settings ? { ...settings } : undefined;
    const current = () => scope() && id === operation;
    localBusy.value = true; error.value = '';
    try {
      if (action !== 'load') {
        const response = action === 'read' ? await venueApi.readAudio() : await venueApi.applyAudio(fields!);
        if (!current()) return;
        result.value = response.audio;
      }
      const response = await venueApi.get(current);
      if (!current()) return;
      state.value = response.venue;
      // 同一范围只回填本次字段，其它尚未应用草稿保留；换范围已在reset清空。
      if (action === 'load' || action === 'read' || fields?.screen_volume != null) volume.value = response.venue.audio.screen_volume ?? null;
      if (action === 'load' || fields?.computer_volume != null) computer.value = response.venue.audio.computer_volume ?? null;
      if (action === 'load' || fields?.handheld_volume != null) handheld.value = response.venue.audio.handheld_volume ?? null;
      if (action === 'load' || fields?.headset_volume != null) headset.value = microphoneVolume(response.venue.audio);
      if (action === 'load' || fields?.source != null) audioSource.value = response.venue.audio.source ?? null;
      clockOffset.value = response.venue.server_time_ms == null ? 0 : response.venue.server_time_ms - Date.now();
      if (action === 'load' && current()) await sources.refresh();
    } catch (failure) { if (current() && !(failure instanceof RequestScopeChangedError)) error.value = failure instanceof Error ? failure.message : '读取失败'; }
    finally { if (current()) localBusy.value = false; }
  }
  onMounted(() => { void run('load'); });
  onBeforeUnmount(() => { alive = false; reload++; });
  return { state, result, error, volume, computer, handheld, headset, audioSource, clockOffset, busy, run };
}
