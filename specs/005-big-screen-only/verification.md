# Verification: 大屏专用控制与媒体整理

## 起点与安全边界（2026-09-27）

- 规范、计划、任务与宪章 v3.0.0 均指向两块大屏播放输出，电视电源保留；新墙面帧尚未抓包，只允许旧单/双屏对应的两个固定预设实体下发。
- 改动前本机非 Physical .NET 240/240、前端 40/40、类型检查和 Web/Electron 构建通过；这些是四窗旧版本基线，不是本功能的通过证据。
- 用户要求停止现场测试并关闭服务；D4 已受控停机，ControlHost、Vite、受管 Worker、MediaMTX 和本轮项目 Office 自动化实例均退出，18443/5173/8554/9997 无监听。后续本机验证不得重新启动 D4 或给墙面/设备发送命令。
- 本机测试素材可从 `C:\Users\qintsg\Desktop\Resources` 复制到忽略目录；不得修改原件或提交媒体。

## 待验证

- 两窗运行组、旧窗口拒绝与旧预案副作用前拒绝。
- 两块大屏显式输出绑定、两个固定预设及手动布局未知帧拒绝。
- PPT 上传逐页图片、默认图片播放、实验开关和文件夹物理路径/移动。
- 本机浏览器布局、OpenAPI 合同与所有文档一致性。
- D4 实体显示、Office 转换和新映射抓包由用户后续恢复现场测试后再执行，不得以本机模拟结果替代。

## 两窗与旧预案本机阶段（2026-09-27）

- 领域窗口 3/4 拒绝回归先红后绿；启动参数从 ControlHost 传至 Supervisor/PlayerWorker，以显式设备名在显示器列表中选址，缺失或非绑定目标 fail closed。未在 D4 重新启动以验证真实落位。
- REST 会话仅返回 1/2，旧窗口 GET/OPEN 返回 `invalid_window` 且不入队；运行组就绪只要求 player-1/2、audio、office，电视电源合同与三设备列表保持不变。
- 旧预案含 3/4 有效目标时，在视频墙、音量、数据库和队列写入前返回 `scenario_legacy_window`；历史 unset 目标可保留。旧未完成 3/4 命令在数据库初始化时标记 `retired_window`，历史记录不删除。
- 本机非 Physical .NET 合计 248/248，通过；前端 `pnpm test` 40/40、`pnpm run typecheck` 和 `pnpm run build:web` 通过。已知 Vite 大 chunk 警告仍在。真实浏览器与 D4 实体两屏暂未验证。
- OpenAPI 分拆合同将新写入窗口收缩到 1/2、旧预案只读目标保留 3/4 说明；`redocly lint docs/openapi.yaml` 通过。

## 大屏布局本机阶段（2026-09-27）

- 左/右/全屏输入的领域校验与两个已知预设识别通过；笔记本左＋`239.1.2.3` 右的草稿保存后 `can_apply=false`，应用返回 HTTP 409 / `protocol_unavailable`，模拟墙面控制器 0 次发包且活动预设不变。
- 旧“窗口 1 全屏”与“窗口 1 左／窗口 2 右”通过统一布局应用入口返回当前运行态和两会话；原 `/api/runtime/` 作为兼容预设入口保留。手写 EF 迁移在从上一版 schema 升级的本机数据库上保留 `Double` 与音量 77，并添加空草稿字段。
- 本机真实 Chromium 对桌面与 390px 手机视口验证：两窗导航、固定预设文案、左右墙面预览、保存“现场笔记本左／自定义 IP 流右”并从 API 读回，应用按钮禁用；无横向溢出或浏览器控制台错误。截图保存在忽略目录 `.validation/qa-big-screen-20260927/mapping-*.png`。测试 ControlHost/Vite 每次按创建 PID 关闭；D4 服务未启动。
- 这一阶段最新 .NET 非 Physical 257/257、前端 `pnpm test` 40/40、类型检查/Web 构建、Redocly lint 和 Spec Kit 校验通过；Vite 仍有 >500 kB chunk 警告。浏览器测试第一次误选左侧下拉框，增加左右区域与 API 映射值断言后重新通过，这是测试定位器问题，不是产品映射缺陷。D4 两个固定预设的实体墙面画面与新布局控制帧仍未验收。

## PPT 图片与实体媒体目录本机阶段（2026-09-27）

- 本机 `C:\Users\qintsg\Desktop\Resources\AllinOne.pptx` 作为只读输入，经独立 Office STA 导出 9 张有序 PNG，真实 PowerPoint 转换测试 1/1 通过；测试后无 `POWERPNT` 残留。没有调用 `SlideShowSettings.Run`，未启动原生放映。`TestPPT.pptx`（约 98 MB）和 D4 Office 实机转换未在本轮执行。
- PPT 上传登记保留原件并创建页图准备作业；Simulation 不伪造 Office 结果，页面显示排队不可播放。假 Office IPC/转换器测试覆盖发布、默认 `slide_images` 选择、翻页、失败重试、状态不明不盲重试、实验开关默认关闭及场景预检。转换作业在 Hardware 模式由 PowerPointHost 执行；现场进程所有权与动画放映仍需 D4 后续复测。
- 上传路径由日期目录改为 `DataRoot/media/` 的页面文件夹层级；根目录、中文多级目录、重名后缀、Windows 保留名与越界、源移动、目录改名/移动/删除、未登记文件拒绝和“物理已移动但未写库”故障补偿均由临时目录测试覆盖。旧日期/`uploads` 源不会被自动批量改写。页图制品独立以源 ID/摘要存于 `cache/artifacts`，移动原件后 URL 关联仍有效。
- ControlHost 合同测试验证上传→移动源→重命名目录→下载字节逐项一致；前端脚本守卫保留跨文件夹全量选源索引、根目录只显示根源和禁止把文件夹移入子孙。Chromium 页面测试实际上传 15 MB 文稿到“PPT文件”，确认原件路径和排队状态，移动该文件夹到“归档”后检查 `relative_path=归档/PPT文件` 与磁盘路径，再将源移回根目录；开发设置实验开关在 Simulation 下禁用，无浏览器控制台错误。截图在忽略目录 `.validation/qa-big-screen-20260927/`，浏览器关闭后本机 ControlHost/Vite 监听端口清空。
- 删除旧四屏物理冒烟测试前端/API 入口及 OpenAPI 合同，避免已停用测试仍被误触发。两台电视的电源按钮与协议保持，不受此删除影响。

## 最终本机门禁与未执行项（2026-09-27）

- 清空本机 HTTP 代理变量后，`dotnet test runtime-dotnet/ScpCv.sln --no-restore --filter "Category!=Physical"`：290/290 通过（Domain 41、Infrastructure 68、Contracts 18、ControlHost 66、Integration 71、Windows 26）。直接带本机代理首跑时，仅自定义 Host 头的回环测试失败；按仓库规定清空代理后全绿，未改业务代码规避。
- 前端 `pnpm --dir frontend test` 42/42、`typecheck`、`build:web` 通过；Vite 仍提示主 chunk 超过 500 kB，为既有性能提醒，不影响构建。`redocly lint docs/openapi.yaml`、Spec Kit validator 与 `git diff --check` 通过；`PlayerRuntimeHost.cs` 有 Git CRLF→LF 的工作树提示，未见空白错误。
- README、使用/维护、变更记录、已知硬件副作用清单、OpenAPI 与本规范已按新需求更新。历史 003/004 QA 报告保留作为旧版本审计，不将其中的四窗描述当作当前操作指引。
- **仍未验收**：D4 两块大屏实体落位、两个既有预设真实画面、D4 上传转换/默认页图播放和原生实验模式、50 节点新手动映射帧抓包与真实设备确认，以及长时间混合稳定性。用户要求现场服务保持关闭；本轮未连接、启动、部署或向 D4 发包，后续需单独授权恢复现场测试。未知映射当前明确禁止下发。
