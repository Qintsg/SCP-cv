# STYLE

本文记录 SCP-cv 的代码、界面和文档风格约定。更具体的目录规则以对应代码附近的既有模式为准。

## 1. 通用原则

- 命名表达业务语义，不使用 `tmp`、`foo`、`bar`、`data`、`obj` 等弱命名。
- 变更保持最小影响面，优先复用现有服务、组件、store 和工具函数。
- 单文件超过 500 行时优先拆分。
- 注释解释意图、约束、边界和风险，不重复代码字面含义。
- 不保留假成功、空实现和未接线的伪功能。
- 代码文件头包含文件描述、项目名、文件名、作者和日期。Python顶部使用中文注释，函数保留类型标注和reST文档。
- 文件编辑使用`apply_patch`；成熟文件格式采用对应的第三方组件。功能验证采用实际文件、服务和进程。

## 2. C# / .NET

- 遵循 `runtime-dotnet/Directory.Build.props` 的 nullable、分析器与 warnings-as-errors 约束。
- 领域规则放在 `ScpCv.Domain`，持久化和外部适配放在 `ScpCv.Infrastructure`，HTTP/进程入口保持薄层。
- ControlHost 是业务数据库唯一写入者；Worker 通过合同和 Named Pipe 协作。
- 外部副作用必须返回可诊断结果，不得静默假成功。
- Python 仅保留 Spec Kit/QA 工具脚本，遵循 PEP 8、类型标注和项目文件头约定。

## 3. TypeScript / Vue

- 014前端使用真实Ant Design Vue组件与ConfigProvider主题；菜单、Slider、Drawer/Modal及通知按Ant事件/Slot接线，不保留旧组件库兼容套壳。主页面按路由加载；共享业务请求和危险确认必须复核主机/账号/页面归属。D4部署状态与本机验收分别记录。

- API 类型集中在服务或 store 附近维护，避免组件内散落重复接口。
- 控制台状态由 Pinia store 承载，组件只处理展示和局部交互。
- 异步请求必须覆盖 loading、error、empty 或禁用态。
- 图标优先使用既有设计系统图标注册，不手写一次性 SVG。
- 组件 props 和 emits 保持显式类型。

## 4. CSS

- 使用 `frontend/src/styles/tokens.css` 中的设计令牌。
- 不在业务组件中散落十六进制颜色、阴影和任意圆角。
- 固定格式控件需要稳定尺寸，避免 hover、加载态或长文本导致布局跳动。
- 移动端触控目标不小于 44px。
- 焦点态必须可见，不移除 `outline` 后不提供替代。

## 5. 前端视觉规范

- 控制台是现场运维工具，优先清晰、密集、稳定，不做营销式 hero。
- 关键状态必须文本和颜色同时表达。
- 危险动作使用确认或明确的危险语义。
- 页面主操作和次操作层级明确，不同时出现多个同权重主按钮。
- 手机端保持底部导航和主要操作可达。

## 6. 文档

- 面向现场使用者的内容写入 `docs/使用文档.md`。
- 面向维护者的内容写入 `docs/维护文档.md`。
- API 合同只保留 `docs/openapi.yaml`。
- 变更记录写入 `docs/CHANGELOG.md`，使用中文、结果导向、可追溯描述。
- 删除或迁移脚本、配置、文档时，同步清理所有引用。

## 7. 验证

常用验证命令：

```powershell
dotnet build runtime-dotnet/ScpCv.sln -c Release --no-restore
$env:http_proxy=''; $env:https_proxy=''; $env:all_proxy=''
dotnet test runtime-dotnet/ScpCv.sln -c Release --no-build --filter "Category!=Physical"
npm --prefix frontend run typecheck
npm --prefix frontend run build:web
uv run --no-project .specify/scripts/python/validate_specs.py --specs-dir specs
```

针对性修复可先运行相关测试，但交付前应说明完整验证是否完成。

## 8. PowerShell 脚本

- `.ps1`使用UTF-8 BOM（`EF BB BF`）和LF换行，以便Windows PowerShell5.1正确读取中文。通过`apply_patch`编辑文件，完成后只读检查文件头字节及语法。
- `.gitattributes` 的 `* text=auto eol=lf` 只管行尾，**不补 BOM**，别以为过了 Git 就没问题。
- 面向现场的脚本，报错要写清缺什么、下一步跑什么命令（`runtime-dotnet/scripts/videowall-loopback.ps1` 的跳过提示即例）；改系统状态的动作要显式检查管理员权限；能用非持久写法就用（如 `netsh … store=active`），不在别人的机器上留持久改动。
- 动作要幂等且显式：`-Apply` / `-Clear` / `-Status` 这种一个动作一个开关，重复执行不报错、不产生叠加副作用。
