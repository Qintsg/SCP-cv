# Phase 0 Research: 大屏专用控制与媒体整理

## 两窗口与旧数据库

- **Decision**: 将窗口 1、2 作为唯一有效播放目标，运行组只起两个 PlayerWorker；保留旧 SQLite 表与 `<=4` 的历史约束，不删除 3、4 行，但会话投影、命令校验、重置、场景与就绪只处理 1、2。旧 3、4 未完成命令转为可诊断的退役终态，不重放。
- **Rationale**: 删除旧行或收紧数据库 CHECK 会在已有 D4 数据上产生破坏性迁移；仅删导航又会残留可执行硬件路径。
- **Alternatives considered**: 清空旧行并重建数据库（违反数据保留）；仅前端隐藏（后端仍可操作）。

## 两块物理输出

- **Decision**: Hardware 配置显式绑定窗口 1、2 到已核对的两块大屏设备名；后端限制显示器选择和重启恢复，其他探测到的显示器只用于诊断，不作为播放器目标。配置缺失或设备不匹配时 fail closed。
- **Rationale**: D4 现有显示器多于两块，按枚举位置取前两块可能占用控制桌面或小电视。
- **Alternatives considered**: 仅减少 Worker 数量或 UI 选项（无法防止旧保存目标回放到电视）。

## 大屏布局协议

- **Decision**: 保存/预览手动布局与实体应用分离；布局可选窗口 1/2、笔记本、自定义 IP 输入，目标为全屏或左右两区。固定预设走既有单/双屏控制帧。未知手动组合在应用前返回明确“协议待抓包”，不发送任何节点包；活动布局仍维持原预设。
- **Rationale**: 现有 `VideoWallSequenceBuilder` 只有两套已知帧，笔记本/自定义流的设备输入语义未知。
- **Alternatives considered**: 从现有包猜测组播/输入编号（不安全）；完全隐藏手动界面（无法先采集配置和预览）。

## PPT 上传与默认播放

- **Decision**: 上传 `.ppt/.pptx` 保留原件，创建版本化逐页图片准备作业；允许独立 PowerPointHost 在上传转换阶段使用 STA/COM 导出图片，失败保留原件并显示不可用/可重试状态。默认播放读已完成页图，不进入放映 COM；服务端持久实验开关关闭为默认，开启后仅后续打开可选原生放映。
- **Rationale**: 用户允许转换阶段使用 PowerPoint，但要求默认实际播放不启用它；现有 OfficeHost 是唯一允许持有 COM 的进程。98 MB 测试文稿说明转换不应长时间占用上传 HTTP 请求。
- **Alternatives considered**: ControlHost 直接 COM（违背进程边界）；默认启动幻灯片再截图（仍依赖放映窗口）；仅转 PDF 而不产页图（不符合“用图片代替 PPT”）。

## 媒体目录与数据库

- **Decision**: 维持 SQLite 单写入者，不引入 MongoDB。新媒体原件按页面文件夹的真实层级落盘；衍生页图使用独立受管理制品路径并随源关联。上传先进入 staging，再执行安全命名、同名不覆盖、落盘与数据库提交；移动/重命名执行同卷原子 rename 与失败补偿，并阻止正在播放的源移动。旧源不批量迁移，用户移动或专门迁移时才调整路径。
- **Rationale**: [Immich 的 Storage Template](https://docs.immich.app/administration/storage-template/) 将用户目录/原始文件名映射到物理目录，对同名加序号，旧文件通过显式迁移任务处理；[Paperless-ngx 的 storage paths](https://docs.paperless-ngx.com/advanced_usage/) 由应用管理物理移动、替换非法字符与同名后缀，并提醒不可手工移动数据库已记录的文件。现有 SQLite 已管理文件夹与源，换 MongoDB 不解决文件系统原子性。
- **Alternatives considered**: 直接把用户输入拼成路径（越界和 Windows 保留名风险）；只改数据库 FolderId（当前缺陷）；自动全库搬迁（停机阶段风险过高）。

## 验证边界

- **Decision**: 本机可使用 `C:\Users\qintsg\Desktop\Resources` 的 PPT/视频/音频原件只读副本，运行非 Physical 自动化与本机浏览器；不重启 D4 服务、不下发墙面或设备命令。真实两屏、Office 上传转换、墙面手动帧需后续实体复测/抓包后才能验收。
- **Rationale**: 用户明确暂停现场测试与服务，同时允许本机测试；自动化证据不能冒充 D4 实体画面。
- **Alternatives considered**: 直接恢复 D4 现场服务（违背当前用户指令）。
