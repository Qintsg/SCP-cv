# Data Model: 大屏专用控制与媒体整理

## 有效播放窗口

- `window_id`: 仅 1、2。各有当前媒体、播放状态、物理输出设备名、静音等现有字段。
- 历史 3、4 会话行可以留在数据库作审计，但不属于有效集合，不出现在 REST/SSE 或新命令中。
- 窗口 1、2 的物理输出目标必须属于 Hardware 配置中的两个大屏设备名；配置缺失不自动选其它屏幕。

## 大屏布局

- `layout_id`、`name`、`kind`（fixed_preset/manual）、`target`（fullscreen 或 left/right）、`input_kind`（window_1/window_2/laptop/ip_stream）、`ip_address`（仅自定义输入）、`revision`、`validation_status`。
- 全屏仅一个映射；左右布局每区最多一项、无重复目标。两个固定预设不可编辑。
- `saved_layout` 是用户意图，`active_layout` 是最近实体确认成功的预设；未抓包手动布局不能成为 active。

## PPT 源与页图

- 原始源继续记录文件 URI、原始文件名、摘要、版本和所在用户文件夹。
- 转换作业绑定源 ID/版本/摘要，状态 queued/running/succeeded/failed；制品清单记录页数、有序页图路径、摘要和配方版本。
- 默认打开要求成功制品，当前播放会话报告 `slide_images` 模式及页码；实验性原生放映报告 `powerpoint` 模式。系统设置 `experimental_powerpoint_enabled` 默认 false，跨控制端共享，仅影响新打开。

## 物理媒体文件夹

- 文件夹已有 ID、名称、父 ID；物理相对路径由从根到当前文件夹的安全名称组成，不存储用户可控制的绝对路径。
- 新上传根级原件为 `media/<安全文件名>`，子文件夹按层级存储。重名使用稳定且不覆盖的后缀；展示原始文件名与实际存储名可区分。
- 移动或重命名成功后源 URI、文件夹关系、版本和关联制品指针一致；失败时数据库不报告成功，staging/补偿日志可追踪。旧路径保持有效直到显式迁移。
