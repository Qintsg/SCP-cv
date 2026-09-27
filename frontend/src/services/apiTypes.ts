/** 共享控制端的媒体、运行态与设备 REST 数据类型。 */
export interface MediaFolderItem {
  id: number;
  name: string;
  parent_id: number | null;
  relative_path?: string;
  created_at: string;
  updated_at: string;
}

export interface MediaSourceItem {
  id: number;
  source_type: string;
  name: string;
  uri: string;
  is_available: boolean;
  stream_identifier: string;
  folder_id: number | null;
  original_filename: string;
  file_size: number;
  mime_type: string;
  is_temporary: boolean;
  expires_at: string | null;
  metadata: Record<string, unknown>;
  /**
   * 是否在播放器启动时预热媒体源。
   * keep_alive 是旧字段兼容别名，后续 UI 只展示 preheat_enabled。
   */
  preheat_enabled: boolean;
  keep_alive: boolean;
  /** 演示文稿播放模式：pdf / powerpoint；其它类型为空字符串。 */
  playback_mode: string;
  preparation_state?: string;
  page_count?: number;
  preview_url: string;
  thumbnail_url: string;
  preview_kind: 'icon' | 'image' | 'video';
  preview_label: string;
  created_at: string;
}

/** PATCH /api/sources/{id}/ 可编辑字段子集。 */
export interface MediaSourceUpdate {
  name?: string;
  uri?: string;
  preheat_enabled?: boolean;
  keep_alive?: boolean;
}

export interface SessionSnapshot {
  window_id: number;
  session_id: number;
  source_id: number | null;
  source_name: string;
  source_type: string;
  source_type_label: string;
  source_uri: string;
  /** 演示文稿播放模式：pdf / powerpoint；其它类型为空字符串。 */
  playback_mode: string;
  playback_state: string;
  playback_state_label: string;
  error_message: string;
  display_mode: string;
  display_mode_label: string;
  target_display_label: string;
  current_slide: number;
  total_slides: number;
  position_ms: number;
  duration_ms: number;
  pending_command: string;
  player_online: boolean;
  player_last_seen_at: string;
  last_updated_at: string;
  volume: number;
  is_muted: boolean;
  loop_enabled: boolean;
}

export interface BackgroundAudioStateSnapshot {
  id: number;
  source_id: number | null;
  source_name: string;
  source_uri: string;
  source: MediaSourceItem | null;
  current_item_id: number | null;
  playback_state: string;
  playback_state_label: string;
  error_message: string;
  position_ms: number;
  duration_ms: number;
  volume: number;
  is_muted: boolean;
  loop_enabled: boolean;
  pending_command: string;
  updated_at: string;
}

export interface BackgroundAudioPlaylistItem {
  id: number;
  source_id: number;
  source_name: string;
  sort_order: number;
  created_at: string;
  source: MediaSourceItem;
}

export interface BackgroundAudioSnapshot {
  state: BackgroundAudioStateSnapshot;
  playlist: BackgroundAudioPlaylistItem[];
}

export interface BackgroundAudioPayload {
  success: boolean;
  background_audio: BackgroundAudioSnapshot;
}

export interface RuntimeSnapshot {
  big_screen_mode: 'single' | 'double';
  volume_level: number;
  muted_windows: number[];
}

export interface ScenarioTargetItem {
  window_id: number;
  source_state: 'unset' | 'empty' | 'set';
  source_id: number | null;
  source_name: string;
  autoplay: boolean;
  resume: boolean;
}

export interface ScenarioItem {
  id: number;
  name: string;
  description: string;
  sort_order: number;
  big_screen_mode_state: 'unset' | 'empty' | 'set';
  big_screen_mode: 'single' | 'double';
  big_screen_mode_label: string;
  volume_state: 'unset' | 'empty' | 'set';
  volume_level: number;
  targets: ScenarioTargetItem[];
  created_at: string;
  updated_at: string;
}

export interface DisplayTargetItem {
  index: number;
  name: string;
  width: number;
  height: number;
  x: number;
  y: number;
  is_primary: boolean;
  playback_role?: 'big_left' | 'big_right' | '';
  is_playback_target?: boolean;
}

export type PlaybackWindowId = 1 | 2;

export type VideoWallInputKind = 'window_1' | 'window_2' | 'laptop' | 'ip_stream';
export type VideoWallRegion = 'fullscreen' | 'left' | 'right';

export interface VideoWallMappingItem {
  region: VideoWallRegion;
  input: { kind: VideoWallInputKind; ip_address?: string };
}

export interface VideoWallLayoutItem {
  name: string;
  preset: 'window_1_fullscreen' | 'window_1_left_window_2_right' | '';
  mappings: VideoWallMappingItem[];
  can_apply: boolean;
  unavailable_reason: string;
}

export interface VideoWallLayoutState {
  draft: VideoWallLayoutItem | null;
  draft_revision: number;
  active_preset: 'single' | 'double';
}

export interface DeviceItem {
  name: string;
  device_type: 'splice_screen' | 'tv_left' | 'tv_right';
  device_type_label?: string;
  host?: string;
  port?: number;
  action?: string;
  detail?: string;
}

export interface PptMediaItem {
  id: string;
  media_index: number;
  media_type: string;
  name: string;
  target: string;
  shape_id: number;
}

export interface PptResourceItem {
  id: number;
  source_id: number;
  page_index: number;
  slide_image: string;
  next_slide_image: string;
  speaker_notes: string;
  has_media: boolean;
  media_items: PptMediaItem[];
  created_at: string;
}

export interface ApiStatePayload {
  success: boolean;
  sessions: SessionSnapshot[];
  background_audio?: BackgroundAudioSnapshot;
}

export interface PhysicalSmokeRequest {
  windows?: number[];
  source_ids?: Record<string, number>;
  settle_seconds?: number;
  timeout_seconds?: number;
  ppt_timeout_seconds?: number;
  stream_timeout_seconds?: number;
  total_timeout_seconds?: number;
  reset_after?: boolean;
}

export interface PhysicalSmokeStepResult {
  window_id: number;
  source_type: string;
  source_id: number;
  source_name: string;
  status: 'ok' | 'failed';
  open_elapsed: number;
  close_elapsed: number;
  error_message: string;
  open_error: string;
  close_error: string;
}

export interface PhysicalSmokeResult {
  success: boolean;
  started_at: string;
  finished_at: string;
  elapsed_seconds: number;
  total_timeout_seconds: number;
  windows: number[];
  source_ids: Record<string, number>;
  summary: { total: number; passed: number; failed: number };
  results: PhysicalSmokeStepResult[];
  reset: { status: 'ok' | 'failed' | 'skipped'; elapsed: number; error_message: string };
  sessions: SessionSnapshot[];
}

export interface UploadOptions {
  onProgress?: (percent: number) => void;
}

export interface ScenarioPayload {
  name: string;
  description?: string;
  big_screen_mode_state?: 'unset' | 'empty' | 'set';
  big_screen_mode?: 'single' | 'double';
  volume_state?: 'unset' | 'empty' | 'set';
  volume_level?: number;
  targets?: Array<{
    window_id: number;
    source_state: 'unset' | 'empty' | 'set';
    source_id?: number | null;
    autoplay?: boolean;
    resume?: boolean;
  }>;
}
