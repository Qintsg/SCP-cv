# Implementation Plan: PowerShell 7与D4横向显示排列

**Branch**: `main` | **Date**: 2026-10-22 | **Spec**: [spec.md](spec.md)

## Technical Context

- D4使用Windows10 Pro18363、admin会话1、A4000/ASPEED及既有DisplayConfig6.0.0。
- 官方PowerShell7.6.7 win-x64 ZIP核对SHA256及Microsoft签名后安装到C:/Program Files/PowerShell/7，实际执行验证旧系统兼容性。
- 迁移当前桌面CMD、两个release任务动作和维护工具；保留系统powershell.exe、全局SSH默认、Framework UNO侧车及固定CV运行包。
- 仓库runtime-dotnet/scripts/start-deployed.ps1和run-headless.ps1明确要求7.2以上；Detach复用当前pwsh。
- 复用成熟DisplayConfig，在原交互会话执行隐藏、有时限的单次维护任务；子进程进入独占Windows Job并限制提交内存。只改变源坐标，保存并保持桌面尺寸、4K信号、刷新率、旋转和缩放。

## Constitution Check

保留真实原生验收及完整退出证据；没有业务API、数据库或播放器变更。显示维护完成后按2026-10-23授权启动CV测试；不修改墙面、电源、音源、摄像机、驱动或EDID，不重启Windows。失败明确报告，不创建自动分辨率恢复任务。原未提交文件与023未完成归因保持。

## Project Structure

- specs/024-pwsh-display-layout/：规范、研究、数据模型、任务、指南及实际验证。
- runtime-dotnet/scripts/start-deployed.ps1、run-headless.ps1：后续项目入口约束。
- .validation/pwsh-display-024/：双机安装制品、有限维护脚本和原始证据；凭据及任务XML不提交。
- docs/维护文档.md、docs/input-resolution-021.md：实际环境、位置及边界。

## Implementation and Validation

1. 保存现存进程、任务动作、桌面脚本、配置及原交互会话基线。
2. 核验官方程序后实际执行版本、有限文本序列化、CIM、ScheduledTasks及脚本解析。
3. 更新项目入口，保持原参数、权限和触发器；同步startup.json动作摘要，只执行CheckOnly/NoPause，不启动任务。
4. 在原会话隐藏单次读取并核对五屏身份；按编号设置连续水平位置，不重写既有4K时序。
5. 实际读回桌面和信号，再查63–66的611A；确认无新增CV成员，清理临时任务和进程。
6. 核对并清理原组残余成员，使用新版入口启动CV，实际检查唯一进程组、两窗在线、正常网页及显示排列保持。通过10.88.50.194访问，同原SSH主机密钥核验。

发行研究及入口审查并行，远端写入由主agent串行执行。本轮没有外部业务接口，省略REST合同。
