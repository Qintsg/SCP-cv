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
- [ ] npm --prefix frontend run typecheck
- [ ] npm --prefix frontend run build:web
- [ ] uv run --no-project .specify/scripts/python/validate_specs.py --specs-dir specs
- [ ] 相关功能已经使用真实媒体、服务和进程验证，并记录资源释放
```

## 风险与回滚

- 现场/设备影响：
- 回滚步骤：
- 未运行的检查及原因：
