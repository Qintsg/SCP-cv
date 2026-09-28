# VLC / libVLC

## 当前 .NET 运行时

两个大屏 `PlayerWorker` 使用 LibVLCSharp.WPF；独立 `AudioWorker` 使用 LibVLCSharp。
原生库来自仓库已有的 `VideoLAN.LibVLC.Windows` NuGet 依赖，不需要 Python、`python-vlc`
或另装系统 VLC。本轮互操作诊断没有新增、升级或替换播放器依赖。

版本由 `runtime-dotnet/Directory.Packages.props` 集中固定，各项目 `packages.lock.json`
约束还原；2026-09-28 核对为 `VideoLAN.LibVLC.Windows 3.0.23`、`LibVLCSharp 3.9.4`。
正常流程是锁定还原并构建 .NET 项目，部署完整构建/发布产物，保留原生库的目录层次：

```text
<PlayerWorker 或 AudioWorker 输出目录>/
├── LibVLCSharp.dll
└── libvlc/win-x64/
    ├── libvlc.dll
    ├── libvlccore.dll
    └── plugins/...
```

NuGet 自带的 `VideoLAN.LibVLC.Windows.targets` 将 x64 库和插件复制到该目录。
当前生产调用为无自定义目录参数的 `Core.Initialize()`；LibVLCSharp 3.9.4 首先查找
绑定程序集/入口程序集旁的 `libvlc/win-x64`，之后还有程序集旁的直接库路径。
它**没有本项目旧 Python 路径所描述的自动搜索 `Program Files/VideoLAN/VLC` 逻辑**。
缺失输出库时应修复还原、构建或部署，而不是把安装系统 VLC 当作自动兜底。
加载规则见 [LibVLCSharp 3.9.4 官方源码](https://github.com/videolan/libvlcsharp/blob/3.9.4/src/LibVLCSharp/Shared/Core/Core.cs)。

## 本目录与历史资产

`tools/third_party/vlc/runtime/` 保留历史 VLC 发行文件，也可用于明确指定路径的独立诊断。
其中存在已被 Git 跟踪的历史文件；`.gitignore` 对新增解压资产的忽略不代表已有资产均未入库。
它不是当前 .NET Worker 的默认库来源，不应手工复制本目录替换 NuGet 输出。
旧版 `python-vlc` 与系统 VLC 探测说明只适用于已退役的 Python 播放服务；历史实现可在
清理前提交 `e822be9` 追溯，不适用于当前启停流程。

## SRT 互操作边界

2026-09-28 本机核对：本目录、NuGet x64 与 PlayerWorker Release 输出的
`libaccess_srt_plugin.dll` 字节相同；当前 VLC 插件的 SRT 握手宣告 **1.5.3**。
这与 [VLC 3.0.23 官方构建规则](https://github.com/videolan/vlc/blob/3.0.23/contrib/src/srt/rules.mak)
一致。不能用 `NEWS.txt` 的旧条目判定当前库版本，也不能把证书 OID 中的 `1.5.5` 误认成 SRT 版本。

有限、无真实音视频输出的同源对照中，VLC/SRT 1.5.3 收到 MediaMTX 的零载荷 ACKACK
未出现 `INVALID SIZE`；辅助 FFmpeg 9.0.2 的握手为 SRT 1.5.7，对同类控制包仍报错。
这不代表 MediaMTX 与较新 SRT 对端的互操作问题已修复，也不证明 D4 实体画面或 60 分钟长稳。
不要通过降日志级别、替换旧库或丢弃告警把此边界写成稳定性通过。
完整包级证据、解码统计、原始日志和清理范围见 [006 本机 SRT 互操作记录](../../../docs/qa/006-srt-interop.md)。
