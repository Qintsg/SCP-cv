# 真实共享控制台与完整API联调

日期：2026-10-05；对应T072/T073/T074。不是实体播放或完整任务完成。

## 环境与边界

正式Web构建（上一轮271回归版本）由Vite preview回环20896提供；完整发布ControlHost采用Simulation、全新DataRoot、独立QA账号，只初始化本轮测试库。
应用先经`/connect`保存实际回环41877及本机HTTP开发选项，登录后进行UI操作。没有伪造业务HTTP或替换store，没有启动本机Worker、Office、电源/墙面/系统音量控制。
探针父68780/start UTC2026-10-04T22:07:44.5969215Z，Host58572；运行根`.validation/008-full-runtime-final-1791148666778/api-run-966a0e7f80cd4df991bcf629f9c47c76`。

## 真实浏览器结果

- 创建“浏览器实测资料”，POST201后显示目录与成功通知；进入新目录再上传自有图片，真实XHR201、缩略图及文件实体位于`media/浏览器实测资料/sample.png`。
- 登记RTSP回环QA地址，POST201后只显示“直播·待验证”，不会因为登记成功标在线或出画。
- 390手机编辑RTSP为错误HTTP协议，PATCH400，表单输入与旧源保留并显示协议错误；更正RTSP及名称后PATCH200、列表改名和通知一致。
- 手机菜单移动图片到根目录，PATCH200后源移出原目录，返回根目录显示；因为根已有同名sample.png，新副本为`media/sample (2).png`，没有覆盖旧原件。
  移动后副本与素材SHA均为`4AA6ADB00D9A1EF2E9CE315A122C22D8A8920612509FB63038C79FAF1CACD4A7`。
- 手机和1440桌面显控明确“控制链路已连接/播放器离线”；源按钮、音量等控制禁用，参考和空输出不冒称实体显示。
- 浏览器认证只读`GET /api/events/`返回200/text-event-stream，首事件playback_state实际含sessions与background_audio。该自有读取随后取消；界面SSE连接持续在线不证明Worker或动态状态已测试。

## 界面、日志和退出

手机编辑Drawer宽390/视口390，列表scrollWidth380；桌面1430/1440，无横向溢出。改变宽度后首捕获仍处布局切换阶段，刷新snapshot才核桌面导航和最终原图，未为过渡状态修改生产布局。
原图位于`.validation/008-full-console-mobile-stream.png`、`008-full-console-mobile-offline.png`、`008-full-console-desktop-final.png`；原始过渡图也保留。
控制台保留首次未配置主机时默认8000的csrf/status/logout连接拒绝3条，以及故意协议400一条；登录和本轮其余成功请求没有新增错误。不声明控制台0错误，也没有把输入拒绝当业务故障。

只关闭自有浏览器页面、20896 preview服务和协作完成信号；Host退出0、父进程结束、用户about:blank页保留。媒体/数据库/日志保留，没有导出用户认证表或打印Cookie。使用本机明确连接的既有域笔记，未产生新的可复用浏览器机制，无需改写笔记。
Spec Kit/git diff检查通过；本轮生产未改，未重跑旧1137后端或271前端，不把旧回归写成本轮执行。
D4活动长稳任务没有被停止/重启，最近只读2596秒/cycle56正常；完整3600秒、资源/图像复核及退出仍待后续确认。

## 尚待验收

同一真实控制台与D4 Worker实际动态SSE/出画、SRT/custom登记编辑完整UI、背景音频、PPT完整组/直播/UDP、MediaMTX与正式secondary同步仍未完成。此轮真实REST/SSE首包/页面门禁不能代替这些要求。
