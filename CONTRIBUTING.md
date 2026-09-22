# CONTRIBUTING

感谢参与 SCP-cv 维护。这个项目直接服务上海第二工业大学 28#108 多媒体显示系统，贡献时优先保证现场稳定、变更可验证、回滚成本低。

## 1. 开始前

```powershell
git status --short --branch
git pull --rebase
dotnet restore runtime-dotnet/ScpCv.sln --locked-mode
pnpm install
pnpm install --prefix frontend
```

如果工作区存在未提交内容，先判断是否与当前任务相关。不要覆盖或回滚他人的改动。

## 2. 分支与提交

提交信息格式：

```text
type(scope): 中文摘要
```

可用类型：

- `feat`
- `fix`
- `refactor`
- `docs`
- `style`
- `test`
- `perf`
- `build`
- `ci`
- `chore`
- `revert`

示例：

```text
fix(frontend): 修复前端环境变量优先级
docs(repo): 补充维护文档
```

每个独立可审查块单独提交。不要把格式化、依赖升级、业务修复和文档大改混成一个不可审查提交。

## 3. 代码要求

- C# 代码遵循 `runtime-dotnet/Directory.Build.props` 的分析器、nullable 和 warnings-as-errors 约束。
- Python 只用于 Spec Kit/QA 辅助脚本，遵循 PEP 8 与类型注解，不得重新引入 Python 运行时服务。
- Vue / TypeScript 代码遵循 `frontend/src/` 既有组件、store、composable 和样式结构。
- 单文件超过 500 行时应优先拆分，不继续堆积实现。
- 不保留空实现、假成功逻辑或无说明占位。
- 如确需保留未完成项，只能使用 `TODO:` 或 `FIXME:`，并写清原因、影响、后续动作和风险。
- 手写注释解释意图、约束和边界，不用注释弥补弱命名。

## 4. 文档要求

以下变更必须同步文档：

- API 合同
- 配置项
- 运行或部署流程
- 权限、设备、端口和平台支持
- 错误处理和联调方式
- 第三方运行时资产

文档入口：

- `README.md`
- `docs/使用文档.md`
- `docs/维护文档.md`
- `docs/openapi.yaml`
- `docs/CHANGELOG.md`
- `STYLE.md`

## 5. 验证要求

后端：

```powershell
dotnet restore runtime-dotnet/ScpCv.sln --locked-mode
dotnet build runtime-dotnet/ScpCv.sln -c Release --no-restore
$env:http_proxy=''; $env:https_proxy=''; $env:all_proxy=''
dotnet test runtime-dotnet/ScpCv.sln -c Release --no-build --filter "Category!=Physical"
```

前端：

```powershell
pnpm --prefix frontend test
pnpm --prefix frontend run typecheck
pnpm --prefix frontend run build
```

规范与 API：

```powershell
py -3 .specify/scripts/python/validate_specs.py --specs-dir specs
pnpm --package=@redocly/cli dlx redocly lint docs/openapi.yaml
```

无法运行某项验证时，在提交或交付说明中写明原因和剩余风险。

## 6. 不提交的内容

不要提交：

- `.env`
- `frontend/.env`
- `.claude/`
- `.oms/`
- `.playwright-cli/`
- `.playwright-mcp` / `.playwright-mcp/`
- `node_modules/`
- `pnpm-lock.yaml`
- 上传媒体、日志、临时测试脚本
- `requirements*.txt`

.NET 依赖以集中包版本和项目 `packages.lock.json` 为准；Node 依赖只用 pnpm，版本以 `package.json` 的精确版本为准，不提交 pnpm 锁文件。

## 7. Pull Request 检查清单

- 变更范围清晰，没有夹带无关重构。
- 相关测试已运行并通过。
- 影响用户或现场运维的行为已更新文档。
- 没有提交本地缓存、日志、上传文件或密钥。
- `docs/CHANGELOG.md` 已记录用户可感知变更。
- 变更涉及物理副作用（墙面、声卡、显示器、设备电源、Office 放映、进程组）时，已对照
  `docs/known-pitfalls.md` §1 的路径清单，并说明仿真替代是否会掩盖接线缺失。

## 8. Spec Kit 工作流

功能需求使用 GitHub Issue 中的“功能规范提案”表单提交。维护者确认范围后，按以下顺序
生成并审查产物：

```text
/speckit-specify <需求描述>
/speckit-clarify
/speckit-plan
/speckit-tasks
/speckit-implement
/speckit-analyze
```

规范目录必须位于 `specs/<编号>-<短名称>/`，至少包含 `spec.md`；进入实现阶段后应有
`plan.md` 和 `tasks.md`。提交 PR 时填写 `.github/PULL_REQUEST_TEMPLATE.md` 中的规范
目录、验证命令、风险与回滚信息。`.github/workflows/spec-kit.yml` 会在规范或相关模板
变更时自动检查目录完整性和未完成占位符。
