# 验证

1. 使用本机.NET10 SDK构建运行时，运行窗口维护相关HTTP、投影与队列验证。
2. 本机真实WPF窗口执行20轮隐藏／显示，读取WS_EX_TOPMOST、窗口矩形和原媒体状态；隐藏期间换源、重新设置相同位置并检查。
3. 本机ControlHost与真实PlayerWorker通信，验证HTTP受理、Worker结果、GET/SSE状态与Web动态菜单。
4. `npm --prefix frontend run typecheck`、`npm --prefix frontend run build:web`。
5. `uv run --no-project .specify/scripts/python/validate_specs.py --specs-dir specs`。
6. Redocly CLI校验docs/openapi.yaml。

实际自动验证入口：

- `dotnet test runtime-dotnet/tests/ScpCv.Windows.Tests/ScpCv.Windows.Tests.csproj -c Release --no-restore --filter FullyQualifiedName~WindowForegroundTests`
- `dotnet test runtime-dotnet/tests/ScpCv.Integration.Tests/ScpCv.Integration.Tests.csproj -c Release --no-restore --filter FullyQualifiedName~WindowForegroundRealTests`
- 完整只读Web检查在第二条命令设置`SCP_CV_VERIFY_WINDOW_WEB=1`、`SCP_PLAYWRIGHT_MODULE`和`SCP_BROWSER`。检查由真实Kestrel及两窗用例启动，浏览器只读取页面，不模拟输入。

仅操作本机项目测试窗口，不修改D4或Windows显示模式。
