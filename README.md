# SCP-cv

SCP-cv 是用于控制上海第二工业大学 28#108 多媒体显示系统的 Windows 播控平台。系统由共享 Vue 控制台、ASP.NET Core ControlHost、持久命令队列、Named Pipe 运行时和独立播放进程组成，可管理 PPT、视频、图片、网页、音频及 SRT/RTSP 直播源。播放器仅占用大屏左、右两块输出；两台小电视保留电源控制，不再有播放窗口。

## 项目信息

| 项目 | 内容 |
| --- | --- |
| 开发者 | Qintsg（饶弘玮，上海第二工业大学 25网工A2）、young86（陈子阳，上海第二工业大学 25数据A2） |
| 单位 | 上海第二工业大学 / 计算机与信息工程学院 / SSPU AI-Lab / 超级棒棒糖 |
| 应用地点 | 上海第二工业大学 28#108 |
| 许可证 | Artistic-2.0 |
| 主仓库 | `https://github.com/SCP-of-SSPU/SCP-cv.git` |
| 镜像仓库 | `http://git.bbt.sspu.edu.cn/Qintsg/scp-cv.git` |

## 架构

```text
Vue 3 + Tailwind CSS 4 + Pinia + Vite（Web / Electron / Capacitor）
                         │ REST / SSE
ASP.NET Core ControlHost（SQLite + EF Core + 持久命令队列）
                         │ Named Pipe
Windows Supervisor ─┬─ PlayerWorker × 2（WPF / VLC / WebView2 / PDF / PPT 页图）
                    ├─ AudioWorker
                    ├─ PowerPointHost（唯一 STA / COM 槽）
                    └─ MediaMTX
```

ControlHost 是业务数据库的唯一写入者。控制端只访问 REST/SSE，不直接访问 Named Pipe、数据库或原生播放对象。旧 Django/PySide 运行时已在 .NET 替换完成后移除；历史可从 Git 获取，旧 `db.sqlite3`、媒体和日志不会自动迁移或删除。

## 环境要求

- Windows 10/11 x64 与交互式桌面
- [.NET SDK 10.0.400](global.json)
- Node.js 22 或更高版本
- pnpm 11（仓库 `packageManager` 字段固定版本；不要使用 npm 安装依赖）
- Microsoft PowerPoint（仅 PPT 上传转换需要；原生放映为设置中的实验性选项）
- VLC/libVLC Windows x64 运行时
- MediaMTX Windows x64

Node 依赖通过仓库级 `.npmrc` 使用 `https://mirrors.cernet.edu.cn/npm/`。项目不提交 pnpm 锁文件，依赖版本以 `package.json` 中的精确版本为准。

## 快速开始

```powershell
git clone <repo-url> SCP-cv
cd SCP-cv

dotnet restore runtime-dotnet/ScpCv.sln --locked-mode
dotnet build runtime-dotnet/ScpCv.sln -c Release --no-restore

pnpm install
pnpm --prefix frontend install
```

第三方运行时约定：

- `tools/third_party/mediamtx/mediamtx.exe`
- `tools/third_party/vlc/runtime/`，或系统安装的 `C:\Program Files\VideoLAN\VLC`
- 当前 Windows 用户可自动化调用的 Microsoft PowerPoint

## 开发运行

先启动无物理副作用的 ControlHost：

```powershell
dotnet run --project runtime-dotnet/src/ScpCv.ControlHost -- `
  --SafetyMode=Simulation `
  --urls=http://127.0.0.1:18000 `
  --DataRoot=.validation/dotnet
```

再启动共享控制台：

```powershell
pnpm --prefix frontend run dev:web
```

如需让前端直连其它地址，复制 `frontend/.env.example` 为 `frontend/.env` 并修改 `VITE_BACKEND_TARGET`。

## Windows 播放运行时

物理播放必须在 Windows 交互桌面中运行。推荐使用无头启动脚本；口令应放在 ACL 受保护的文件或 `SCP_CV_DEVELOPMENT_PASSWORD` 环境变量中，不要写入命令行或仓库。

```powershell
powershell -NoProfile -ExecutionPolicy Bypass `
  -File runtime-dotnet/scripts/run-headless.ps1 `
  -SafetyMode Hardware `
  -StartWorkers `
  -Detach `
  -DataRoot data/dotnet `
  -ListenUrls https://localhost:18443 `
  -AllowedHosts 'localhost;127.0.0.1' `
  -AllowedOrigins 'https://localhost' `
  -DevelopmentPasswordFile '<口令文件>'
```

运行组状态与启停使用 ControlHost 的已认证会话：`GET /api/runtime/` 查询，
`POST /api/system/restart/` 启动/重启，`POST /api/system/shutdown/` 协作停机；
写请求必须携带 CSRF。先确认两块大屏绑定，停机确认 Worker/Office 退出后，再以同一绝对
DataRoot 调用 `run-headless.ps1 -Stop` 停止控制面。完整流程见
[工作站操作手册](docs/qa/003-workstation-runbook.md)。独立 `runtime.ps1` 已退役，所有旧动作
均明确失败并提示迁移，不再读写另一份运行组状态文件。

配置来源主要是 `runtime-dotnet/src/ScpCv.ControlHost/appsettings.json`、命令行参数和标准 ASP.NET Core 环境变量。默认 `SafetyMode=Simulation`；只有明确切到 `Hardware` 才会访问显示器、系统音量、设备和视频墙。

## 验证

```powershell
dotnet restore runtime-dotnet/ScpCv.sln --locked-mode
dotnet build runtime-dotnet/ScpCv.sln -c Release --no-restore
$env:http_proxy=''; $env:https_proxy=''; $env:all_proxy=''
dotnet test runtime-dotnet/ScpCv.sln -c Release --no-build --filter "Category!=Physical"

pnpm --prefix frontend test
pnpm --prefix frontend run typecheck
pnpm --prefix frontend run build:web

py -3 .specify/scripts/python/validate_specs.py --specs-dir specs
pnpm --package=@redocly/cli dlx redocly lint docs/openapi.yaml
```

本机代理可能影响使用自定义 `Host` 头的回环 HTTP 测试，因此测试命令显式清空代理变量。`Physical` 测试、两块大屏/Office/VLC/MediaMTX/音频长稳测试和性能基准需要专用工作站，不能用 Simulation 结果替代。未知协议的手动墙面映射只能保存与预览，不能向实体节点下发；仅“窗口 1 全屏”和“窗口 1 左、窗口 2 右”两个已知预设可执行。

当前 D4 两窗实测、修复前后对照及未完成门禁见 [006 验证记录](specs/006-d4-live-convergence/verification.md)。短时出画或 1000 普通命令通过不等于 60 分钟混合长稳已完成。

控制台区分选中源与实际播出；离线选源/Seek 禁用，断线恢复或换源不补发旧缓冲进度。PPT 页图 missing/failed 可显式重试，queued/running/uncertain 不盲重放，通知以返回的真实准备状态为准。

## 数据边界

新的运行数据默认位于 `data/dotnet/`，验证数据应放在 `.validation/`。不要自动删除或覆盖旧 `db.sqlite3`、上传媒体、日志、凭据或其它未跟踪数据；如需清空数据，必须先停止运行时并单独确认精确目标。

## 文档

- [使用文档](docs/使用文档.md)
- [维护文档](docs/维护文档.md)
- [OpenAPI](docs/openapi.yaml)
- [已知坑与物理副作用路径](docs/known-pitfalls.md)
- [.NET 重构规范](specs/003-dotnet-runtime-refactor/spec.md)
- [视频墙控制规范](specs/004-video-wall-control/spec.md)
- [大屏专用与媒体整理规范](specs/005-big-screen-only/spec.md)
- [D4 两窗实机收敛规范](specs/006-d4-live-convergence/spec.md)
- [变更记录](docs/CHANGELOG.md)

## 许可证

本项目使用 Artistic License 2.0，详见 [LICENSE](LICENSE)。
