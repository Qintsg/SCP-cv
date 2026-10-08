# 旧帧复核、验证证据与未完成项

相关资料：[目录](README.md)、[成帧](03-device-framing-and-crypto.md)、[业务帧](04-wall-matrix-audio.md)。

## 原抓包的状态

用户确认此前抓包原件已丢失。仓库旧资料也没有可复查的 `.pcap`／`.pcapng`、抓包时间和原厂协议版本。因此本轮不能逐包复原“当时网络上实际出现过什么”。

补充证据来自：原软件的构造算法、接收算法、业务调用链、成功登录返回的现场拓扑以及当前项目发送实现。这些证据各有用途，不把它们写成重新获得了旧抓包。

## 对旧协议结论的检查

| 项目 | 新检查结果 | 适用范围 |
| --- | --- | --- |
| 清屏 `61 FF` | 与原构造函数相同 | 当前既有零控制字节模式 |
| 提交 `61 B8`、刷新 `61 B9` | 与原函数相同 | 字节来源已确认；所有场景时序未全部恢复 |
| 45 字节映射 | 两种预设共 400 包逐字节相同 | 103 个不同帧；不等于现场像素验证 |
| 长度字段 | 原函数是两个控制字节加大端 u16 长度 | 不是无条件的大端 u32 长度 |
| 映射校验 | 只累加偏移 2～39 | 历史“加模式字节再减 1”仅在模式为1时等价 |
| 坐标 | 原函数输入右下点包含最后像素 | 接口使用宽高时须转换为 `x+w-1`、`y+h-1` |
| 第二标识、A/B/C控制字段 | 创建调用链分别对应WindowLevel、窗口总数、跨屏分片计数、屏幕索引 | 黄金参数来自黄金包，固定9/1/1的业务正确性需另验证 |
| 屏幕位置 | 登录响应的50位置全部匹配项目节点算法 | 包括45/46互换；仍不是网线／实体画面观察 |
| 传输模式 | 原软件还有可选DES外层 | 不能认为所有版本／配置都只发明文 FBFC |
| 应答 | 原客户端确实解析多种返回消息 | 当前项目只写路径不能冒充设备状态回读 |
| 编码 | 名称是UTF-8，部分IPC字符串是单字节转换，共享内存是本机整数 | 不能统一套一个字符串编码或字节序 |

## 已执行的原代码验证

| 验证 | 数量／结果 | 方法 |
| --- | --- | --- |
| 登录 | HTTP业务成功；用户重开成功 | 已有现场服务器 |
| 既有预设黄金包 | 400／400 | 原构造算法与已有固定样本逐字节比较 |
| 通用窗口编码 | 40／40 | 固定随机种子；地址、矩形、标识、控制字段变化 |
| 变长字符串、音量、层级 | 15／15 | 含中文、非BMP字符、零／边界数值，比较独立编码 |
| DES-ECB | 5／5 | 原子密钥／分组算法与独立DES实现比较 |
| 加密帧接收 | 8／8 | 原环形解析器的完整、半包、粘包、环绕和错误输入 |
| KVM鼠标／系统消息 | 8／8 | 截取原DLL在模拟器内的UDP调用参数 |
| UDP视频分类 | 7／7 | 原分类函数的NAL起始码和结束标记分支 |
| 固定应答 | 34类、99字段；1193次字段比较通过 | 原环形解析采样与独立网络字节解码；半包、环绕分别核对 |
| 扩展／变长应答 | 8条完整样例均相同 | 非空名称、语音文本、远程登录及关闭；单独记录原用户名环绕错误 |
| 多点触摸 | 18／18 | 三个原入口、0～12点、前10点截断、19字节实际UDP长度 |
| Dante与标注 | 56／56 | 13个Dante入口各4组，加4个标注格式 |
| 媒体序号与音频类型 | 8+4，全部相同 | 原序号分支、关键帧请求截点、前4音频包的AAC／PCM选择 |
| 矩形舍入 | 8／8 | 原内联toRect指令；正／负半像素、空尺寸及含末像素坐标 |
| 注册中间串 | 45组归一化一致 | 合成源串；脚本原调用存在2个大写输入差异 |
| 注册源串选择 | 10／10 | 合成CPU、卷序号和安装时间；原切片与独立公式比较 |
| 节点配置 | 50／50 | 服务器Position与项目算法比较 |

数量不相加为一个“实机测试总数”。各项验证的对象和边界不同。原程序、DLL、现场大屏和共享内存写入进程没有为这些测试实际启动。

Unicorn执行的是原始构造／解析指令；Qt字符串、容器和内存操作使用有限纯内存适配，没有加载真实Qt DLL。字符串编码名称来自原调用符号，中文测试验证其适配值参与原打包算法后的结果，不作为真实Qt ABI或媒体执行的验证。

## 接收行为的额外发现

原加密解析器 `0x0066CFB0`：

- 完整消息返回1并前移读指针。
- 只有头部或缺尾字节返回3，读指针保持。
- 跨20480字节环形缓冲末尾可以识别。
- 粘连两包时先消费一个，保留下一包。
- 错头、错尾、高长度字节非零返回2。

长度高字节非零的限制已用原代码复现，不能用新写的宽松解析器通过来覆盖原软件限制。

34类固定解析器的合成损坏校验位置样例都被接受。这只能说明这些原客户端的检查范围；发送帧校验仍按原构造保留。

`0x0066B000` 远程登录字符串有混用环形索引与固定起点复制的问题：跨缓冲末尾时状态为1，IPv4正确，文本字段错误。`0x0077E920` 固定556字节工程记录只有一次read，内容流另有累积处理。`PlayCoreGroup:0x004046F0` 接受64300→1、拒绝标准u16的65535→0。原行为和验证对象分别记录在09、07、05。

## 第三版的证据更正

早期静态常量扫描缺少未被引用函数的边界，曾把相邻独立构造归入前一个函数。本次补齐入口、使用完整函数索引重新生成调用上下文，撤销 `65B340/65B850` 的相邻命令分支归属。Dante／标注归属以及触摸实际长度也按原构造和真实发送调用参数修正。旧静态候选文件留在本地分析目录作为历史记录，当前仓库目录使用新版索引。

150个设备构造候选在合成参数／配置下完成采样，131个入口生成可识别帧。Qt字符串、系统设置单例、旧型号开关的适配都有明确值；这一数量表示代码入口覆盖，不表示131个不同厂商操作均已实机验证。

## 本地证据位置与复现

完整分析目录：`E:/Projects/SSPU/Resources/SimulatorVideoJoint-analysis/`。其中包含程序指纹、带地址字符串、Qt元数据、反汇编、原函数伪代码、调用图、脱敏前的登录资料和所有验证脚本。

常用命令：

```powershell
uv run --default-index https://pypi.org/simple --with pefile --with capstone --with unicorn python E:/Projects/SSPU/Resources/SimulatorVideoJoint-analysis/verify_protocol.py
uv run --default-index https://pypi.org/simple --with pefile --with capstone --with unicorn python E:/Projects/SSPU/Resources/SimulatorVideoJoint-analysis/verify_business_frames.py
uv run --default-index https://pypi.org/simple --with pefile --with capstone --with unicorn --with pycryptodome python E:/Projects/SSPU/Resources/SimulatorVideoJoint-analysis/verify_encryption.py
uv run --default-index https://pypi.org/simple --with pefile --with capstone --with unicorn --with pycryptodome python E:/Projects/SSPU/Resources/SimulatorVideoJoint-analysis/verify_stream_parser.py
uv run --default-index https://pypi.org/simple --with pefile --with capstone --with unicorn python E:/Projects/SSPU/Resources/SimulatorVideoJoint-analysis/verify_kvm.py
uv run --offline --default-index https://pypi.org/simple --with pefile --with capstone --with unicorn python E:/Projects/SSPU/Resources/SimulatorVideoJoint-analysis/verify_kvm.py --touch
uv run --offline --default-index https://pypi.org/simple --with pefile --with capstone --with unicorn python E:/Projects/SSPU/Resources/SimulatorVideoJoint-analysis/verify_aux_frames.py
uv run --offline --default-index https://pypi.org/simple --with pefile --with capstone --with unicorn --with pycryptodome python E:/Projects/SSPU/Resources/SimulatorVideoJoint-analysis/verify_variable_replies.py
uv run --offline --default-index https://pypi.org/simple --with pefile --with capstone --with unicorn --with pycryptodome python E:/Projects/SSPU/Resources/SimulatorVideoJoint-analysis/verify_geometry.py
```

仓库目录的 `samples/` 保存可版本管理的测试向量与摘要，`catalogs/` 保存命令和函数索引。它们不是重新生成原厂二进制的源码。

原始程序保持原SHA-256。本轮没有修改应用代码、设备电源、墙面、系统音量或注册表授权，也没有把用户正在修改的008功能产物纳入本次变更。

`--offline` 要求uv缓存已具备相应依赖。完整脚本及所有调用／反编译证据保留在本地分析目录。QString失败数值转换的0返回与有限适配依据也参照了[Qt文档](https://doc.qt.io/archives/qt-5.15/qstring.html#toInt)；宿主适配不代表加载实际Qt ABI验证。

## 范围与剩余边界

| 范围 | 还缺什么 |
| --- | --- |
| 实体画面与旧抓包 | 原抓包不可恢复；需要另一次有日期、软件版本、动作和画面对应的新证据 |
| 任意跨屏布局 | 创建参数来源、相交／裁剪分支与原矩形转换已记录；整个业务函数尚未整体模拟，现场结果未验证 |
| 固件／型号 | 构造及解析字段已索引；客户端没有名字的设备枚举和固件内部约束无法仅凭本版本确认 |
| 设备回读 | 34类固定及8条扩展路径已核对；不代表每个写操作都有完成ACK |
| 触摸与标注 | 基础打包已差分；操作系统生命周期、负触点坐标和JSON图形动作的全部适用条件未实测 |
| 工程固定记录 | 全部字节范围和接收侧读取已明确；未使用元数据按原样保留，发送程序未提供 |
| 媒体 | 分类、序号、请求与共享内存读写已记录；现场流持续解码、声卡与采样格式未验证 |
| 工程数据 | 宏与命令发送逻辑已恢复；空示例表不包含实际电源／网关／逻辑命令内容 |
| 服务器 | 实现按用户明确要求排除；客户端实际请求与可读取字段继续保留，服务端未出现于现有二进制的部分不补造 |

本轮是逆向调查和文档，没有进入SCP-cv功能实现，也没有改变当前008功能指针。新增接入仍按项目Spec Kit推进。完整客户端函数目录已导出4647项，但这不表示所有UI函数和第三方库都逐函数完成语义恢复；本次交付以通信及其本地业务来源为范围。
