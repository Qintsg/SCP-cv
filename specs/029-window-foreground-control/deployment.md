# D4部署及桌面启动

日期：2026-10-29。

## 制品与服务

实现提交`9f23702f7c65e7ffcf5f794312cd54ab7c5ae0f4`已推送GitHub与GitLab。D4从GitLab快进至对应源码，完整运行包为`D:/SCP-cv/runtime-dotnet/artifacts/deploy-20261029-029-9f23702/`。

981个运行文件与83个网页文件逐项核验SHA256。发布包大小122,620,819字节，SHA256为`D365548AA9886077C31CA2CE665EB7F0EEE278F501508D9A0290916928B006E8`。通过正常TLS从组网地址读取全部83个网页文件，字节摘要与构建一致；HTTP80跳转HTTPS443，现有客户端来源预检保持。

旧运行组5个成员均协作退出0，随后停止原后端与网页，三份数据库文件冷备位于`D:/SCP-cv/.validation/release-029/database-before/`。旧运行包、网页与任务定义保留。

新任务为`ScpCvRuntime-029-20261029`及`ScpCvFrontend-029-20261029`，均使用PowerShell7及admin交互会话1。DataRoot继续使用`D:/SCP-cv/.validation/t129-workstation`；旧027任务已禁用。

## 现场验证

- 两播放器在线，窗口1恢复PDF来源144第一页，窗口2恢复直播来源88；窗口音量、静音、循环、目标显示器及来源状态保持。
- 两窗隐藏、显示均从正式HTTP入口提交，等待PlayerWorker实际window_foreground读回；最终两窗均恢复置顶。来源、播放状态、页码、代次和声音配置在维护操作期间保持。
- 七个核心进程运行于会话1。11个媒体登记、原目录及6份原件保持，原件摘要逐项一致。
- 显示器1保持主屏1920×1200；DISPLAY16–19均为3840×2160，横向位置依次1920、5760、9600、13440。部署前后API显示配置一致。
- 系统音量保持；未发送墙面、电视、电源、云台或现场音量指令，没有修改Windows显示模式。

## 桌面入口

桌面入口更新为`C:/Users/admin/Desktop/SCP-cv 一键启动.lnk`，目标为PowerShell7，以隐藏方式运行本轮`start-desktop.ps1`及`startup.json`。

启动成功后自动结束，重复启动复用现有任务与进程；出现错误时显示原因并写入`.validation/release-029/desktop-launch.log`。需要管理员权限时使用Windows正常提权。旧CMD已归档至本轮`desktop-before.cmd`。

实际验证包含配置检查、正常启动及配置缺失失败路径；退出码分别为0、0、1，中文输出完整可读。重复启动前后七个进程的PID与启动时刻相同，播放保持。快捷方式的目标、参数、工作目录和窗口样式已读回核验。没有主动触发交互提权或失败弹窗。

证据保存在本机与D4的`.validation/release-029/`，包括`activation`、`verification`、`launcher-verification`、启动配置及正常TLS核验记录。本轮没有重新打包Windows或Android客户端。
