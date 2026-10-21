# D4显示模式回到1080p的只读调查

日期：2026-10-21。本记录的现场时间使用北京时间（UTC+8）。

## 已取得的现场证据

1. DISPLAY2–5当前均为1920×1080，连接NVIDIA RTX A4000；控制桌面DISPLAY1为1920×1200，连接ASPEED。四路输入63–66的611A读回也均为1920×1080。此前15:01–15:10的系统与输入读回确实为3840×2160。
2. System日志在17:00:11及17:30:22各记录一次Display/4101：`nvlddmkm`停止响应并成功恢复。对应NVIDIA原始事件包含`Resetting TDR`、`Reset TDR`和`Restarting TDR`，GPUID为3b00；`nvidia-smi`确认A4000的PCI地址为3B:00.0。
3. Application日志的Windows Error Reporting同时记录两组`LiveKernelEvent 117`，故障模块为`nvlddmkm.sys_Ampere`；16:52另有`LiveKernelEvent 193`。117表示显示驱动超时，不能据此认定发生蓝屏。
4. 17:25:32的User32/1074记录由admin的RuntimeBroker发起系统重启；操作系统于17:31:57.5重新启动。该次系统重启早于本轮服务部署。日志不能证明系统重启由GPU故障自动触发。
5. 四个活动CRA2400显示目标的WMI推荐模式均为1920×1080@60Hz；各自返回23项模式，列表最大为1920×1080，未包含4K。这是显示描述返回的模式列表，不等于驱动全部自定义模式列表。
6. 四路注册表中的EDID均为128字节且SHA256相同，均无`EDID_OVERRIDE`。Windows保存配置中同时存在3840×2160和1920×1080的显示组合。保留的4K记录证明此前设置存在，不证明当前已经使用它。
7. 当前系统为Windows10 18363；NVIDIA驱动537.70，驱动日期2023-10-18。查询时GPU为39°C、利用率0%、显存389/16376MiB；此瞬时值不能解释此前超时。

## 一键启动与其它显示软件

桌面入口引用的正式核心脚本完成一次实际冷启动及两次连续重复启动，七个核心进程身份、系统显示模式、输入63–66读回均保持。项目启动路径未发现设置Windows分辨率的调用；显示枚举中的`SetThreadDpiAwarenessContext`只调整当前线程的DPI上下文，并恢复原上下文。

机器还运行GameViewer和RustDesk。GameViewer虚拟显示驱动在系统启动时有Kernel-PnP/219警告，设备为`ROOT\DISPLAY\0000`；当前四路CRA2400连接A4000。Windows会话日志在所查时段只记录本地会话登录，没有取得这些远程软件改变四路分辨率的证据。没有停止远程软件或禁用驱动。

## 可以确定的内容与调查边界

显卡超时重置和四路显示目标只报告1080p推荐模式均已确证。显卡重置或显示组合重新建立后选回该模式，是当前需要验证的分辨率恢复路径；目前缺少4K切换至1080p瞬间的模式变化记录，不能把该路径认定为已复现根因。

现存WER归档保留`Report.wer`，其引用的当天WATCHDOG转储已不在LiveKernelReports中。没有取得能够归因到特定程序、图形调用或硬件问题的内核转储。本轮不进行会中断显示的显卡重置、系统重启、远程连接切换或分辨率写入；因此没有完成自动退回1080p的受控复现，也没有实施驱动或EDID修复。

后续处理需要核对四路输入设备实际向显卡提供的4K EDID及推荐模式，确认与线缆、转换器和实际输入能力一致；同时依据兼容性要求处理A4000驱动稳定性。维护验证必须同时记录系统模式、输入读回和新的TDR事件，确认重新建立显示组合后仍保持4K。未经这一验证，不以启动时强制重设4K认定问题解决。

## 服务与保留状态

本轮没有修改显示模式、注册表、显卡或远程驱动，没有发送墙面、电视、电源或云台控制。项目服务已经完成部署和重启。19:42只读复核七个核心进程身份保持、两个播放器在线且均为空闲；没有重放此前已停止的142号来源。

本机忽略目录`.validation/deploy-022-20261021/`保存`display-environment.json`、`display-system-events.json`、`display-detailed-events.json`、`display-mode-summary.json`、`display-saved-modes.json`、`display-crash-reports.json`、`display-driver-metadata.json`及`display-final-runtime.json`。原始证据不提交公开仓库。

## 官方说明

- [Windows TDR机制](https://learn.microsoft.com/en-us/windows-hardware/drivers/display/timeout-detection-and-recovery)：GPU超时后的重置、桌面恢复及事件记录。
- [Live dump 0x117](https://learn.microsoft.com/en-us/windows-hardware/drivers/debugger/bug-check-0x117---video-tdr-timeout-detected)：显示驱动未及时响应及诊断边界。
- [显示目标模式列表](https://learn.microsoft.com/en-us/windows/win32/wmicoreprov/wmimonitorlistedsupportedsourcemodes)：WMI返回的显示描述模式及推荐索引。
- [显示器EDID](https://learn.microsoft.com/en-us/windows-hardware/drivers/display/overriding-monitor-edids)：显示能力报告及显示模式选择的关系。
