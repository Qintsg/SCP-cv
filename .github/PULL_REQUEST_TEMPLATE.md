## Spec Kit 追踪

- 功能规范目录：`specs/___`
- [ ] `spec.md` 已完成并通过 clarify（或已说明为何跳过）
- [ ] `plan.md` 与 `tasks.md` 已与实现保持一致
- [ ] 需求、实现和测试可以相互追溯

## 验证

请列出实际运行的命令及结果：

```text
- [ ] dotnet restore runtime-dotnet/ScpCv.sln --locked-mode
- [ ] dotnet build runtime-dotnet/ScpCv.sln -c Release --no-restore
- [ ] dotnet test runtime-dotnet/ScpCv.sln -c Release --no-build --filter "Category!=Physical"
- [ ] pnpm --prefix frontend test
- [ ] pnpm --prefix frontend run typecheck
- [ ] pnpm --prefix frontend run build:web
- [ ] py -3 .specify/scripts/python/validate_specs.py --specs-dir specs
```

## 风险与回滚

- 现场/设备影响：
- 回滚步骤：
- 未运行的检查及原因：
