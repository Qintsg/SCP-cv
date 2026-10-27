# Implementation Plan: D4系统与显卡驱动升级

**Branch**: `main` | **Date**: 2026-10-27 | **Spec**: [spec.md](spec.md)

## Summary

使用官方同语言专业版安装媒体执行保留应用、文件和设置的22H2升级；在实际兼容性检查通过后替换系统。使用582.78企业驱动的显示驱动安装选项，保留原音频组件与设置。通过Windows正式策略关闭自动更新和驱动更新，并在重启后核验。

## Technical Context

**Language/Version**: PowerShell 7.6.7；已有uv/Paramiko维护连接。
**Primary Dependencies**: Microsoft Windows Setup、DISM、wbadmin、微软正式媒体、NVIDIA正式安装器。
**Storage**: D4的D盘独立逻辑磁盘，保存系统映像、项目数据备份、基线和安装日志。
**Testing**: 实机预检、Setup兼容性检查、真实退出码、重启后版本和数据核验。
**Target Platform**: D4，Windows 10专业版1909 x64、zh-CN；目标22H2及582.78。
**Project Type**: 一次性设备维护，证据写入被忽略的`.validation/upgrade025/`。
**Performance Goals**: 安装和重启有明确阶段及截止时间；读取、输出和下载均有限制。
**Constraints**: 保留软件和数据；不创建显示自动恢复；不改变墙面、电视、云台、设备电源或实体音源。
**Scale/Scope**: 一台主机、一个系统磁盘、一个独立数据磁盘、一个A4000及五个显示目标。

## Constitution Check

- 运行安全：先保存基线和系统映像，明确校验安装兼容性与退出，失败停止后续安装。
- 规范驱动：本目录包含需求、计划、任务及实机验证记录。
- 分层验证：文档与脚本语法验证、正式安装器扫描、安装后设备和业务环境实际核验。
- 进程边界：复用现有项目停机入口，仅管理本次创建的维护任务。
- 复杂度：仅使用现有工具及官方安装组件，不添加长期升级或显示守护服务。

## Project Structure

- `specs/025-d4-windows-driver-upgrade/`：需求、研究、计划、任务及验证说明。
- `.validation/upgrade025/`：本机一次性脚本及维护记录，不提交制品或敏感信息。
- `D:/SCP-cv/.validation/upgrade025/`：D4安装文件、阶段状态和项目备份。
- `D:/WindowsImageBackup/`：在不存在既有同名系统备份时使用wbadmin明确保存C盘及同一系统磁盘的引导分区；D盘项目数据另行备份，保持原分区和文件。

## Execution

1. 核对系统语言、位数、版本、空间、加密与待重启状态，记录应用、驱动、网络、显示、任务和项目入口。
2. 下载并验证正式媒体，核对专业版、zh-CN、x64和22H2；NVIDIA安装器签名必须有效。
3. 通过现有项目入口协作停止运行组，备份项目配置、业务数据库及媒体清单，执行系统关键卷映像备份。
4. 使用Windows Setup `/Auto Upgrade /Compat ScanOnly /DynamicUpdate Disable`检查实际迁移资格，读取扫描原生退出码和兼容性报告。
5. 仅在成功兼容后启动`/Auto Upgrade`安装，保留原软件和数据；必要重启后重新连接同一主机。
6. 验证22H2，安装582.78的`Display.Driver`，保留现有HD Audio与软件设置，读取真实安装日志和退出码，按需重启。
7. 配置`NoAutoUpdate=1`和`ExcludeWUDriversInQualityUpdate=1`，保留手动更新；最后重启后读取实际策略。
8. 比较基线文件与应用，核对管理网络、设备、显示和音频绑定，恢复原项目入口并验证实际服务状态。

## Validation

每个阶段记录实际开始、进程身份、截止时间、原生退出码和结果文件。成功要求系统版本、驱动版本、有效策略、保留清单和服务验证均通过。交互会话不可用时明确记录待验项目，不自动登录或保存用户密码。
