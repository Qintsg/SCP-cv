# 本机验收

1. `git status --short --branch`确认保留既有847898d本地提交，不推送。
2. 使用pnpm执行frontend的test、typecheck、build:web、build:app、build:electron-main；本机pnpm版本启动故障时可用node --run/--test执行相同脚本，不改包管理器。
3. .NET locked restore后运行非Physical测试；只运行独立loopback预览源，不接192.168.5网段。
4. 检查50格顺/逆拖选、重叠拒绝、编辑删除、短码往返/损坏/超长、3/4禁发及4K输入裁切。
5. 媒体/预案目录同表，编辑再读取和0参数语义；后台音乐/设置/侧栏检查。
6. 用户已允许只读无头浏览器，在1440/2560/3840/390捕获并核对附图；不模拟鼠标键盘，不接D4。真实组件交互在内存DOM测试。
7. `uv run --no-project --offline --no-managed-python python .specify/scripts/python/validate_specs.py --specs-dir specs`及Redocly检查；保存design-qa.md及verification.md，如实保留现场缺口。
