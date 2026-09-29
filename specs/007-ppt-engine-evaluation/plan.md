# Implementation Plan: 本机文稿引擎评估

**Branch**: 不切换当前Git分支 | **Date**: 2026-09-29 | **Spec**: [spec.md](spec.md)

## Summary

实现两份可丢弃的本机验证程序，不接入正式播放器。比较同一文稿的内容、控制、关闭和资源；负面结果原样交付。代码/媒体/日志位于 `.validation/ppt-engine-eval/`，规范和结论可审查。

## Technical Context

**Language/Version**: C#/.NET10 WPF；JavaScript；LibreOffice自带Python/UNO仅作桥接验证。

**Primary Dependencies**: WebView2 SDK1.0.3537.50（同仓库）；pptx-vanilla-viewer3.13.0（安装后核版本）；本机LibreOffice26.8.0.3。

**Storage**: 忽略目录内JSONL、PNG、只读副本、独立profile，不使用业务数据库。

**Testing**: 真实WPF宿主、Playwright Chromium辅助、UNO命令/窗口及独立图像审查。

**Target Platform**: 当前本机Windows交互桌面。

**Project Type**: 隔离诊断，不是生产接入。

**Performance Goals**: 记录实际加载/控制耗时及10轮加载/100次导航结果，动作设置有限预算，失败不换小样本冒充成功。

**Constraints**: pnpm/CERNET优先；零付费；原件不改不外上传；D4不连接；只停止登记的自有进程。

**Scale/Scope**: AllinOne9页静态复杂排版，TestPPT7页点击动画/音视频；不足覆盖过渡与所有特效。

## Constitution Check

- 安全：无设备/系统音量写入；宿主/profile/PID/输出目录独占。
- 规范：007不覆盖暂停006，spec/plan/tasks可追溯。
- 验证：浏览器不替代WPF，外部放映不替代.NET内嵌，未知不标绿。
- 边界：不共享生产原生对象、不写业务数据库、不修改供应库掩盖失败。
- 最小复杂度：仅探针和报告，不新增Mongo或生产依赖。

## Project Structure

```text
specs/007-ppt-engine-evaluation/
  spec.md, plan.md, tasks.md, research.md, data-model.md, quickstart.md
  contracts/probe.md, checklists/requirements.md, verification.md
.validation/ppt-engine-eval/
  fixtures/   # 文稿盘点和参考
  web/        # 浏览器诊断页
  webview/    # WPF实际宿主
  uno/        # 独立profile/UNO
```

## Research Decisions

- 使用公开vanilla播放器，减少框架变量；不修改内部store伪造动画。
- 同产品WebView2 SDK，但独立项目不引用生产，避免006改动影响。
- UNO先引擎/生命周期，再.NET绑定/承载；未完成层单列。
- 文稿缺过渡时记录覆盖缺口，不声称所有特效保真。

## Complexity Tracking

无宪章例外。Python桥若必需，将作为正式.NET接入的额外复杂度报告。
