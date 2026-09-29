# Implementation Plan: LibreOffice完整接入

**Branch**: main（保存点549c57f） | **Date**: 2026-09-29 | **Spec**: [spec.md](spec.md)

## Summary

替换PowerPoint播放/转换/设置，使用已安装LibreOffice及官方UNO绑定，不fork其源码。PlayerWorker仍唯一拥有Display命令和WPF窗口；每窗独立C#互操作侧车/profile拥有原生文稿，另设转换实例。先验证关键能力，再接入生产；不将原型或投影受理当播放完成。

## Technical Context

**Language/Version**: 主运行时C#/.NET10/WPF；官方旧CLI UNO使用独立.NET Framework4.8 C#侧车；前端Vue3/TypeScript。

**Primary Dependencies**: 已安装LibreOffice26.8.0.3的官方CLI/GAC程序集、URE/offapi类型配置；框架引用/协议序列化按仓库集中依赖管理；无生产Python。

**Storage**: 原件/准备产物仍由ControlHost管理；每个引擎独占profile，身份含group/instance；SQLite只由ControlHost写。

**Testing**: 非Physical policy/合同/进程归属回归、真实本机只读文稿、多开切换/关闭/媒体时钟/图像；D4须后续单独确认。

**Target Platform**: Windows x64交互桌面；侧车依赖.NET Framework4.8及正确UNO运行时类型注册。

**Project Type**: 多进程桌面播放主机与共享Web控制台。

**Performance Goals**: 有界启动/打开/关闭与超时证据；10轮双窗20次原件切换、100次控制；至少60分钟稳定性留在最终门禁，不以短跑替代。

**Constraints**: pnpm/CERNET、零付费、不修改LibreOffice、只读/禁宏/禁外链、两窗、不控制用户Office、每文件<500行。

**Scale/Scope**: 两播放实例+单转换实例；PDF/页图显式静态模式；旧PowerPoint入口退役，历史证据不改写。

## Constitution Check

- 安全/原件保护/失败真实/明确归属/代次围栏继续适用。
- 用户最新选择取代旧“单PowerPoint COM”技术约束，已写spec，治理同步单独任务，不覆盖原则或旧006结果。
- 官方CLI引用RealProxy，.NET10直接加载已失败；纯C#Net48连接成功（显式uno.ini/URE_MORE_TYPES），不能塞进主进程或复制Python原型当生产。
- 原生对象仅留侧车，WPF对象仅留Player；跨进程传递带身份/代次的纯值及验证过的OS窗口句柄。
- 未通过去UI/真实媒体控制门禁前，不宣称完整路由可交付。无源代码fork例外。

## Project Structure

```text
specs/008-libreoffice-playback/
  spec.md, plan.md, tasks.md, research.md, data-model.md, quickstart.md
  contracts/, checklists/, verification.md
runtime-dotnet/src/
  ScpCv.LibreOfficeHost/          # 独立.NET48官方CLI宿主
  ScpCv.Presentation.Protocol/   # 纯值兼容协议，不引用UNO/WPF
  ScpCv.PlayerWorker/Presentations/
  ScpCv.ControlHost/Presentations/
  ScpCv.Infrastructure/Presentations/
frontend/src/features/settings/ & display/ & sources/
```

## Phase 0 Research Decisions

- 以纯C#Frame正常承载为基础，不使用已崩溃的Hidden+外部ParentWindow组合。
- 原件可播放与静态产物准备分开；native失败明确error，静态仅人工选择。
- 先修停机Pending旧命令与过早CompleteStop，再启动新宿主，防止跨组误操作。
- IPC需父command/claim、group/owner、engine/document/source epochs、deadline及parentPID/start/session/DPI，晚结果不能更新新画面。
- 统一媒体暂停须实际冻结/恢复时钟；公开二进制接口若不可达就是技术阻断，不以stopSound/静音假修。

## Complexity Tracking

Net48侧车是官方旧混合CLI/Remoting兼容所必需，不替换主.NET10，不新增Python业务服务。持有独立引擎的profile/进程是双窗和用户文稿隔离所必需，不复用单全局Office槽位。
