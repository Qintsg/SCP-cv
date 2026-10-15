# Implementation Plan: 网页双协议入口与共享安全跳转

**Branch**: `main` | **Date**: 2026-10-12 | **Spec**: [spec.md](spec.md)

**Input**: `specs/011-http-https-access/spec.md`

## Summary

HTTP主监听，配置TLS时增加独立HTTPS监听，共用Vite middleware。通过同源无秘密入口发现接口获得实际HTTPS端口；HTTP共享先确认再导航，不自动采集。按请求协议选择上游，认证及CSRF按协议分名。

2026-10-15更新：TLS已配置时，HTTP middleware在业务处理前返回307与no-store，目标为原主机+实际HTTPS端口+原路径/查询；HTTPS正常提供内容，未配置TLS的开发服务保持原HTTP。发现接口同样遵循跳转，HTTPS仍返回原版本/端口JSON。纯目标构造及真实HTTP socket覆盖主机/端口/方法/零下游调用；D4正常TLS客户端核验跳转和内容。无应用源码或后端合同变更，只需网页Node重启。

## Technical Context

**Language/Version**: TypeScript6/Vue3、Node现有运行时、C#/.NET10。

**Primary Dependencies**: Vite8.2、Node https、既有Naive UI Dialog、ASP.NET CookieAuthentication。

**Storage**: 无新数据库；沿用会话及主机配置，入口信息在环境变量/内存。

**Testing**: Node test/HappyDOM、xUnit实际HTTP认证、真实浏览器布局/导航、独立可信HTTPS客户端。

**Target Platform**: Windows本机网页；封装端原策略保持，不自动部署D4。

**Project Type**: 已有网页/Windows播放控制服务。

**Performance Goals**: 入口发现5秒硬超时；HTTPS点击原采集调用栈不延迟。

**Constraints**: 私钥在frontend之外；证书错误拒绝启动，不关闭上游TLS验证；端口严格固定；账号/主机/页面失效取消迟到跳转。

**Scale/Scope**: 两共享窗口、两网页监听，不新增播放器或物理动作。

## Constitution Check

研究前/设计后均通过：I无自动采集或物理副作用、确定清理；II独立011追溯010；III地址/代理/认证/DOM/浏览器分层验证；IV私有接收页仍loopback+cap；V复用现有middleware/dialog，无新依赖/数据库。无例外。

## Project Structure

### Documentation (this feature)

```text
specs/011-http-https-access/
  spec.md, plan.md, research.md, data-model.md, quickstart.md, tasks.md
  contracts/access.yaml, checklists/requirements.md, verification/{local,d4-deployment}.md
```

### Source Code (repository root)

```text
frontend/config/{dualProtocol.ts,https.ts}
frontend/vite.config.ts
frontend/src/platform/connection.ts
frontend/src/features/screen-share/{httpsUpgrade.ts,ScreenSharePanel.vue,useScreenShare.ts}
frontend/src/features/settings/ServerConnectionView.vue
frontend/src/locales/zh-CN/
frontend/scripts/*https*.test.mjs, client-connection.test.mjs, screen-sharing-panel.test.mjs
runtime-dotnet/src/ScpCv.Infrastructure/Auth/{ProtocolCookieManager.cs,AuthServiceCollectionExtensions.cs}
runtime-dotnet/tests/ScpCv.ControlHost.Tests/ProtocolAuthTests.cs
docs/{screen-sharing.md,使用文档.md,维护文档.md,known-pitfalls.md,CHANGELOG.md}
```

**Structure Decision**: 扩展三个既有边界的小模块，不向大型api.ts堆新逻辑；封装端保留HTTPS策略并由设置页明确提醒。

## 后续D4部署

2026-10-12用户授权：通过现有secondary（本机gitlab远端）推送已验证源码，D4在D:/SCP-cv快进；完整运行包安装到新的artifacts目录，不覆盖010包。协作停止精确原进程组后冷备份现有数据库，沿用原DataRoot和DISPLAY2/3，更新专属计划任务启动配置。新私有TLS目录使用补齐AKI/SKI的用户叶证书/叶私钥和公开根，HTTP5173、HTTPS5174，HTTP上游仅loopback18444、HTTPS上游127.0.0.1:18443，receiver仍loopback18444。验收只做正常认证、只读状态和HTTP共享确认/导航，零采集/预约/播放器换源；出错保留旧包、配置和冷备份供原配置恢复，不重置数据库或导入根。
