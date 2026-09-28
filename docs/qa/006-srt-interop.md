# 006 本机有限 SRT 互操作记录

验证日期：2026-09-28，时间为北京时间（UTC+8）；原始 JSON 使用 UTC 时间戳。
对应 `specs/006-d4-live-convergence` 的 T026/T032、FR-010 与 SC-007。

## 1. 结论与范围

当前播放器已有的 VLC 3.0.23 插件使用 SRT 1.5.3；辅助 FFmpeg 9.0.2 的握手宣告 SRT 1.5.7。
在本机同一个 MediaMTX 发布源的有限读取中，VLC 收到 343 个 16 字节 ACKACK 时没有尺寸告警；
FFmpeg 收到 328 个同类包，对应 328 条 `UMSG: 6 INVALID SIZE: 0`。VLC 的解码和 dummy
输出计数持续增长，但仍有两条迟显警告及 `frames lost=2`，不能写成零告警、零丢帧。

本记录仅证明本机有限回环的库版本、控制包及消费者差异。测试没有显示真实窗口、输出音频，
也没有启动 D4 播放器、Office、设备电源或修改产品配置/依赖。D4 实际加载 SHA、实体画面、
独立解码与 60 分钟长稳以 [006 主验证记录](../../specs/006-d4-live-convergence/verification.md)
为准；不得用本页替代那些门禁，T026 不因本机对照而整项关闭。

## 2. 当前产品的部署与加载链

PlayerWorker 引用仓库已有的 `LibVLCSharp.WPF 3.9.4` 与 `VideoLAN.LibVLC.Windows 3.0.23`；
AudioWorker 使用相同版本的原生包。版本位于 `runtime-dotnet/Directory.Packages.props`，
各项目 `packages.lock.json` 固定还原。本次诊断没有增加或升级这些依赖。

NuGet 包自带的 targets 将 x64 原生库和插件复制到输出的 `libvlc/win-x64/`。
`PlayerRuntimeHost.GetOrCreateLibVlc()` 调用无路径参数的 `Core.Initialize()`；
LibVLCSharp 首先搜索绑定程序集/入口程序集旁的该目录，而不是本项目的
`tools/third_party/vlc/runtime/` 或自动搜索系统 `Program Files`。
查找规则来自 [LibVLCSharp 3.9.4 官方 Core.cs](https://github.com/videolan/libvlcsharp/blob/3.9.4/src/LibVLCSharp/Shared/Core/Core.cs)。

本机 `libaccess_srt_plugin.dll` 的三个位置均为 4,255,128 字节，文件版本为 VLC 3.0.23：

| 位置 | 相对于对应根目录的路径 |
| --- | --- |
| 仓库历史发行目录 | `tools/third_party/vlc/runtime/plugins/access/libaccess_srt_plugin.dll` |
| NuGet 全局包目录 | `videolan.libvlc.windows/3.0.23/build/x64/plugins/access/libaccess_srt_plugin.dll` |
| PlayerWorker Release 输出 | `libvlc/win-x64/plugins/access/libaccess_srt_plugin.dll` |

三个插件的 SHA-256 均为：

```text
D82A5B3972FD1A590CDA4DA70888FCD145CA2897F53C45DEA518AAAEC41B709D
```

本机仓库与 PlayerWorker 输出 `libvlc.dll` 同为 192,408 字节，SHA-256 为
`8AE9F16A72441F43FB4AE8F72C843736726E067EA4A8DEF2646748631CC4E872`。
独立 CLI 的加载模块清单记录了实际库路径和摘要；日志明确选用了 `access_srt`、`avcodec`、
`vdummy` 和 `adummy`，不能只凭扫描阶段“DLL 曾被加载”判定正在读取 SRT。

版本证据是三项相互印证，而非仅凭文件版本或任意 strings 匹配：

- [VLC 3.0.23 的官方 SRT 构建规则](https://github.com/videolan/vlc/blob/3.0.23/contrib/src/srt/rules.mak)固定 1.5.3，并使用静态构建。
- 同 SHA 插件内有独立字符串 `1.5.3`；`1.3.6.1.5.5.*` 属于证书 OID，不能据此认定 SRT 1.5.5。
- 实际客户端 HSREQ 扩展记录 `0x10503`，即 1.5.3；FFmpeg 为 `0x10507`，即 1.5.7。不是仅凭 VLC `NEWS.txt` 中历史 1.4.4 条目推测。

## 3. 告警所在的协议边界

`UMSG: 6` 是 ACKACK，收到的数据报长 16 字节时，正好只有 SRT 固定头，控制负载长为 0。
它不是 H.264 帧长，也不是 `pkt_size`、接收 buffer 或用户媒体尺寸为 0。

[SRT 官方协议草案的 ACKACK 定义](https://github.com/Haivision/srt-rfc/blob/main/draft-sharabayko-srt.md#ackack-acknowledgement-of-acknowledgement-ctrl-pkt-ackack)
不要求 CIF；但 [Haivision 的控制包校验变更](https://github.com/Haivision/srt/commit/fcae57145c000a9e7b72aa777adb8f85c2463242)
在入口拒绝零长度/未按 4 字节对齐的控制负载。官方 SRT 1.5.3 的
[packet.cpp](https://github.com/Haivision/srt/blob/v1.5.3/srtcore/packet.cpp) 给 ACKACK 放入 4 字节填充，
其处理路径可对照 [core.cpp](https://github.com/Haivision/srt/blob/v1.5.3/srtcore/core.cpp)。

MediaMTX 所用 gosrt 的 ACKACK 序列化不附加该填充，参见
[gosrt v0.10.0 的发送实现](https://github.com/datarhei/gosrt/blob/v0.10.0/connection.go#L1196)；
本机透明转发实测也看到 16 字节包。有限对照支持的判断是：告警由较新的 SRT 接收端与这类
控制包形态的互操作差异触发，不是通过改变 H.264 码流或 buffer 大小得到的结论。
不能把这简化成“gosrt 明确违反草案”，也不能因解码错误为 0 就断言被拒绝的控制反馈无害。

## 4. 固定素材与实验条件

使用本机合成的 320×180、25 fps、18 秒、无音频 MPEG-TS/H.264 fixture；独立本地解码得到
450 帧，H.264 错误和尺寸告警均为 0。原件 SHA-256：

```text
3DCE1D3E03B3AE7C70B2ADA86775D2A8576BC57D8278B6BED710A3E83E152081
```

两个有效实验均使用相同 SHA 的辅助 FFmpeg 9.0.2：
`589E50B766D251AFDF181DD664D40BD94407E200B019989FD7468C7D118A28D0`。
它是诊断程序，不是产品新增播放依赖。MediaMTX 使用仓库原有 v1.17.1，独立本机配置固定
`writeQueueSize=512`，仅监听回环地址；未覆盖仓库正式配置。

转发器在 UDP 边界记录控制包类型、长度、前缀及握手版本，转发同一个完整 Buffer，不重写
ACKACK、时间戳、socket ID 或媒体负载。各场景收发计数及字节数一致，长度不符、发送失败、
第三方 peer 计数均为 0。它不是协议补丁，只用于观察消费者实际收到什么。

## 5. 有效实验结果

### 5.1 发布端 A/B：20:58:16 批次

发布端最多 18 秒，每个 FFmpeg 读取端解码 12 秒，透明转发器期限 20 秒。
每相只改变发布路径：MediaMTX/gosrt 对端与直接 FFmpeg/libSRT 对端。

| 场景 | 发给读端的 ACKACK | 数量 | 尺寸告警 | 解码帧 | H.264 错误 | 读端退出 |
| --- | --- | ---: | ---: | ---: | ---: | ---: |
| MediaMTX → FFmpeg | 16 字节，0 字节控制负载 | 335 | 335 | 300 | 0 | 0 |
| FFmpeg → FFmpeg | 20 字节，4 字节填充 | 311 | 0 | 300 | 0 | 0 |

第一相 publisher/relay 均退出 0；第二相 relay 退出 0，但发布端在有限读端先关闭后报告
I/O 错误并退出 `-5`。这不是“所有端点正常退出”的通过记录，原始发布端错误日志保留。
两个读端均有 `progress=end`，仍不以退出 0 或 300 帧掩盖第一相 335 条控制包告警。

### 5.2 同发布源消费者对照：22:02:27 批次

同一个 FFmpeg 发布至同一个 MediaMTX 路径，两个读取端并发消费；源数据、发布端、MTX、
队列配置不变。VLC 使用 `--intf=dummy --vout=dummy --aout=dummy --no-audio`，忽略用户配置，
关闭 Lua/网络元数据，期限 `--run-time=12`；原生模块日志保持 verbose 2。
FFmpeg 保持 warning 级别与严格解码检查，读取期限 12 秒。没有降低日志级别规避告警。

| 消费端 | HSREQ 中 SRT 版本 | 16 字节 ACKACK | `INVALID SIZE` | 独立解码观察 |
| --- | --- | ---: | ---: | --- |
| VLC 3.0.23 | `0x10503` / 1.5.3 | 343 | 0 | avcodec 与 dummy 输出活动，RC 统计持续增长 |
| FFmpeg 9.0.2 | `0x10507` / 1.5.7 | 328 | 328 | 300 帧、0 H.264 错误、`progress=end` |

两端的启动/退出时间、缓存与反馈节奏没有被校准为等时性能基准，343 与 328 不要求相等。
比较的是各端实际收到的同形态控制包及自身告警数，不由这些数字推导吞吐或延迟优劣。

只读 RC 在回环 TCP 接口发送 `stats`，不发送播放、音量或退出命令。两个采样文件的命名
`stats-4s`/`stats-8s` 表示脚本相对模块观察完成后的等待点，不是严格从媒体首帧计时的秒数。

| VLC 原始统计 | 第一次采样 | 第二次采样 |
| --- | ---: | ---: |
| `video decoded` | 170 | 400 |
| `frames displayed`（dummy 输出） | 76 | 191 |
| `frames lost` | 2 | 2 |
| `demux corrupted` | 0 | 0 |
| `discontinuities` | 0 | 0 |
| `audio decoded` / `buffers played` | 0 / 0 | 0 / 0 |

decoded 是 VLC 自报累计计数，不能推导与 FFmpeg 300 帧逐帧等价；displayed 是 dummy 输出
计数，不是实体大屏显示证据。日志保留两条 picture late 警告（90/50 ms），lost=2 在这两个
有限采样点未继续增长；没有宣称原因已完全排除或长期不再丢帧。CLI 的主窗口句柄观察为 0。

VLC、FFmpeg 读端、发布端和两个 relay 均退出 0。MediaMTX 是长期服务，最后按自有精确
PID/启动时间停止，不把“被测试脚本终止”写成自然退出 0。

## 6. 原始证据与排除的无效尝试

本机原始证据根为 `E:\Projects\SSPU\SCP-cv\.validation\qa-srt-interop-006`。
以下表格路径相对于该证据根；此目录被忽略，不会随 Git 克隆出现。
原始素材、日志和包记录不提交。

| 相对该证据根的路径 | 内容 |
| --- | --- |
| `run-20260928-205816/ab-summary.json` | 发布端 A/B 条件、帧数、告警、ACKACK 与退出码 |
| `run-20260928-205816/fixture-create.*`、`fixture-decode.*`、`synthetic-18s.ts` | 固定素材生成/验证日志、身份及原始码流 |
| `run-20260928-205816/{mediamtx-to-ffmpeg,ffmpeg-to-ffmpeg}/` | 原始 reader/publisher 日志、`controls.jsonl`、relay 计数与阶段摘要 |
| `vlc-compare-20260928-220227/summary.json`、`packet-comparison.json` | 有效同源消费者对照及握手/包计数 |
| `vlc-compare-20260928-220227/version-chain-and-limitations.json` | 库路径、摘要、版本证据、primary 链接与限制 |
| `vlc-compare-20260928-220227/vlc/loaded-modules.json` | 实际 CLI 原生模块路径/SHA、主窗口句柄 |
| `vlc-compare-20260928-220227/{vlc,ffmpeg}/reader.*` | 启动身份、完整 stdout/stderr；VLC 保留全部迟显警告 |
| `vlc-compare-20260928-220227/vlc/{stats-4s,stats-8s}.txt` | RC 原始响应，不改写统计 |
| `vlc-compare-20260928-220227/{vlc,ffmpeg}/controls.jsonl`、`relay-summary.json` | 控制包及不变转发的原始证据 |
| `vlc-compare-20260928-220227/cleanup.json`、`remaining-endpoints.json`、`remaining-listeners.json` | 精确自有进程清理与零剩余监听 |
| `run-local-ab.ps1`、`run-vlc-compare.ps1`、`udp-control-relay.mjs`、`mediamtx*-loopback.yml` | 受控诊断入口与仅回环配置，不是产品运行流程 |

`vlc-compare-20260928-215724` 使用了该 VLC 不支持的 `--no-inhibit`，没有有效 VLC 消费证据，
被排除。`vlc-compare-20260928-215818` 虽已有读取日志和握手，但摘要误用 PS5.1
`MatchCollection[-1]` 导致统计失败，亦不作为完整通过批次；之后修正索引并显式使用
dummy 音频输出重跑。`vlc-compare-20260928-215947` 的有效初步结果为 VLC 348/0、FFmpeg
326/326；最终带 RC 原始统计的 22:02:27 批次为本页主要依据，各批次数字不混用。

## 7. 清理与尚未证明的项目

本机诊断进程均隐藏启动，并记录绝对程序路径、参数、PID、启动时间与日志。
清理只作用于匹配该身份的自有 PID，没有按进程名批量结束 VLC、Office 或其它用户程序。
最终批次 UDP 29900/29902/29904 剩余端点为 0，TCP 29906 listener 为 0，VLC 进程为 0；
原 A/B 批次 UDP 29890/29892/29894/29895 也已清理。证据目录保留，不删除素材或原始错误。

当前可下的结论是“本机现有 VLC 路径未复现较新 libSRT 的该尺寸校验告警”，不是“所有 SRT
问题已解决”。较新外部对端仍会拒绝这类控制包；当前较旧库接受它也不构成安全性或长期可靠性
证明。本轮没有修补 MTX/gosrt、替换产品库、升级/降级 SRT，也没有隐藏告警。

D4 的实际模块身份、SRT/RTSP 两窗画面与独立解码、失联恢复、至少 60 分钟资源/帧稳定性，
以及外部较新 SRT peer 的互操作处理仍需分别验收并更新主验证记录。
