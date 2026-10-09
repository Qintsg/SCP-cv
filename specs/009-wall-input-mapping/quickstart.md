# Quickstart: 本机验证

1. 独立DataRoot、SafetyMode=Simulation，不启动D4/电源。
2. dotnet test runtime-dotnet/tests/ScpCv.Infrastructure.Tests -c Release --filter "FullyQualifiedName~VideoWall&Category!=Physical"。
3. Integration/ControlHost VideoWall测试核对组合、版本、失败、持久读取及Hardware装配。
4. pnpm --prefix frontend test、typecheck、build:web；浏览器核对桌面/窄屏及模拟状态。
5. python .specify/scripts/python/validate_specs.py --specs-dir specs；已安装Redocly校验docs/openapi.yaml。

现场验收仍需配置快照、12组合抓包和图像、IP81信号身份及最终恢复布局；本轮本机结果不替代。
