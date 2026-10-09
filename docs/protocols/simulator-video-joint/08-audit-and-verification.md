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
| 第二标识、A/B/C控制字段 | 创建调用链及完整业务13组对照；50屏场景A=1，B/C递增至50 | 固定9/1/1不适用于任意布局；统一节点测试不等于现场画面证明 |
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
| 多点触摸 | 30／30 | 三个原入口，数量／截断与19字节长度；新增负数、半像素和越界坐标 |
| Dante与标注短帧 | 58／58 | 13个Dante入口各4组，加6个标注短帧 |
| 完整跨屏业务 | 13／13 | 原591E50，最多50节点、部分相交、裁剪、分屏、备用输入及SendType2/3逐节点顺序 |
| 跨屏配置／混合尺寸 | 9／9 | 同步裁剪、IGMPv3、分类端口、6120完整字节及一个混合尺寸场景 |
| 鼠标事件分派 | 12／12 | 原Qt事件→原打包槽；包括拖动状态、滚轮及特殊事件82 |
| 键盘 | 227键三库一致；37常用键独立一致；7序列三库一致 | 按下、释放、重复抑制、六键上限与组合状态；一致性与独立证明分开 |
| CT文本 | 106入口完成采样；27独立例相同 | 参数变体定位字节，包含MS重排、制式选择、MAC十进制表示、NUL及Ack非零哨兵 |
| CT分辨率辅助表 | 两套表74个采样完成；5项独立预期相同 | 具名尺寸枚举、归一化、未知／大小写及子串匹配；英文翻译回退 |
| CT列表／有效尺寸 | 29／29 | 六个原列表循环、空列表、无符号值，以及尺寸字符串边界 |
| CT图片／标题包装 | 9／9 | 完整写入参数，包括65536字节内容与独立声明长度不一致 |
| CT图片分段 | 16／16 | 两条原1024字节循环，末块和空内容；另一分支每块50毫秒等待仅截取 |
| 设备维护 | 6构造及18参数变体全部相同 | 三种原始以太网与三种TCP维护构造；网卡及写入被拦截 |
| 标注变长包装 | 13／13 | UTF-8文本、NUL、16位长度边界；QImage编码结果注入，未测试图像编解码 |
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

## 第四版的设备分支补充

原Qt元调用中多个按钮共用跳板，旧标签曾将所有标注操作写成相同地址。第四版沿原分派指令跨过紧邻跳板，停在实际业务调用之前，恢复43个槽；主表不把旧跳板名称继续当作每个按钮的业务入口。131个旧设备构造中100个发现直接引用，31个未发现直接引用；没有直接引用不等于代码不可达，更不能由此删除协议候选。

重要原行为单独保留：维护配置实际102字节但内部长度字段仍004C；CT `<Ack>`仅初始化5字节却返回12；标注超过65535字节声明长度回绕，仍拼接完整内容；标题上传不比较声明长度与内容长度。设备是否接受这些异常、或将其判为错误，没有在实体设备上验证。

新增证据不改变旧400黄金包的编码一致性结论，也不把旧抓包视为已经恢复。客户端业务参数选择、网络消息编码和实机完成状态分别保留证据等级。

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
uv run --offline --default-index https://pypi.org/simple --with pefile --with capstone --with unicorn --with pycryptodome python E:/Projects/SSPU/Resources/SimulatorVideoJoint-analysis/verify_device_layout.py
uv run --offline --default-index https://pypi.org/simple --with pefile --with capstone --with unicorn --with pycryptodome python E:/Projects/SSPU/Resources/SimulatorVideoJoint-analysis/verify_device_keyboard.py
uv run --offline --default-index https://pypi.org/simple --with pefile --with capstone --with unicorn --with pycryptodome python E:/Projects/SSPU/Resources/SimulatorVideoJoint-analysis/verify_device_input_events.py
uv run --offline --default-index https://pypi.org/simple --with pefile --with capstone --with unicorn --with pycryptodome python E:/Projects/SSPU/Resources/SimulatorVideoJoint-analysis/verify_device_text.py
uv run --offline --default-index https://pypi.org/simple --with pefile --with capstone --with unicorn --with pycryptodome python E:/Projects/SSPU/Resources/SimulatorVideoJoint-analysis/verify_device_maintenance.py
uv run --offline --default-index https://pypi.org/simple --with pefile --with capstone --with unicorn --with pycryptodome python E:/Projects/SSPU/Resources/SimulatorVideoJoint-analysis/verify_painter_payloads.py
uv run --offline --default-index https://pypi.org/simple --with pefile --with capstone --with unicorn --with pycryptodome python E:/Projects/SSPU/Resources/SimulatorVideoJoint-analysis/verify_ct_lists.py
uv run --offline --default-index https://pypi.org/simple --with pefile --with capstone --with unicorn --with pycryptodome python E:/Projects/SSPU/Resources/SimulatorVideoJoint-analysis/verify_ct_uploads.py
uv run --offline --default-index https://pypi.org/simple --with pefile --with capstone --with unicorn --with pycryptodome python E:/Projects/SSPU/Resources/SimulatorVideoJoint-analysis/verify_layout_branches.py
uv run --offline --default-index https://pypi.org/simple --with pefile --with capstone --with unicorn --with pycryptodome python E:/Projects/SSPU/Resources/SimulatorVideoJoint-analysis/probe_resolution_enums.py
uv run --offline --default-index https://pypi.org/simple --with pefile --with capstone --with unicorn --with pycryptodome python E:/Projects/SSPU/Resources/SimulatorVideoJoint-analysis/verify_ct_chunks.py
```

仓库目录的 `samples/` 保存可版本管理的测试向量与摘要，`catalogs/` 保存命令和函数索引。它们不是重新生成原厂二进制的源码。

原始程序保持原SHA-256。本轮没有修改应用代码、设备电源、墙面、系统音量或注册表授权，也没有把用户正在修改的008功能产物纳入本次变更。

`--offline` 要求uv缓存已具备相应依赖。完整脚本及所有调用／反编译证据保留在本地分析目录。QString失败数值转换的0返回与有限适配依据也参照了[Qt文档](https://doc.qt.io/archives/qt-5.15/qstring.html#toInt)；宿主适配不代表加载实际Qt ABI验证。

## 范围与剩余边界

用户最新范围只继续实际设备相关客户端逻辑。下表不将仅本地资料、仅软件与服务端的处理及服务端实现计入后续设备逆向；此前章节保留历史记录。

| 范围 | 还缺什么 |
| --- | --- |
| 实体画面与旧抓包 | 原抓包不可恢复；需要另一次有日期、软件版本、动作和画面对应的新证据 |
| 任意跨屏布局 | 完整业务13组加配置／混合尺寸9组已比较；更多混合行列、拼缝、不能整除的源尺寸及其它输入／型号选择仍缺独立对照 |
| 固件／型号 | 构造及解析字段已索引；客户端没有名字的设备枚举和固件内部约束无法仅凭本版本确认 |
| 设备回读 | 34类固定及8条扩展路径已核对；不代表每个写操作都有完成ACK |
| KVM与标注 | 基础及状态分派已补充；系统短帧value尚无具名调用枚举，操作系统实际生命周期及设备图形呈现未实测 |
| 设备维护 | 6种构造完成对照；原始发现响应的完整结构及部分控制选择值只有静态消费证据 |
| 设备升级 | 客户端TFTP启动／61FD触发已恢复；固件侧文件请求、校验、分区和成功确认不在所给程序中 |
| CT图像／工程命令 | 文本、列表、上传包装及分段已比较；原专用接收槽没有完成ACK解析；图像内容生产者、设备解析及R/S/Q/F/T业务定义尚需关联证据 |
| 媒体 | 分类、序号、请求与共享内存读写已记录；现场流持续解码、声卡与采样格式未验证 |
| 自定义设备命令 | 已恢复宏与原样发送路径；示例工程没有实际电源／网关的自定义命令内容，不能补造现场协议 |
| 软件本地与纯服务端交互 | 按用户最新要求排除继续调查；旧资料保留，不作为当前未完成设备工作 |

本轮是逆向调查和文档，没有进入SCP-cv功能实现，也没有改变当前008功能指针。新增接入仍按项目Spec Kit推进。已导出的客户端函数及恢复候选不表示所有UI函数和第三方库都逐函数完成语义恢复；本次交付以设备通信及其客户端业务来源为范围。
