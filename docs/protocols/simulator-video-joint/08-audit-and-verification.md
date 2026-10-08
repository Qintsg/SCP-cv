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
| 第二标识、A/B/C 控制字段 | 编码位置恢复；业务枚举未全部命名 | 不能把既有常量当成全部型号规则 |
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

应答解析中还发现部分校验字节未被检查。这只能说明原客户端的检查范围；不能反过来删除发送帧校验或宣称设备无需校验。

## 本地证据位置与复现

完整分析目录：`E:/Projects/SSPU/Resources/SimulatorVideoJoint-analysis/`。其中包含程序指纹、带地址字符串、Qt元数据、反汇编、原函数伪代码、调用图、脱敏前的登录资料和所有验证脚本。

常用命令：

```powershell
uv run --default-index https://pypi.org/simple --with pefile --with capstone --with unicorn python E:/Projects/SSPU/Resources/SimulatorVideoJoint-analysis/verify_protocol.py
uv run --default-index https://pypi.org/simple --with pefile --with capstone --with unicorn python E:/Projects/SSPU/Resources/SimulatorVideoJoint-analysis/verify_business_frames.py
uv run --default-index https://pypi.org/simple --with pefile --with capstone --with unicorn --with pycryptodome python E:/Projects/SSPU/Resources/SimulatorVideoJoint-analysis/verify_encryption.py
uv run --default-index https://pypi.org/simple --with pefile --with capstone --with unicorn --with pycryptodome python E:/Projects/SSPU/Resources/SimulatorVideoJoint-analysis/verify_stream_parser.py
uv run --default-index https://pypi.org/simple --with pefile --with capstone --with unicorn python E:/Projects/SSPU/Resources/SimulatorVideoJoint-analysis/verify_kvm.py
```

仓库目录的 `samples/` 保存可版本管理的测试向量与摘要，`catalogs/` 保存命令和函数索引。它们不是重新生成原厂二进制的源码。

原始程序保持原SHA-256。本轮没有修改应用代码、设备电源、墙面、系统音量或注册表授权，也没有把用户正在修改的008功能产物纳入本次变更。

## 尚未完整闭环的范围

| 范围 | 还缺什么 |
| --- | --- |
| 实体画面与旧抓包 | 原抓包不可恢复；需要另一次有日期、软件版本、动作和画面对应的新证据 |
| 任意跨屏布局 | 整体业务裁剪、舍入、重叠、边缘、层级和不同源尺寸的现场结果 |
| 型号分支 | 部分61/62/63命令切换条件和尾部控制字段枚举 |
| 设备回读 | 所有查询／应答对、状态意义、版本和ACK覆盖范围 |
| 多点触摸、标注 | 完整字段差分、事件生命周期、全部图形枚举 |
| 报警、Dante、语音、软录制 | 具体服务端消息样本和完整参数／返回定义 |
| 工程固定记录 | 556字节中全部元数据的准确命名、发送侧生成和完整分包验证 |
| 媒体 | 现场流样本、序列回绕、其它音频路径和独立持续解码 |
| 扩展HTTP接口 | 若干代理／第三方接口的完整参数、服务端认证及实际返回 |

这些项有明确入口、模板或字段，但不能标为全部恢复。后续优先从当前需要接入的通道选择完整切片；新增功能仍按项目 Spec Kit 推进，并保留物理副作用和验证边界。
