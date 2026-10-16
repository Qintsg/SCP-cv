# 本机验收

1. pnpm使用仓库配置的淘宝镜像安装精确Ant版本，不提交锁文件。
2. frontend执行`pnpm test`、`pnpm run typecheck`、`pnpm run build:web`、`pnpm run build:app`。
3. 本机假API启动网页，不连接D4；浏览器先截图/检查DOM，再操作可见控件。
4. 检查单按钮、12组合、重复点击、未登记IP及旧请求；无保存入口和发送详情。
5. 验证媒体/预设目录、上传/编辑/打开到、播放、共享、摄像机/音量与设置；副作用只由假API记录。
6. 检查1440/1024/390/360px、明暗主题、焦点、弹层、44px触控与控制台异常。
7. 执行`py -3 .specify/scripts/python/validate_specs.py --specs-dir specs`及`git diff --check`，记录结果与未验证边界。
