# Implementation Plan: 大屏输入组合与协议接入

**Branch**: `main` | **Date**: 2026-10-09 | **Spec**: [spec.md](spec.md)
**Input**: 三种输入的左右/全屏组合和已恢复协议。

## Summary

保留旧预设黄金包，新增受限均匀墙通用计划，独立协议编码/配置/回读解析。ControlHost注册真实配置，布局服务保存草稿，RuntimeStateService持久化已发送意图，前端使用服务端能力及结果。

## Technical Context

**Language/Version**: C#/.NET 10、TypeScript/Vue3。
**Primary Dependencies**: 现有EF Core、SQLite、ASP.NET Core、Naive UI；加密使用BCL，无新增运行依赖。
**Storage**: runtime_state增加已发送布局JSON、草稿版本及发送状态，只增加列。
**Testing**: xUnit黄金包/原代码向量、内存传输替身、回环TCP、HTTP、Node DOM和真实浏览器。
**Target Platform**: Windows x64主机，现有网页/薄封装控制端。
**Project Type**: 本机控制服务与共享前端。
**Performance Goals**: 50节点阶段内最多25并发，既有200ms清屏等待。
**Constraints**: 主动实现文件≤500行，既有自动生成EF快照由模型工具结构限制保留；传输成功非画面确认；密钥只能本地配置；仅原均匀10×5墙。
**Scale/Scope**: 12完整组合、6单侧组合，两个播放器，50墙面节点。

## Constitution Check

- I：清屏前预检；部分发送/取消标不确定，不自动重放，模拟显式标记。
- II：009记录替代005手动布局门禁的授权和验收条件，008草稿保留。
- III：包、下发、装配、持久化、HTTP及浏览器分别验证；现场未执行项不勾选。
- IV：前端只用REST；密钥不进入响应，ControlHost唯一写数据库。
- V：复用控制器和队列，独立codec/profile/plan/parser，不重造原软件管理和KVM。

研究前及设计后通过，无豁免。

## Project Structure

- specs/009-wall-input-mapping/：规范、设计、合同、任务、验证。
- runtime-dotnet/src/ScpCv.Infrastructure/VideoWall/：VideoWallProtocolOptions、PacketCodec、LayoutPlanner、ReplyDecoder及既有控制器/服务。
- runtime-dotnet/src/ScpCv.Infrastructure/Playback/RuntimeStateService.VideoWall.cs：应用与状态独立partial。
- runtime-dotnet/src/ScpCv.Infrastructure/Persistence/Migrations/：只增列。
- runtime-dotnet/tests/ScpCv.{Infrastructure,Integration,ControlHost}.Tests/：协议及服务测试。
- frontend/src/features/runtime/VideoWallMappingPanel.vue、frontend/scripts/video-wall*.test.mjs：页面/DOM验证。
- docs/components/schemas/VideoWall*.yaml：公共合同。

**Structure Decision**: 领域复用布局，基础设施承担设备协议与配置；回读仅收集真实接收帧，不启用未证实查询；结果保持sent_unconfirmed。

## 紧凑交互补充（2026-10-09，US4）

- 复用`VideoWallMappingPanel.vue`，以单屏/双屏切换示意块中的一个/两个`NSelect`；左右关系不因窄屏变成上下关系，不增加组合按钮。
- 复用Fluent令牌和现有字体：蓝色输入边框、浅灰屏幕底、正文和辅助文字两级；输入直接位于对应屏幕区域，不重复显示同一输入标签。保存/应用保留独立动作，不在选择时下发。
- 布局名称移至折叠选项，自定义IP按选择显示；保留单侧、未知输入禁用和草稿修订保护。全屏与双屏各自保留局部选择。
- 首页以该交互块替换旧预设卡；运行设置将原预设卡及大草稿表单合为同一组件，其他位置原两个预设不改。API、数据库及D4运行服务均不变。
- 先补DOM交互回归，再实现；本机独立Simulation真实浏览器覆盖12组合、布局往返、未知IP、首页/设置一致与390窄屏截图，最后类型/构建/前端全套及规范校验。
- 后续补充绑定修正：`VideoWallProtocolOptions`默认值和ControlHost配置使用63/64/81。Planner已有按配置选择历史兼容路径的保护：63/64不能进入固定55/56的旧序列，旧黄金用例改为显式D2绑定，不修改历史fixture。新增全组合及旧两预设D4地址回归和Hardware装配配置断言。

## 视觉精修补充（2026-10-09，FR-012）

- 场景是运维控制台中的大屏选源，不是新建展示网站。签名元素采用真实10×5拼接墙的细网格和屏幕边框；分区文字、输入控件在同一画面内，周边保持克制，不添加装饰数字或营销区。
- 复用现有主题令牌：底面白色`#ffffff`、正文墨灰`#242424`、中性浅灰`#f5f5f5`、品牌蓝`#0f6cbd`/浅蓝`#ebf3fc`、警示棕`#bc4b09`为基础设计映射；实际取CSS令牌，尊重运行时主题覆盖和深色值，不在业务组件硬编码hex。标题用既有Segoe UI体系的16px/600，选源14px/600，紧凑动作按钮12px/600，辅助12px/400，不下载字体。
- 柔和分段控件、草稿徽标、主次按钮、折叠选项及发送状态条形成层级。先自审去除多余图标/重复文案，不新增第二组输入或组合按钮；未知输入理由与实体未确认提示不能隐藏。
- Vue保留状态/动作，样式分到同目录`VideoWallMappingPanel.css`以避免堆积；新增草稿徽标和留空区域DOM回归先红后绿。自有回环Simulation浏览器先截图发现DOM，再验证12组合、键盘、360/390与浅/深主题；QA证据另存`.validation/qa-wall-009-polish/`，不覆盖前轮截图。
- 仅颜色/边框/焦点过渡，减少动效下关闭；不重跑未变更后端或实体路径，以本轮前端/真实浏览器证据交付，不把前轮1209计作新运行。
- 真实浏览器若发现共享顶栏在360px挤出主题/应急按钮，仅用既有Tailwind换行与对齐修正其几何；保留全部标签/入口，不重设计其他页面，增加现有壳层回归断言。

## D4部署补充（2026-10-09）

用户另行要求部署，本轮使用既有发布脚本在本机生成完整Release包和已验证Web制品，经内网Git快进及新独占发布目录同步到`D:/SCP-cv`。先核对运行状态并协作停止旧组，备份既有数据库/可用WAL/SHM和Web制品；媒体、私有口令、旧包及控制桌面不删除或覆盖。使用已登录交互桌面计划任务，保持原DataRoot及DISPLAY2/3绑定，启动新包并核对只读健康、组/两窗、63/64配置及新页面；禁止借部署自动下发墙面或开关电源。部署文档单独记录发布提交、摘要、备份和未验实体边界。
