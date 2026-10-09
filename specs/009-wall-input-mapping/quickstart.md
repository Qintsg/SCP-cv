# Quickstart: 本机验证

1. 独立DataRoot、SafetyMode=Simulation，不启动D4/电源。
2. dotnet test runtime-dotnet/tests/ScpCv.Infrastructure.Tests -c Release --filter "FullyQualifiedName~VideoWall&Category!=Physical"。
3. Integration/ControlHost VideoWall测试核对组合、版本、失败、持久读取及Hardware装配。
4. pnpm --prefix frontend test、typecheck、build:web；浏览器从首页和设置→运行设置的“大屏布局”选择单/双屏及区域内窗口1/2/IP81，核对12组合、往返保留、显式保存/应用、键盘、窄屏及模拟状态，不使用12个组合按钮。后端同时核对D4的63/64单播和组播；黄金样本用显式历史55/56配置。
5. uv run --no-project python .specify/scripts/python/validate_specs.py --specs-dir specs；API合同结构有变更时使用已安装Redocly校验docs/openapi.yaml。

现场验收仍需配置快照、12组合抓包和图像、IP81信号身份及最终恢复布局；本轮本机结果不替代。
