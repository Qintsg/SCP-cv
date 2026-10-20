# 免安装的直播预览（018）

2026-10-20已连同015–017推送双仓并部署D4，网页与完整运行包固定为018/643da4b，桌面新增一键启动入口。[部署记录](../specs/018-shared-flv-preview/deployment.md)包含冷备、任务、TLS与两播放器身份验收；真实设备流解码／WSS端到端和现场QoS没有在部署轮代验。

## 使用者需要做什么

正常登录控制台即可观看，不需要在看网页的电脑安装Node、FFmpeg、浏览器插件或本机预览工具。四窗卡片及大屏布局中的预览共用网页播放器；3/4仍是预留，没有猜测输入地址，也没有新增.NET播放窗口。

小窗请求设备chn1低清通道，大预览请求chn0；墙面1／2／IP81按已有VideoWall地址生成`rtmp://设备IP:1935/live/chn0或1`。已登记媒体通过ID解析其原始RTSP／RTMP地址，包括服务端保存的凭据；内置Camera的已确认`/live/chn0`可改用chn1，普通用户地址不擅自猜通道。

原始地址和密码不进入FLV网址、查询串、浏览器错误或播放器参数。自定义IP预览只允许VideoWall已配置输入或管理员列入`StreamPreview.AllowedWallInputAddresses`的地址；未登记的地址显示“暂无预览流”，也可先登记为直播媒体通过ID观看。

无流是普通空态。连接失败只在画面下方一行说明，不弹窗；重连期间可保留最后画面，但明确标注“非实时”，不能把静止图片当作仍在直播。预览没有声音，不改变正式播放／背景音乐的音频绑定。

## 数据路径与按需共享

```text
设备RTMP／RTSP → 现有MediaMTX按需路径 → 一份服务器FFmpeg → HTTP-FLV／WS-FLV → 网页
```

MediaMTX v1.17.1本身不输出FLV，因此补充的是ControlHost内的薄转换／扇出模块，不是另一套MediaMTX服务。浏览器使用Apache-2.0的mpegts.js 1.8.2，默认HTTP-FLV；服务端也提供WebSocket-FLV。

- 创建租约只登记需求，实际消费连接才拉流，不扫描或定时拉取整个媒体库。
- 完整规范化URI（含凭据）在服务端计算共享键；同一实际流的多个卡片／客户端只拉一份、只编码一份。不同通道和密码不是同一个流；请求高清与低清但实际URI相同仍共享。
- 离屏、页面隐藏、卸载、换来源会关闭本组件租约。认证／主机变化后不把旧DELETE发到新范围；未消费的迟到租约不会拉流，服务端到期兜底。
- 网页每10秒续租，30秒失联到期；最后消费连接离开后最多3秒宽限，再停止自有编码器，MediaMTX无读者约1秒关闭上游。正常链路失联释放目标≤35秒；依赖故障／退出不确定明确保留诊断，不假报释放成功。
- 编码器从自有MediaMTX回环RTSP读，不在命令行携带摄像机凭据。MediaMTX管理配置仍含原始地址，本机管理员可以查看，不声称加密隐藏。

## 网络让路与拥塞

预览默认总出站上限8Mbps、最多6个上游、128个租约。所有HTTP／WS共享限速，最多16KiB突发；不是每个观看者各占8Mbps。慢客户端队列只保留两组完整关键帧，不阻塞共享生产者；禁止随机丢FLV字节或P帧。

档位依次为320×180/4fps/96kbps、640×360/8fps/300kbps、960×540/12fps/650kbps、1280×720/15fps/1200kbps。按实际观看数分摊预算；正式直播活跃、主机网卡忙或慢写压力时收紧预算，保守档位保持至少30秒，避免反复升降。新观看者不会被塞入已经超出预算的旧高码率缓存。

调档只在完整GOP边界重启这一个自有编码器，保留HTTP／WS连接、重映射时间戳并发送新初始化／关键帧。网络或设备本身断流时仍可能中断，不能承诺永不断线。

**这不是全机绝对网络优先级。** FFmpeg使用Windows Idle CPU优先级、单线程与512MiB独占Job内存限额；这些不等于网络QoS。应用层能保证预览限额、低清优先和尽力让路，无法强制任意交换机把所有其它服务排在它之前。需要严格网络等级时，要由Windows与交换机共同配置专用流量分类／队列；不能把共享MediaMTX或ControlHost整个进程标低，否则会连正式服务一起降级。本轮未改系统QoS、路由或防火墙。

输出降码率只节省“D4→浏览器”的流量，不能反向降低“摄像机→D4”的原始RTMP码率。低清通道能减少入站；来源只有高清时需设备侧提供低码率流，软件无法凭空消除它的入站带宽。

## 服务端准备与配置

### 018部署的一键启动入口

`runtime-dotnet/scripts/start-deployed.ps1 -ConfigurationPath <部署生成的startup.json>`只接受当前主机的两个固定计划任务，核对动作及启动脚本摘要，再在全局互斥锁内启动未运行任务。启动成功需后端健康、原生两播放器在线和正常TLS网页可达；不能只看计划任务已受理。

部署会在D4当前用户桌面生成`SCP-cv 一键启动.cmd`，通过正常UAC确认运行固定启动脚本，不含密码、不使用执行策略Bypass。重复点击不会重启在线运行组；停机后保留Host的空组可由已稳定运行的原任务经正常认证API恢复。新登录任务正在冷启动时不并发提交第二次restart。部分成员残留／身份不符明确失败，不杀进程。它只启动软件，不开启拼接屏或电视电源，不切墙、不修改系统分辨率、音量或摄像机。

`-CheckOnly -NoPause`可只读检查配置；自动运维加`-NoPause`避免等待回车。配置仅保存既有凭据文件路径，不保存口令；凭据沿用受限ACL。部署结果和最终路径以[018部署记录](../specs/018-shared-flv-preview/deployment.md)为准。

观看端无依赖，服务器需已有MediaMTX和含libx264的FFmpeg。D4已配置本轮私有`D:/SCP-cv/.validation/deploy-018-20261020/ffmpeg/ffmpeg.exe`（9.0.2，摘要和原生version已核）；不改全局PATH。维护时使用真实exe绝对路径，避免全局shim／非项目命令混用；不启动PowerPoint或其它用户窗口。

ControlHost配置示例：

```json
{
  "StreamPreview": {
    "ApiBase": "http://127.0.0.1:9997/",
    "LocalRtspPort": 8554,
    "FfmpegPath": "D:/tools/ffmpeg/bin/ffmpeg.exe",
    "MaximumFeeds": 6,
    "MaximumLeases": 128,
    "EgressKilobitsPerSecond": 8000,
    "LeaseSeconds": 30,
    "IdleGraceSeconds": 3,
    "NetworkUtilizationPercent": 40,
    "AllowedWallInputAddresses": []
  }
}
```

管理API必须回环，无代理、无重定向；API与RTSP端口沿用现有MediaMTX。生产MediaMTX的RTMP listener可以继续关闭：它主动拉取设备RTMP不需要开放自身1935。不修改原`all_others`、正式发布路径、writeQueueSize或Supervisor生命周期，不在预览失败时重启运行组。

只创建带Host随机前缀的自有sourceOnDemand路径，退出只删本实例已跟踪项。正常退出、取消和父进程死亡由独占Job回收转换器；清理未确认时不重复启动同键流。设置“直播预览”提供只读数量与预算摘要，不代表设备在线确认。

外部仍走既有HTTP／HTTPS网页入口，不新增公网预览端口；Node代理明确开启WS升级，HTTPS证书验证保持开启。HTTP入口是否跳转HTTPS仍遵循现有配置。只有播放主机服务端使用Node／FFmpeg，用户电脑不安装它们。

## 认证与验证边界

先经Cookie／CSRF申请短租约，再用同一Cookie消费；租约绑定完整加密会话票据摘要、协议和Origin。同账号重登也会失效旧租约，框架自动续签可安全触发重连。WS必须有精确Origin，不接受查询串token；GET不能直接创建任意URL代理。

本机已用合成RTMP证明65观看者仅一条上游／一份编码、两种FLV出口可实际解码、1280→320降档及单调时间戳、全部关闭后释放。真实Chromium通过生产mpegts适配器解码同一捕获字节，观察到两种尺寸；它不是D4现场摄像机、WSS网络或封装客户端实测。详细结果见[018验证](../specs/018-shared-flv-preview/verification.md)。

旧`tools/client-preview`三份工具代码和浏览器本机令牌配置已退役，可从Git历史恢复，但不再作为生产备用路径。
