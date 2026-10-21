/** 显示逻辑记账合同：表示最近一次完整发送，不代表实体屏幕读回。 */
import { exclusiveWrite, readAfterWrite, requestJson } from './api';
import type { VideoWallMappingItem } from './apiTypes';
export type TelevisionTarget = 'tv_left' | 'tv_right';
export interface DisplayRoutingOutput {
  target: 'wall' | TelevisionTarget;
  status: 'unknown' | 'dispatching' | 'sent_unconfirmed' | 'simulated' | 'uncertain';
  last_sent: { input?: VideoWallMappingItem['input'] | null; layout_code?: string | null } | null;
  updated_at?: string | null;
}
export const displayRoutingApi = {
  get: (current: () => boolean) => readAfterWrite('display-routing', () => requestJson<{success: boolean; outputs: Record<'wall' | TelevisionTarget, DisplayRoutingOutput>}>('/api/display-routing/', {}, undefined, {canConsume: current}), current),
  apply: (target: TelevisionTarget, input: VideoWallMappingItem['input'], current: () => boolean) => exclusiveWrite('display-routing', () => requestJson<{success: boolean; output: DisplayRoutingOutput; result: string}>(`/api/display-routing/televisions/${target}/`, {method:'PUT',body:JSON.stringify({input,force:true})}, undefined, {canConsume: current})),
};
