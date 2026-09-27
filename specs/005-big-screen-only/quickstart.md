# 本机验证手册（现场服务保持关闭）

## 前提

- 不连接或启动 D4 的 ControlHost、Vite、Supervisor、Office/MediaMTX；不发送墙面、电源和显示器命令。
- 使用本机 `C:\Users\qintsg\Desktop\Resources` 的测试文稿、视频和音频；验证时复制到 `.validation/`，不修改原件或提交媒体。
- .NET 10 与 pnpm 已安装；测试进程按项目既有方式绕过本机 HTTP 代理。

## 验证顺序

1. 运行 .NET 非 Physical 构建/测试和合同测试，确认仅两播放器就绪、旧窗口请求拒绝、预案旧目标在副作用前拒绝、电视电源合同仍存在。
2. `pnpm --prefix frontend test`、`typecheck`、`build:web` 与 Electron 构建通过；在本机模拟 API 中检查两窗导航、映射预览与未知帧禁止、PPT 实验开关及文件夹移动反馈。
3. 用本机 PPT/PPTX 样本验证上传转换作业：保留原件、页图完整且有序、默认播放不新建 Office 放映实例；转换失败/重试可诊断。
4. 用根目录及多级中文文件夹验证上传、重名、移动、重命名、下载哈希与失败补偿；既有旧日期路径不被批量改写。
5. 运行 `py -3 .specify/scripts/python/validate_specs.py --specs-dir specs` 与 Redocly OpenAPI 校验，并核对用户/维护/变更文档。

## 暂缓项

D4 两块真实输出、PowerPoint COM 上传转换、现场电视电源、大屏两个预设实体画面及新手动布局帧均不在本轮启动；需用户后续恢复现场测试并提供抓包资料。纯本机/模拟通过不得写为实体通过。
