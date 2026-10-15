# 本机验证

1. `pnpm --prefix frontend test`、`pnpm --prefix frontend run typecheck`、`pnpm --prefix frontend run build:web`。
2. `py -3 .specify/scripts/python/validate_specs.py --specs-dir specs`。
3. 使用本机隔离数据与正常认证启动前端；禁止指向D4写操作。
4. 检查首页→两窗口/媒体/声音/预设/设置，长页滚动后切页，键盘与底栏入口。
5. 检查媒体/预设两层路径、长名称、筛选无匹配、清除、选择取消及关闭小卡片；没有确认不执行删除或设备动作。
6. 360/390/768/1280/1920与明暗主题截图；核主内容溢出、44px、焦点、抽屉遮挡和减少动效。
7. 记录实际通过项、浏览器/封装客户端未运行项及原因，不将仿真反馈当实体效果。
