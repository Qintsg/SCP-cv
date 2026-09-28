# Contracts: D4 实机收敛

本文件记录 006 使用与需要补齐的外部合同。完整字段以 `docs/openapi.yaml` 为准；新增或改动后需同时更新 OpenAPI 和合同测试。

## 既有控制合同

| 用户动作 | REST 合同 | 完成判据 |
| --- | --- | --- |
| 两窗播放与关闭 | `POST /api/playback/{window_id}/open/`、`close/`、`control/`、`navigate/` | 仅接受 1/2；HTTP 受理、命令完成、会话实际状态与实体画面逐项核对 |
| 运行组 | `POST /api/system/restart/`、`shutdown/`，`GET /api/runtime/`、`GET /api/sessions/` | 2 个播放器就绪及精确 PID；停止后项目子进程 0 残留 |
| 大屏预设/草稿 | `GET/PUT /api/video-wall/layout/`、`POST /api/video-wall/layout/apply/`、`POST /api/video-wall/presets/{preset}/apply/` | 固定预设实体画面正确；未知组合返回 `protocol_unavailable`，节点包 0 且活动预设不变 |
| PPT 转换与设置 | `POST /api/sources/upload/`、`POST /api/sources/{id}/prepare/`、`GET /api/sources/{id}/slides/{page}/`、`GET/PATCH /api/settings/powerpoint/` | 原件/页图/页码/Office 进程及放映窗口相互印证；实验设置跨控制端一致 |
| 媒体目录 | `GET/POST /api/folders/`、`PATCH/DELETE /api/folders/{id}/`、`PATCH /api/sources/{id}/move/`、`DELETE /api/sources/{id}/`、`GET /api/sources/{id}/download/` | 真实相对路径、实体文件与 SHA-256 一致；失败不覆盖旧原件；单源被占用/只读时 400 且保留记录，清理残留/提交结果不明时 503 `media_cleanup_pending` 并保留隔离清单 |
| 预案音量 | `POST /api/scenarios/{id}/activate/`、`GET /api/volume/` | Hardware 模式下实体 Core Audio 读回与预案目标一致；写失败返回明确错误，不能只改数据库 |

## 待补直播源登记合同

- 建议入口：`POST /api/sources/streams/`，body 包含 `source_type`（`rtsp_stream` / `srt_stream` / `custom_stream`）、`url`、`name`、可选 `folder_id` 与预热意图；返回受认证的 `MediaSource` 和“尚未验证播出”状态。`rtsp_stream` 只接受 RTSP URL，`srt_stream` 只接受 SRT URL；`custom_stream` 应限定当前 VLC 实际支持的协议集合，禁止把网页 URL 静默当作视频流。
- 后续编辑/删除复用现有媒体源合同，编辑 URL 时增加源版本并撤销旧在线结论。前端错误必须区分“地址格式无效”“连接失败”“可连接但解码失败”和“已出画”。
- 新端点必须要求会话与 CSRF，不向客户端暴露 Named Pipe、数据库或本机任意文件读写。正式字段与响应码在实现前同步到 `docs/openapi.yaml`。

## 证据合同

- 每项实机用例同时保留：触发请求及响应、源/窗口标识、Worker PID/代次、实体截图或独立视频帧采样、关键日志与资源指标。截图至少在动作后稳定帧和再次切源后各取一次，不能用 HTTP 200 代替。
- 60 分钟混合运行每分钟记录目标源、会话状态、实际画面变化、Worker 私有内存/句柄与错误数；基准保存 1000/100 样本的原始时间数据。证据可脱敏摘要写入 `verification.md`，原始媒体/截图/凭据不提交。
