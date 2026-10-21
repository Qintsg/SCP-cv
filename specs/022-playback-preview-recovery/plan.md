# Implementation Plan: 播放恢复与摄像机低延迟

**Branch**: `main` | **Date**: 2026-10-21 | **Spec**: [spec.md](spec.md)

## Summary

通过当前进程身份、Windows事件及真实媒体复现定位播放故障，修复运行组稳定性和背景音乐状态恢复。摄像机保留MediaMTX、同源共享和有界编码，将容器读取交给成熟组件，并降低编码组等待与浏览器缓冲。仓库的当前命令统一npm，辅助Python工具使用uv和直接导入。

## Technical Context

- Language/Version: C#/.NET10及Framework4.8，Vue3/TypeScript，Node24/npm11.12.1。
- Dependencies: 保留LibreOffice、VLC、MediaMTX、FFmpeg和mpegts.js；FLV容器职责采用成熟库，公共API和实际FFmpeg验证通过后使用。
- Storage: SQLite及现有媒体目录；不直接编辑用户数据库。
- Testing: 真实API、Named Pipe、媒体文件、原生播放器、音频设备、FFmpeg和只读浏览器；本轮新增验证不采用mock。
- Target: Windows x64本机和D4交互会话，仍只创建两个原生播放器。
- Performance: 相同受控画面的软件附加延迟降低至少30%；各条预览按需共享、资源有界。
- Constraints: 不操作实体映射、电源、云台或其它应用声音；保留当前显示模式、4K软件支持及用户引用；日志不记录URI凭据和请求正文。

## Constitution Check

运行安全、身份验证、数据写入职责和真实验证要求保持。已有Simulation能力不参与本轮修复验收；不采用模拟成功。规范、计划、任务和证据按同一功能记录。成熟容器组件和必要依赖用于实际格式处理。

## Project Structure

- `runtime-dotnet/src/ScpCv.ControlHost/Ipc/`：连接故障与当前身份记录。
- `runtime-dotnet/src/ScpCv.Supervisor/`：运行组监控与实际退出原因。
- `runtime-dotnet/src/ScpCv.AudioWorker/`、`Infrastructure/Runtime/`：当前音频观测和投影。
- `runtime-dotnet/src/ScpCv.PlayerWorker/Playback/`：视频、文稿、实时媒体执行。
- `runtime-dotnet/src/ScpCv.ControlHost/StreamPreview/`：受限预览转换和共享。
- `frontend/src/features/preview/`、`frontend/electron/preview/`：实际解码、延迟及资源释放。
- `frontend/scripts/`、`.specify/scripts/python/`、当前开发文档：npm和规范一致性。

## Design

### 媒体与背景音频

先记录首个退出角色及进程退出码、当前连接身份和最近消息类型，排除旧代次事件。通过正常受控启动和STOP/OPEN执行真实恢复；状态只接受当前组和实际执行结果。对视频/PPT进行独立和交替操作，故障记录必须指向原生依赖、传输、命令或媒体中的具体环节。

### 摄像机

现有完整GOP发布和浏览器1.5秒追赶阈值是需要测量的缓冲。使用成熟媒体组件读取/生成容器，保留访问校验、包尺寸上限、单个转换器、独占Job及共享预算。真实原型验证格式处理后，降低受控GOP周期、配置直播追赶和资源级缓存，并验证编码速度持续实时。主/子通道和质量不得在失败后静默切换。

### 当前规范

使用npm11.12.1真实入口，根目录和前端脚本、Electron构建和Android路径一起调整；安装使用既有淘宝镜像，不新增锁文件。当前使用文档只给现行命令。Python工具保留函数文档，文件顶部使用中文注释元数据，直接导入需要的模块。PowerShell脚本通过apply_patch编辑并校验UTF-8 BOM。

## Verification

每项修改先复现、再修复、再运行真实验证。D4当前合法播放状态逐次核对；无法确认的旧来源不会自动重放。修复代码可在本机和D4运行验证，原包和数据保留。公开仓库推送由用户后续明确要求执行。
