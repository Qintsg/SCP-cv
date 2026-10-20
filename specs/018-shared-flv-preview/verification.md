# 018 本机验证记录

日期：2026-10-20。范围为按需共享的服务端FLV预览；**未推送、未部署或连接D4**，无实体墙面、电源、云台、声卡或Windows显示设置操作。全机严格QoS不在本机通过结论中。

## 完成与追溯

| 要求 | 实现／验收 |
| --- | --- |
| FR-001、SC-001 | 现有MediaMTX＋单份服务器FFmpeg＋HTTP／WS出口，mpegts.js；显式原生与真实Chromium解码 |
| FR-002、SC-002 | Registry／SharedFeed原子复用；65并发、一上游／一编码、全部关闭后释放 |
| FR-003 | 受限输入／媒体ID，默认小窗chn1、高清chn0，无音轨，3/4不猜地址 |
| FR-004 | 可见性、离页、取消、30秒租约＋3秒宽限、Job父死亡；内存与原生分层 |
| FR-005、SC-003 | 总8Mbps可配、6上游／128租约、16KiB突发、2GOP队列、512MiB自有Job；正式直播／NIC压力尽力降档 |
| FR-006 | 完整tag／GOP追赶、编码档位、时间线代次、快速降档／30秒恢复迟滞 |
| FR-007 | 最后画面标非实时；入站／出站、CPU优先级／系统QoS限制写入使用说明 |
| FR-008、FR-009、SC-004 | 正式Cookie／CSRF／Origin、完整票据摘要防同ID重登、源修订、无任意URL代理 |
| FR-010 | 新浏览器组件与只读设置，删除旧tools/client-preview三份代码／本机令牌入口 |
| FR-011、SC-005 | OpenAPI／使用／维护／变更与许可证齐全；无D4或浏览器输入模拟 |

T001–T013均已完成本机范围；没有把旧012的现场欠账或所有封装客户端实测标成完成。

## 自动化与构建

- .NET默认非Physical全量：**1685/1685**，0失败／跳过。日志`.validation/preview018/dotnet-final.log`；各项目346、501、50、233、283、272。
- 新预览默认针对31项包含源解析、FLV／时间线、预算、65并发、慢消费者、同账号重登、HTTP／WS门禁、源修订、恢复与退出；已包含在全量中。
- 前端最终全量：**1090/1090**，0失败／跳过，204533ms；`.validation/preview018/frontend-final.tap`。新增10项真实Vue组件／假媒体边界，不冒称浏览器解码。
- 类型、Web／App／Electron-main构建通过。Vite既有未来native config扩展名提醒保留，未关闭警告。
- OpenAPI3.0经Redocly校验0错误，6警告：1条新WS101无2xx、3条旧退役410无2xx、2条旧组合模式提示。不添加虚假200或关闭规则。`.validation/preview018/redocly-final.log`。
- Spec Kit与diff／行数检查通过，变更代码均小于500行。新Node依赖由pnpm 11.22.0经现有npmmirror安装，使用原E:/.pnpm-store，保留原依赖目录，不改镜像／提交锁文件。
- mpegts.js 1.8.2 Apache-2.0与es6-promise／events的MIT原许可证已保留；未提交FFmpeg新二进制。

## 显式原生与协议取证

`StreamPreviewProcessTests`和`StreamPreviewLiveTests`标Physical，必须显式指定本机FFmpeg／MediaMTX及产物目录；默认全量不会运行它们。最终**2/2**，0跳过，33秒，见`.validation/preview018/native-final.log`。

Live测试只启动自有回环MediaMTX、1280×720合成RTMP发布者及独占Job转换器，独立测试数据库／认证账号；不会使用63／64／81或现场摄像机作为测试上游。

- 真MediaMTX v1.17.1、FFmpeg／ffprobe 9.0.2；私有随机回环端口，API sourceOnDemand路径，无第二份生产MediaMTX。
- 65观看租约／消费连接，MediaMTX仅1条RTMP读上游、1条RTSP编码器读连接，Registry仅1共享项。
- HTTP-FLV与WebSocket-FLV都实际经ffprobe逐帧解码，尺寸均含**1280、320**；时间戳不倒退。调档期间采集任务仍未完成，连接未主动关闭。
- 全部关闭后共享项与RTMP上游读连接归0；测试数据库CommandRecords为空，不发送播放／设备命令。
- ASP.NET出站HTTP／WS使用TestServer应用管线，不是现场TLS或物理NIC吞吐证据；RTMP／RTSP与原生编码是真实回环TCP。
- 捕获产物在`.validation/preview018/<测试GUID>/`，包含`http-quality-change.flv`、`websocket.flv`、`result.json`及自有配置。
- 原生单进程验证生成两组可解析GOP、CPU Idle并精确确认退出。另用专用父进程探针故意跳过Dispose后Environment.Exit，确认原身份子进程已被Job回收（PID 38736）；探针只输出合成空汇，不接网络／音频或窗口。
- 最终进程库存核对无本轮FFmpeg／MediaMTX残留；仅保留已核身份的本机只读UI夹具。

## 浏览器与UI

用户禁用computer use，仅允许只读无头浏览器。全过程没有CUA、鼠标／键盘自动输入或伪造真人点击。

- Chromium直接使用生产mpegts薄适配器读取实际网关捕获字节：**26帧、1280与320两档、0媒体／页面／控制台错误**，`.validation/preview018/browser-decoded.json`及PNG。
- 正式ClientStreamPreview Vue组件再验：完整租约／可见性代码、真实MSE解码，**33帧、最终320宽、video可见、0错误**；申请／续租API仅内存替身，真实网络只有自有回环GET。见`component-decoded.json`／PNG。
- 因只读权限，没有从浏览器操作真实POST租约；正式Cookie／CSRF和HTTP／WS应用协议由上述TestServer／原生测试独立覆盖，不偷换为浏览器真人操作。
- 完整控制台48组浅深／8尺寸／3模式：0页面／控制台错误、无水平溢出；1440×900、1920×1080、2002×990、2560×1260普通桌面，仪表盘／侧栏无滚动。短屏保留可达滚动。布局证据`.validation/preview018/browser-layout-final.log`和`.validation/console017/`最终截图。
- 本机只读预览仍为127.0.0.1:18894；它是内存夹具，不连接D4，不能用它操纵现场。

## 诊断与修复历史

1. 源／FLV模块尚不存在时测试先红灯，随后实现转绿；未用空实现证明成功。
2. 首个前端回归失败来自测试夹具JSON缺Content-Type，以及静态导入Pinia过早让Vue缓存空document；修正夹具／导入时机后13/13，再加可见性后10项预览专项通过，不改生产规则绕过。
3. 真实Chromium最初0帧、Worker报相对URL不能解析；保留`browser-before-absolute-url.*`。主线程转绝对URL后通过，未关闭Worker或压制错误。
4. 完整Vue夹具第一次无video是原生HTML未转换bare imports，改走Vite虚拟模块后正式组件33帧；不是生产功能修复或绕过。
5. 首次后端全量1条OpenAPI覆盖失败：旧路径具现化把新的GUID id替换成数字。按路径局部生成GUID，增加回归后4/4与全量1685/1685；未豁免404判据。
6. 初次新schema组合lint提示已按互斥来源分成SourceRequest／InputRequest；最终仅剩上述6条明确警告。
7. 加入中继重启丢路径的同nonce恢复、依赖恢复后终态生产者可重启，以及新订阅不缓存超预算旧高码率GOP；均有回归／真实降档依据。

## 规范一致性与未验边界

本节是原本机阶段记录；2026-10-20后续用户授权的双仓推送、D4部署、FFmpeg私有安装、两播放器与桌面启动实测见[独立部署记录](deployment.md)。下方“未部署”不再代表当前部署状态，真实流／WSS解码与QoS限制仍保持。

只读speckit-analyze：16项要求、13任务、100%覆盖，宪章冲突／未映射任务／阻塞问题均0；1处低优先级旧文件名漂移已按既有T012文档收尾同步为previewTarget／previewPlayer。

未验证或未执行：

- D4安装FFmpeg、实际设备RTMP／RTSP鉴权与出画、现场4K编码器SPS／裁切或其它原012实体欠账。
- 真实NIC极端丢包、全机绝对网络优先级、交换机QoS、无限低带宽下永不断线。
- 现场WSS证书／代理网络链、所有Electron／Android实际解码；构建通过不是其实体通过。
- 其它业务进程／网卡／防火墙／系统音频策略未修改，未推送／部署、未启停D4。

CPU Idle不是网络QoS。输出转码只能节省D4出站，不能降低摄像机原始入站；严格等级仍需要系统与网络设备配合。
