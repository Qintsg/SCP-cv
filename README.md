# SCP-cv

SCP-cv 是用于控制上海第二工业大学 28#108 多媒体显示系统的 Windows 播控平台。系统由共享 Vue 控制台、ASP.NET Core ControlHost、持久命令队列、Named Pipe 运行时和独立播放进程组成，可管理 PPT、视频、图片、网页、音频及 SRT/RTSP 直播源。配置HTTPS后，HTTP访问默认跳到同主机HTTPS并保留路径和查询；登录、控制及屏幕共享在HTTPS页面操作，详见[屏幕共享与入口配置](docs/screen-sharing.md)。播放器仅占用大屏左、右两块输出；两台小电视保留电源控制，不再有播放窗口。

Ant控制台已部署D4：[HTTP入口（自动跳转）](http://192.168.5.194/)、[HTTPS控制台](https://192.168.5.194/)，端口80/443。当前网页为c2bcf0a（基于014的Ant界面），运行包仍为012k/06d342e；保留可信63号声音输出、私有网页/Office引擎、两块大屏目标及原数据。大屏布局一次应用；媒体/预设按路径浏览，摄像机84–88及固定76保持。软件声音、正常退出与一小时混合运行已有分层证据，机位、DSP和实体声音来源等缺口仍单列。使用说明见[现场控制台](docs/web-access.md)，最新网页验证见[最新网页发布](specs/012-directory-camera-console/verification/20261018-venue-web-deployment.md)，运行组与实体边界见[运行记录](specs/012-directory-camera-console/verification/d4-deployment.md)。

## 项目信息

本机接续已修复显式软件声音绑定的继承包身份隔离缺口，尚未部署D4；完整后端1615项及正常绑定/切换/退出验证见[T046记录](specs/012-directory-camera-console/verification/20261018-audio-package-isolation.md)，本机[真人点击网页播放](specs/012-directory-camera-console/verification/20261018-human-web-gesture.md)已补证。D4真实手势与原现场欠账仍未完成。

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
Vue 3 + Ant Design Vue + Tailwind CSS 4 + Pinia + Vite（Web / Electron / Capacitor）
                         │ REST / SSE
ASP.NET Core ControlHost（SQLite + EF Core + 持久命令队列）
                         │ Named Pipe
Windows Supervisor ─┬─ PlayerWorker × 2（WPF / VLC / WebView2 / PDF / LibreOffice）
                    ├─ AudioWorker
                    │  └─ LibreOfficeHost（每窗独立，按需创建）
                    └─ MediaMTX
```

ControlHost 是业务数据库的唯一写入者。控制端只访问 REST/SSE，不直接访问 Named Pipe、数据库或原生播放对象。旧 Django/PySide 运行时已在 .NET 替换完成后移除；历史可从 Git 获取，旧 `db.sqlite3`、媒体和日志不会自动迁移或删除。

## 环境要求

- Windows 10/11 x64 与交互式桌面
- [.NET SDK 10.0.400](global.json)
- Node.js 22 或更高版本
- pnpm 11（仓库 `packageManager` 字段固定版本；不要使用 npm 安装依赖）
- LibreOffice Windows x64（包含官方.NET互操作/GAC组件；本机验证26.8.0.3）
- .NET Framework 4.8运行时与开发targeting pack（官方UNO侧车使用）
- VLC/libVLC Windows x64 运行时
- MediaMTX Windows x64

Node 依赖通过仓库级 `.npmrc` 使用 `https://registry.npmmirror.com/`（2026-10-16按维护要求选择淘宝镜像，原CERNET跳转节点失效）。项目不提交 pnpm 锁文件，依赖版本以 `package.json` 中的精确版本为准。前端工作区显式拒绝已核对、仅展示捐赠广告的`core-js`安装后脚本，其余依赖构建权限不放宽。

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
- `C:\Program Files\LibreOffice\program`，或`SCP_CV_LIBREOFFICE_PROGRAM`指定的官方安装目录

PPT/PPTX默认LibreOffice只读动态放映；上传由独立转换实例准备图片和只含图片的静态PDF。
PowerPoint设置/播放/转换入口已退役。文字适配默认关闭，开启只改变内存布局，不修改原件。
统一媒体暂停、恢复、定位和窗口音量/静音暂不支持，不以放映暂停标志假报能力。
详细配置、回退与验证边界见[LibreOffice文稿运行指南](docs/libreoffice-playback.md)。

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

本机代理可能影响使用自定义 `Host` 头的回环 HTTP 测试，因此测试命令显式清空代理变量。`Physical` 测试、两块大屏/Office/VLC/MediaMTX/音频长稳测试和性能基准需要专用工作站，不能用 Simulation 结果替代。大屏现支持窗口1/2/输入IP81的12种全屏和左右组合及单侧映射，保留两个旧预设；已发送不等于出画，未登记IP仍禁止下发。配置、协议及现场边界见[大屏映射指南](docs/video-wall-mapping.md)。

历史D4紧凑界面及63/64配置部署、运行/备份见 [2026-10-09部署记录](docs/deployments/20261009-d4-compact.md)，当前版本以 [012部署记录](specs/012-directory-camera-console/verification/d4-deployment.md)为准。历史两窗实测、修复前后对照及未完成门禁见 [006 验证记录](specs/006-d4-live-convergence/verification.md)。部署烟雾不证明新映射出画，短时出画或1000普通命令通过也不等于60分钟混合长稳完成。

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
- [三输入大屏组合与协议接入规范](specs/009-wall-input-mapping/spec.md)
- [D4 两窗实机收敛规范](specs/006-d4-live-convergence/spec.md)
- [变更记录](docs/CHANGELOG.md)

2026-10-13工作区后续已修正IP81实测1080P默认、选源编码和手机44px设备操作。LibreOffice两实例实际声音可到63，但正常退出尚未稳定，完整来源仍禁用，未提交部署；当前结论见[选源与手机操作台接续](specs/012-directory-camera-console/verification/20261013-venue-followup.md)。

## 许可证

本项目使用 Artistic License 2.0，详见 [LICENSE](LICENSE)。
