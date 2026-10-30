# 本机验证

1. 使用独立`.validation/media-030/`目录和回环服务，不连接D4。
2. `npm run typecheck`、`npm run build:web`及本轮真实验证入口。
3. 长缓存网页更新后通过正式REFRESH读取新DOM，核验失败回执。
4. 真实采集/接收期间切换观察面板10次，检查同一身份及后帧；停止后核验释放。
5. 使用真实图片、视频、音频、PPT、PDF、网页及直播验证预览与资源释放。
6. 只读无头浏览器核对390、800、1440宽度、页面错误及溢出。
7. 执行Spec Kit与Redocly；将实际结果写入verification.md。
