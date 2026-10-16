/*
 * Fluent UI System Icons 映射表（DESIGN.md §9 与生态包的关系）。
 *
 * 设计取舍：
 * 1. 业务侧延续既有名 token（`home_24_regular` 等）调用 `<FIcon name="…" />`，
 *    本表把它翻译为 @vicons/fluent 暴露的 Vue 组件（PascalCase 化的官方
 *    Fluent UI System Icons 名）。
 * 2. 渲染由 FIcon.vue 通过原生span与SVG组件完成；本文件
 *    只负责映射，不直接渲染。
 * 3. `FluentIconName` 联合类型从 ICON_MAP 的键派生，保持全仓库类型契约不变。
 */
import type { Component } from 'vue';
import Camera24Regular from '@vicons/fluent/es/Camera24Regular';
import Add24Regular from '@vicons/fluent/es/Add24Regular';
import Add20Regular from '@vicons/fluent/es/Add20Regular';
import Alert24Regular from '@vicons/fluent/es/Alert24Regular';
import AlertUrgent24Regular from '@vicons/fluent/es/AlertUrgent24Regular';
import Apps24Regular from '@vicons/fluent/es/Apps24Regular';
import AppsList24Regular from '@vicons/fluent/es/AppsList24Regular';
import ArrowClockwise24Regular from '@vicons/fluent/es/ArrowClockwise24Regular';
import ArrowClockwise20Regular from '@vicons/fluent/es/ArrowClockwise20Regular';
import ArrowDownload24Regular from '@vicons/fluent/es/ArrowDownload24Regular';
import ArrowLeft24Regular from '@vicons/fluent/es/ArrowLeft24Regular';
import ArrowRight24Regular from '@vicons/fluent/es/ArrowRight24Regular';
import ArrowMaximize24Regular from '@vicons/fluent/es/ArrowMaximize24Regular';
import ArrowMinimize24Regular from '@vicons/fluent/es/ArrowMinimize24Regular';
import ArrowRepeatAll24Regular from '@vicons/fluent/es/ArrowRepeatAll24Regular';
import ArrowRepeatAllOff24Regular from '@vicons/fluent/es/ArrowRepeatAllOff24Regular';
import ArrowReset24Regular from '@vicons/fluent/es/ArrowReset24Regular';
import ArrowSwap24Regular from '@vicons/fluent/es/ArrowSwap24Regular';
import ArrowUpload24Regular from '@vicons/fluent/es/ArrowUpload24Regular';
import Checkmark16Filled from '@vicons/fluent/es/Checkmark16Filled';
import Checkmark20Regular from '@vicons/fluent/es/Checkmark20Regular';
import Checkmark24Regular from '@vicons/fluent/es/Checkmark24Regular';
import CheckmarkCircle24Filled from '@vicons/fluent/es/CheckmarkCircle24Filled';
import ChevronDown20Regular from '@vicons/fluent/es/ChevronDown20Regular';
import ChevronUp20Regular from '@vicons/fluent/es/ChevronUp20Regular';
import ChevronLeft20Regular from '@vicons/fluent/es/ChevronLeft20Regular';
import ChevronLeft24Regular from '@vicons/fluent/es/ChevronLeft24Regular';
import ChevronRight20Regular from '@vicons/fluent/es/ChevronRight20Regular';
import ChevronRight24Regular from '@vicons/fluent/es/ChevronRight24Regular';
import Circle24Regular from '@vicons/fluent/es/Circle24Regular';
import DarkTheme24Regular from '@vicons/fluent/es/DarkTheme24Regular';
import Delete24Regular from '@vicons/fluent/es/Delete24Regular';
import Delete20Regular from '@vicons/fluent/es/Delete20Regular';
import Desktop24Regular from '@vicons/fluent/es/Desktop24Regular';
import DesktopMac24Regular from '@vicons/fluent/es/DesktopMac24Regular';
import Dismiss24Regular from '@vicons/fluent/es/Dismiss24Regular';
import Dismiss20Regular from '@vicons/fluent/es/Dismiss20Regular';
import Dismiss16Regular from '@vicons/fluent/es/Dismiss16Regular';
import Document24Regular from '@vicons/fluent/es/Document24Regular';
import Edit24Regular from '@vicons/fluent/es/Edit24Regular';
import Edit20Regular from '@vicons/fluent/es/Edit20Regular';
import ErrorCircle24Filled from '@vicons/fluent/es/ErrorCircle24Filled';
import ErrorCircle20Filled from '@vicons/fluent/es/ErrorCircle20Filled';
import Eye24Regular from '@vicons/fluent/es/Eye24Regular';
import EyeOff24Regular from '@vicons/fluent/es/EyeOff24Regular';
import Filter24Regular from '@vicons/fluent/es/Filter24Regular';
import Folder24Regular from '@vicons/fluent/es/Folder24Regular';
import FullScreenMaximize24Regular from '@vicons/fluent/es/FullScreenMaximize24Regular';
import FullScreenMinimize24Regular from '@vicons/fluent/es/FullScreenMinimize24Regular';
import Globe24Regular from '@vicons/fluent/es/Globe24Regular';
import Home24Regular from '@vicons/fluent/es/Home24Regular';
import Home24Filled from '@vicons/fluent/es/Home24Filled';
import Image24Regular from '@vicons/fluent/es/Image24Regular';
import Info24Regular from '@vicons/fluent/es/Info24Regular';
import Layer24Regular from '@vicons/fluent/es/Layer24Regular';
import Layer24Filled from '@vicons/fluent/es/Layer24Filled';
import Library24Regular from '@vicons/fluent/es/Library24Regular';
import Library24Filled from '@vicons/fluent/es/Library24Filled';
import Link24Regular from '@vicons/fluent/es/Link24Regular';
import Live24Regular from '@vicons/fluent/es/Live24Regular';
import MoreHorizontal24Regular from '@vicons/fluent/es/MoreHorizontal24Regular';
import MoreHorizontal20Regular from '@vicons/fluent/es/MoreHorizontal20Regular';
import MusicNote224Regular from '@vicons/fluent/es/MusicNote224Regular';
import Navigation24Regular from '@vicons/fluent/es/Navigation24Regular';
import Next24Regular from '@vicons/fluent/es/Next24Regular';
import Open24Regular from '@vicons/fluent/es/Open24Regular';
import Pause24Regular from '@vicons/fluent/es/Pause24Regular';
import Pause24Filled from '@vicons/fluent/es/Pause24Filled';
import Pin24Regular from '@vicons/fluent/es/Pin24Regular';
import Pin24Filled from '@vicons/fluent/es/Pin24Filled';
import PinOff24Regular from '@vicons/fluent/es/PinOff24Regular';
import Play24Regular from '@vicons/fluent/es/Play24Regular';
import Play24Filled from '@vicons/fluent/es/Play24Filled';
import PlugDisconnected24Regular from '@vicons/fluent/es/PlugDisconnected24Regular';
import Power24Regular from '@vicons/fluent/es/Power24Regular';
import Previous24Regular from '@vicons/fluent/es/Previous24Regular';
import RadioButton24Regular from '@vicons/fluent/es/RadioButton24Regular';
import RadioButton24Filled from '@vicons/fluent/es/RadioButton24Filled';
import Search24Regular from '@vicons/fluent/es/Search24Regular';
import Search20Regular from '@vicons/fluent/es/Search20Regular';
import Settings24Regular from '@vicons/fluent/es/Settings24Regular';
import Settings24Filled from '@vicons/fluent/es/Settings24Filled';
import Speaker224Regular from '@vicons/fluent/es/Speaker224Regular';
import Speaker220Regular from '@vicons/fluent/es/Speaker220Regular';
import SpeakerMute24Regular from '@vicons/fluent/es/SpeakerMute24Regular';
import SpeakerMute20Regular from '@vicons/fluent/es/SpeakerMute20Regular';
import SpeakerOff24Regular from '@vicons/fluent/es/SpeakerOff24Regular';
import SpinnerIos20Regular from '@vicons/fluent/es/SpinnerIos20Regular';
import Star24Regular from '@vicons/fluent/es/Star24Regular';
import Star24Filled from '@vicons/fluent/es/Star24Filled';
import Stop24Regular from '@vicons/fluent/es/Stop24Regular';
import Subtract16Filled from '@vicons/fluent/es/Subtract16Filled';
import Tv24Regular from '@vicons/fluent/es/Tv24Regular';
import Tv24Filled from '@vicons/fluent/es/Tv24Filled';
import Video24Regular from '@vicons/fluent/es/Video24Regular';
import Warning24Filled from '@vicons/fluent/es/Warning24Filled';
import Warning20Filled from '@vicons/fluent/es/Warning20Filled';
import WeatherMoon24Regular from '@vicons/fluent/es/WeatherMoon24Regular';
import WeatherSunny24Regular from '@vicons/fluent/es/WeatherSunny24Regular';

/**
 * token 名 → @vicons/fluent Vue 组件 静态映射。
 * 沿用业务层既有的 ~70 个 token 名，避免大面积改 props。
 */
export const ICON_MAP = {
  camera_24_regular: Camera24Regular,
  add_24_regular: Add24Regular,
  add_20_regular: Add20Regular,
  alert_24_regular: Alert24Regular,
  alert_urgent_24_regular: AlertUrgent24Regular,
  apps_list_24_regular: AppsList24Regular,
  apps_24_regular: Apps24Regular,
  arrow_clockwise_24_regular: ArrowClockwise24Regular,
  arrow_clockwise_20_regular: ArrowClockwise20Regular,
  arrow_download_24_regular: ArrowDownload24Regular,
  arrow_left_24_regular: ArrowLeft24Regular,
  arrow_right_24_regular: ArrowRight24Regular,
  arrow_maximize_24_regular: ArrowMaximize24Regular,
  arrow_minimize_24_regular: ArrowMinimize24Regular,
  arrow_repeat_all_24_regular: ArrowRepeatAll24Regular,
  arrow_repeat_all_off_24_regular: ArrowRepeatAllOff24Regular,
  arrow_reset_24_regular: ArrowReset24Regular,
  arrow_swap_24_regular: ArrowSwap24Regular,
  arrow_upload_24_regular: ArrowUpload24Regular,
  checkmark_16_filled: Checkmark16Filled,
  checkmark_20_regular: Checkmark20Regular,
  checkmark_24_regular: Checkmark24Regular,
  checkmark_circle_24_filled: CheckmarkCircle24Filled,
  chevron_down_20_regular: ChevronDown20Regular,
  chevron_up_20_regular: ChevronUp20Regular,
  chevron_left_20_regular: ChevronLeft20Regular,
  chevron_left_24_regular: ChevronLeft24Regular,
  chevron_right_20_regular: ChevronRight20Regular,
  chevron_right_24_regular: ChevronRight24Regular,
  dark_theme_24_regular: DarkTheme24Regular,
  delete_24_regular: Delete24Regular,
  delete_20_regular: Delete20Regular,
  desktop_24_regular: Desktop24Regular,
  desktop_mac_24_regular: DesktopMac24Regular,
  dismiss_24_regular: Dismiss24Regular,
  dismiss_20_regular: Dismiss20Regular,
  dismiss_16_regular: Dismiss16Regular,
  document_24_regular: Document24Regular,
  edit_24_regular: Edit24Regular,
  edit_20_regular: Edit20Regular,
  error_circle_24_filled: ErrorCircle24Filled,
  error_circle_20_filled: ErrorCircle20Filled,
  eye_24_regular: Eye24Regular,
  eye_off_24_regular: EyeOff24Regular,
  filter_24_regular: Filter24Regular,
  folder_24_regular: Folder24Regular,
  full_screen_maximize_24_regular: FullScreenMaximize24Regular,
  full_screen_minimize_24_regular: FullScreenMinimize24Regular,
  globe_24_regular: Globe24Regular,
  home_24_regular: Home24Regular,
  home_24_filled: Home24Filled,
  image_24_regular: Image24Regular,
  info_24_regular: Info24Regular,
  layer_24_regular: Layer24Regular,
  layer_24_filled: Layer24Filled,
  library_24_regular: Library24Regular,
  library_24_filled: Library24Filled,
  link_24_regular: Link24Regular,
  live_24_regular: Live24Regular,
  more_horizontal_24_regular: MoreHorizontal24Regular,
  more_horizontal_20_regular: MoreHorizontal20Regular,
  music_note_2_24_regular: MusicNote224Regular,
  navigation_24_regular: Navigation24Regular,
  next_24_regular: Next24Regular,
  previous_24_regular: Previous24Regular,
  open_24_regular: Open24Regular,
  pause_24_regular: Pause24Regular,
  pause_24_filled: Pause24Filled,
  pin_24_regular: Pin24Regular,
  pin_24_filled: Pin24Filled,
  pin_off_24_regular: PinOff24Regular,
  play_24_regular: Play24Regular,
  play_24_filled: Play24Filled,
  plug_disconnected_24_regular: PlugDisconnected24Regular,
  power_24_regular: Power24Regular,
  radio_button_24_regular: RadioButton24Regular,
  radio_button_24_filled: RadioButton24Filled,
  search_24_regular: Search24Regular,
  search_20_regular: Search20Regular,
  settings_24_regular: Settings24Regular,
  settings_24_filled: Settings24Filled,
  speaker_2_24_regular: Speaker224Regular,
  speaker_2_20_regular: Speaker220Regular,
  speaker_mute_24_regular: SpeakerMute24Regular,
  speaker_mute_20_regular: SpeakerMute20Regular,
  speaker_off_24_regular: SpeakerOff24Regular,
  spinner_ios_20_regular: SpinnerIos20Regular,
  star_24_regular: Star24Regular,
  star_24_filled: Star24Filled,
  stop_24_regular: Stop24Regular,
  subtract_16_filled: Subtract16Filled,
  tv_24_regular: Tv24Regular,
  tv_24_filled: Tv24Filled,
  video_24_regular: Video24Regular,
  warning_24_filled: Warning24Filled,
  warning_20_filled: Warning20Filled,
  weather_moon_24_regular: WeatherMoon24Regular,
  weather_sunny_24_regular: WeatherSunny24Regular,
} as const satisfies Record<string, Component>;

/** 业务侧使用的图标名 token 联合类型；新增图标先登记本表再使用。 */
export type FluentIconName = keyof typeof ICON_MAP;

/** 未登记名兜底：用通用圆形占位，避免布局抖动。 */
export const FALLBACK_ICON: Component = Circle24Regular;

/**
 * 解析图标名为 Vue 组件。
 * @param name 业务侧使用的图标 token；未登记时回退到 Circle24Regular
 * @return Vue 组件，可作为 <component :is> 使用
 */
export function resolveIcon(name: FluentIconName | string): Component {
  return (ICON_MAP as Record<string, Component>)[name] ?? FALLBACK_ICON;
}

/** 调试辅助：返回当前已登记的图标映射数量。 */
export function loadedIconCount(): number {
  return Object.keys(ICON_MAP).length;
}
