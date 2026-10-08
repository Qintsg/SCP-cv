# SimulatorVideoJoint 通信协议逆向资料

调查版本：`TPSimulator.exe`，SHA-256 `657d4c123936901cabcf5119fb8a878a63b962f21b24a661b61a74f58f47c91d`。调查日期：2026-10-08。

这组文档说明原客户端实际使用的通信协议，覆盖管理 HTTP、WebSocket、设备控制和应答、加密传输、窗口与矩阵、媒体预览、进程间通信、KVM、辅助服务、文本控制及工程传输。它是对指定版本的逆向结果，不是厂商正式规范，也不代表 SCP-cv 已接入所有能力。

## 阅读入口

| 文档 | 主要内容 |
| --- | --- |
| [01 网络拓扑与生命周期](01-topology-and-lifecycle.md) | 服务器、配置路径、端口、登录与设备连接的关系 |
| [02 HTTP 与 WebSocket](02-http-and-websocket.md) | 管理请求、返回结构、会议／摄像机代理、登录通知与节点树 |
| [03 设备成帧与加密](03-device-framing-and-crypto.md) | 多种二进制格式、字节序、校验、DES 封装、TCP 环形解析 |
| [04 墙面、矩阵与音频](04-wall-matrix-audio.md) | 映射字段、几何变化、层级、关闭、音量、背景与 IPC 切换 |
| [05 媒体与本地 IPC](05-media-and-local-ipc.md) | 组播／单播、专有视频分片、关键帧请求、共享内存与进程参数 |
| [06 KVM 与辅助服务](06-kvm-and-auxiliary-services.md) | 鼠标、触摸、系统消息、标注、报警、语音与软录制通道 |
| [07 文本控制与工程传输](07-text-control-and-project-transfer.md) | CT 文本命令、XML-RPC、长度前缀 JSON、发现和文件传输 |
| [08 旧帧复核与验证](08-audit-and-verification.md) | 抓包丢失的边界、黄金包、现场配置、原算法差分和剩余工作 |
| [HTTP OpenAPI 3.0](contracts/http-openapi.yaml) | 已恢复的管理 HTTP 契约；不属于 SCP-cv 自身 API |

完整命令索引放在 `catalogs/`；示例和验证摘要放在 `samples/`。索引中的函数地址属于上述二进制版本，换版本后不能直接套用。

## 证据等级

| 标记 | 含义 |
| --- | --- |
| 实测 | 对用户已有服务器做过请求，或者用户确认过客户端结果 |
| 原代码差分 | 在隔离 CPU 模拟器中执行原始构造／解析算法，再与独立编码或预期比较 |
| 静态确认 | 从常量、反汇编、反编译和调用链定位到格式或行为 |
| 待确认 | 仅有模板、名称、部分分支或型号关联，尚不足以定义全部语义 |

文档尽量把证据等级写在结论旁。不能把“找到一个字符串”写成“完整协议已恢复”，也不能把原客户端忽略某个字段写成设备允许任意填写。

## 范围与产物

- 主程序恢复了 444 个 Qt 类、1109 个方法的元数据；具名上下文用于识别参数和操作，不单独作为语义证明。
- 主协议代码区原有的 100 处帧头位置索引包含构造、解析和型号分支，不能直接理解为 100 种已验证的命令。
- 额外分析了 `dllKVMTouch.dll`、`dllKVMMouse.dll`、`DLLPCToControl.dll`、`NetworkTopology.dll`、`PlayCoreGroup.exe`、`PlayCoreSignal.exe`、`PlayCoreRtmp.exe` 和 `VideoPreview.exe`。
- 标准 HTTP、WebSocket、RTSP、RTMP、H.264 和音频编解码的第三方库实现不在逐函数恢复范围；本组文档说明原软件如何调用它们，以及附加的私有格式。
- 尚未完全解释的型号分支、标注字段、工程传输记录和设备设置参数保留在命令索引及各章末节。

原始文件、完整服务器返回、带地址字符串、反汇编、反编译代码和 CPU 验证脚本在本机 `E:/Projects/SSPU/Resources/SimulatorVideoJoint-analysis/`。本目录只保存适合版本管理的协议说明、脱敏索引、测试向量与验证摘要；不提交数据库、完整日志、二进制或授权／登录密钥。

## 使用这些资料接入 SCP-cv

先从协议和型号选择明确的入口，再按项目 Spec Kit 创建或更新规范、设计与任务。现有 `VideoWallSequenceBuilder` 的两种预设获得了原程序字节和服务器拓扑两层补充证据；任意布局、设备应答回读、加密模式及新输入类型仍是后续功能，不能只修改常量后直接宣称实体画面正确。
