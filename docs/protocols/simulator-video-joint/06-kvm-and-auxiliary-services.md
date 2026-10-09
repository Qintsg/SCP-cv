# KVM、标注与辅助服务

相关资料：[端口表](01-topology-and-lifecycle.md)、[媒体](05-media-and-local-ipc.md)、[成帧](03-device-framing-and-crypto.md)。

## KVM 预览与控制分别设置

`dllKVMTouch.dll`、`dllKVMMouse.dll` 和 `DLLPCToControl.dll` 导出媒体设置、`setUDPIp`、显示／适配窗口、播放、重播和销毁函数。触摸库还导出 `setAllResolution` 和 `PlayVideoChannel`。

`setMulticastIp` 设置预览来源；`setUDPIp` 设置输入事件的目标地址和端口。不能把预览输入的组播端口自动当作 KVM 控制端口。

## 鼠标短报文

触摸库 `0x6DCCAB50` 与 PC 控制库 `0x649878A0` 都构造：

```text
FC FD 06 01 buttonsState:u8 x:be16 y:be16 wheel:u8 DE
```

共 11 字节，没有设备窗口帧的两字节累加校验和。

坐标按预览区域缩放：`x*1920/previewWidth`、`y*1080/previewHeight`，使用整数除法。触摸库该入口会把负坐标钳为零；不同 DLL 的负数处理不能未经复核就统一。原预览区域宽高由右下点减左上点加一计算。

第四版执行原Qt事件入口并连接到原打包槽，12组对照全部一致，见 [输入事件验证](samples/device-input-events.json)。偏移4是按键状态，偏移9是滚轮方向，修正此前笼统的`event/button`名称。Qt信号是 `si_WriteUDPMousePos(int,int,int,int)`，宿主信号连接用显式调用原槽替代。

| 原Qt事件分支 | 线上按键状态／滚轮 |
| --- | --- |
| 按下2、双击4 | 左键1、右键2、中键4；已测其它键值8映射为0 |
| 释放3 | 状态清零 |
| 移动5 | 保留此前按下状态；已测按下→拖动→释放序列 |
| 滚轮31 | 状态固定1；delta>0写滚轮1，delta<=0写FF，包括delta=0 |
| 特殊事件82 | 先状态2、再状态0，共两帧；原代码每帧前Sleep(10) |

两个预览尺寸下的相同中心点都得到：

```text
FCFD06010103C0021C01DE
```

上述中心点例子的末字段1表示正向滚轮采样值；普通左键按下的滚轮字段是0。基础四组打包与新增事件分派分别核对，UDP调用都只在模拟器中记录。

## 键盘报文与状态

三个KVM库使用同一14字节格式：

```text
FC FD 09 05 00 modifier:u8 00 key[6] DE
```

`key[6]` 是当前六键槽，不是文本或一组Windows虚拟键。原转换函数的227个Qt键值在三个库中一致，见 [键值表](catalogs/device-keyboard-map.csv)；另独立核对37个常用键：A～Z对应4～29，1～9对应30～38，0对应39，空格对应44。其它映射的三库一致性不作为独立编码证明，也不擅自统一成完整标准HID表。

例如A按下为 `FCFD0905000000040000000000DE`，重复按下不另发帧，释放后六槽归零。原按下／释放函数已对照7组序列，包含重复抑制、六键上限、大小写、Shift标点和组合键。Ctrl+Alt+Esc路径会产生Delete槽值`4C`，释放行为也必须按原状态机复现，不能简单假定每次释放都发送空键表。详见 [键盘验证](samples/device-keyboard-verification.json)。

## 系统短报文

`dllKVMTouch:0x6DCCAF60`、`DLLPCToControl:0x64987BC0` 使用：

```text
FC FD 05 06 value:u8 07 80 04 38 DE
```

共 10 字节，后四字节固定表示 1920、1080 的大端数值。Qt 方法名为 `sl_WriteUDPSystem(int)`；不能仅因为它在 KVM 库里，就把 `value` 命名为键盘扫描码。

值 0、1、2、255 的四例与原 DLL 输出一致。尚未把这四个值赋予未经验证的业务枚举。

## 多点触摸

`dllKVMTouch` 的 `0x6DCC8F30`、`0x6DCC9700`、`0x6DCC9ED0` 使用相同的19字节格式：

| 偏移 | 内容 |
| --- | --- |
| 0～4 | `FC FD 0E 07 02` |
| 5、6 | 第一个触点：状态、序号 |
| 7、9 | 第一个触点x、y，各为小端u16 |
| 11、12 | 第二个触点：状态、序号 |
| 13、15 | 第二个触点x、y，各为小端u16 |
| 17 | 首包写 `min(pointCount,10)`；后续包写0 |
| 18 | `DE` |

状态：原Qt触点状态等于8时写6，其余写7。序号是本次列表位置加1，不是操作系统的原始触点ID。每包取两个点；单数最后一包的第二记录写零。空列表不发包，超过10点只取前10点。

坐标先按原入口的整数舍入转换，再计算 `x*4096/previewWidth`、`y*4096/previewHeight`，随后只写低16位。原18组扩展为30组，增加负坐标、正／负半像素及超出预览边界；全部与独立编码相同。Qt触点由纯内存适配提供，实际打包、截断和UDP长度使用原指令，见 [触摸验证](samples/touch-verification.json)。

传输长度是19，不应因为局部缓冲清了30字节，就发送30字节。原事件分派 `0x6DCCBEB0` 将事件194送至`8F30`、195送至`9700`、196或209经过Sleep(10)送至`9ED0`。这里恢复的是客户端分派条件；实际操作系统触摸生命周期与设备响应未运行验证。

## IPC 服务及录制文本

`CTcpIpcClient` 从 `IpcServerIp` 读取服务器，默认回环，端口 `8070`。`UseIpcServer` 与 `ConnectIpcServer` 决定使用／连接分支。服务涉及 RTSP 推送、输入节点识别和矩阵／音频属性。

另外，原软件构造以下 ASCII 录制控制：

```text
ipcam: record,chan={channel}&file={file}&url={url}
ipcam: stop,chan={channel}
ipcam: querry,chan={channel}
```

原拼写是 `querry`。还有 `stopRecord_`、`querry_` 等另一收发分支的字符串，不应统一改成 `query`。URL／文件名的转义规则及完整应答待相应服务样本确认。

IPC／ONVIF 代理读取 `IpcOnvifServerIp`，端口 `9996`。它是客户端到配套服务的连接，不等于客户端自行发送标准 ONVIF SOAP 的证明。

## 中控、远程输入与标注

`CUdpServerKey` 在 UDP `63430` 接收远程输入，解析器位于 `0x0066AE30`、`0x0066B000`、`0x0066C020` 等。它包含键／鼠标事件分派及来源标识，消息的 `2A 01` 长度不能自动当作设备帧 payload 长度。

`CTcpSocketPainter` 连接目标TCP3367（`0x00718890/0x00718940`），重新连接后写入以下标注格式：

| 构造入口 | 完整结构 | 长度 |
| --- | --- | --- |
| `0x0065E3E0` | `EA EB 01 red:u8 green:u8 blue:u8 alpha:u8 FC FD` | 9 |
| `0x0065E450` | `EA EB 02 lineWidth:u8 FC FD` | 6 |
| `0x0065E4B0` | `EA EB 03 shapeType:u8 x:be16 y:be16 FC FD` | 10 |
| `0x0065E560` | `EA EB 04 shapeType:u8 x:be16 y:be16 FC FD` | 10 |
| `0x0065E610` | `EA EB 05 shapeType:u8 x:be16 y:be16 FC FD` | 10 |
| `0x0065E6C0` | `EA EB 06 action:u8 FC FD` | 6 |
| `0x0065E720` | `EA EB 07 length:be16 textUtf8[length] FC FD` | 正常为7+内容长度 |
| `0x0067E1B0` | `EA EB 08 length:be16 pngBytes[length] FC FD` | 正常为7+内容长度 |

颜色由 `QColor::red/green/blue/alpha` 逐项传入。线宽发送层 `0x00718BB0` 将UI值1→3、3→10、5→15。短帧03是起点，04是终点，05是移动／重放坐标，首个参数来自图形类型。真实按钮槽经过共用跳板后重新定位，不能把全部按钮都归给旧元数据的同一个跳板地址。

| shapeType | 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8 | 9 | 10 |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| 原客户端工具 | 矩形 | 椭圆 | 箭头 | 线 | 三角旗 | 红旗 | 文本 | 自定义图片 | 圆 | 橡皮 |

撤销`0x00575380`→动作0，重做`0x00575B20`→1，清空`0x005757E0`→2，经`0x00719160`发送06帧。文本发送入口`0x007194D0`使用07；图片入口`0x007192B0`使用08。完整分派证据见 [设备槽入口](catalogs/device-operation-dispatch.json)。

07对QString执行`toUtf8`，长度是UTF-8字节数，嵌入NUL也保留。08读取图片路径，调用QImage加载后以`save(buffer,"png",-1)`获得PNG编码结果，再包装字节。空文本／空路径返回空QByteArray；非空路径编码失败仍包装零长度内容。两个入口均只把内容长度低16位放入头部，却追加完整内容，65536字节时声明长度回绕为0。13组独立比较通过，见 [标注变长验证](samples/painter-payload-verification.json)；图片编码结果为注入值，不代表真实图片加载、编码或设备渲染已验证。

状态同步是另一条JSON路径，包含 `MarkItem`、`Type`、`WidthLine`、`Color`、`PressPoint`、`EndPoint`、`Undo`、`Redo`。上述短帧枚举来自实际按钮与发送调用链，不是按JSON字段名猜测。此前把 `65F680～65FD60` 归为标注的结论错误，已更正为下一节的Dante。

## Dante、报警、语音和软录制

| 服务 | 已确认的消息／配置 |
| --- | --- |
| Dante | UDP5062；下文给出8字节格式 |
| 设备信息 | `DevInfoServerIp`、TCP4850，周期计时器10000毫秒；连接调用见 `0x00717500` |
| CVS 控制服务 | `CvsControlServerIp`、TCP9898；存在 `WindowStartPolling`、`WindowStopPolling`、`Operate`、`JointId`、`WindowId`、`UserId`；本版 `0x0071A330` 接收槽直接返回，不构造不存在的ACK |
| 报警 | UDP 8932 接收；解析出 `Open`、`Close` 和恢复／满屏置顶相关事件 |
| 摄像机辅助 | `CUdpSocketHK` 的 UDP 9998 发送；完整厂商事件语义未确认 |
| 音频识别 | `AudioRecServerIp`、TCP8099；具体 `FAFB2200/01/02` 解析见09 |
| 软录制 | UDP9888发送、9889接收；原接收器先原样转发QByteArray，再由录制界面解析 |

Dante 状态 JSON 字段包括：`DanteInputIp`、`DanteInputIndex`、`DanteOutputIp`、`DanteOutputIndex`、`DanteInputValue`、`DanteOutputValue`、`DanteDelayTime`、`DanteInputMute`、`DanteOutputMute`、`ContainsDante`、`MixArr`。

## Dante短帧

所有下列构造均返回8字节，格式为：

```text
A5 AB family:u8 opcode:u8 payload[3] checksum:u8
checksum = sum(packet[2:7]) & 0xFF
```

| 入口 | family／opcode | payload |
| --- | --- | --- |
| `65F680/65F710/65F7A0` | `02/31`、`02/32`、`02/33` | 两个u8参数，再填0 |
| `65F830` | `02/34` | 三个u8参数 |
| `65F8C0` | `02/37` | u8参数、be16参数 |
| `65F960/65F9F0/65FA80` | `03/31`、`03/32`、`03/33` | 两个u8参数，再填0 |
| `65FB10` | `03/34` | 三个u8参数 |
| `65FBA0` | `03/37` | u8参数、be16参数 |
| `65FC40/65FCD0` | `04/35`、`04/36` | 两个u8参数，再填0 |
| `65FD60` | `0D/02` | u8参数、be16参数 |

family与opcode是已还原的线上字节，不把未命名硬件枚举猜成某种厂商寄存器。`0x00717E80/0x00718330` 在调用两条37构造前，将第二个业务值减7200；直接构造层接收的已经是转换后的值。13个入口各核对四组零、一般、u8边界和u16边界值，全部52组相同。

第四版沿 `0x00522F90` 的滑杆／静音操作、`0x00522B70` 的选择及 `0x006F61F0` 的具名字段序列化，恢复以下业务含义：

| family／opcode | 客户端业务 | payload来源 |
| --- | --- | --- |
| `02/31`、`02/32` | 输入解除静音／静音 | 输入通道重复两次，再填0 |
| `03/31`、`03/32` | 输出解除静音／静音 | 输出通道重复两次，再填0 |
| `02/37`、`03/37` | 输入／输出音量值 | 通道u8，`(Value-7200)&FFFF`大端 |
| `0D/02` | 输出延迟值 | 输出通道u8，DelayTime大端u16 |
| `04/35`、`04/36` | 连接／断开输入到输出的音频路由 | 输入通道、输出通道、0；调用`718420/718510` |

已定位UI计算：音量业务值为整数表达式`((x-90)*8400)/236`，延迟业务值为`((x-90)*2000)/236`。本版本没有由这些表达式证明固件的dB或时间单位；`02/33/34`、`03/33/34`仍按混音相关候选保留参数，不补造通道寄存器定义。

录制另一接收分支 `0x004C5CD0` 要求下划线分割后恰好四段，首段为原拼写 `querry`，后三段转整数。它与TCP录制返回的 `&`／`=` 分割路径不同，不宜统一改名或统一分割器。客户端各分支的字段访问来自代码，服务端含义与实现按本次范围排除。

## 报警文本与摄像机代理

UDP8932的 `0x0071D650` 将数据按fromAscii转换，按**单个空格**分割且要求恰好四段：第一段转整数，第二段在此字段提取步骤没有读取，第三段是目标／墙面标识，第四段按区分大小写的 `Open/Close` 分支处理。Open路径查找目标并进入恢复／满屏或窗口动作，Close路径进入相应关闭／恢复处理；对象不存在时不等同于成功执行。多余或连续空格会改变分段数量，不能任意规范化成四段。

`CUdpSocketHK:0x0071FC60` 的发送载荷是Latin1文本：

```text
{QString参数2}_{十进制参数1}:{十进制参数3}
```

目标来自设置对象 `+0x14C` 保存的地址，端口9998；接收端的厂商含义不在本次客户端范围内。这里已恢复字段次序、下划线、冒号、转换和端口，没有把它写成标准ONVIF消息。

UDP远程输入模块还通过 `0x007134A0/00713510/00713580` 构造回送消息 `FAFA3101/3102/3103`：14字节，payload是两个**独立u8**，校验位置零，尾 `FBFB`。对应构造入口为 `65F4D0/65F560/65F5F0`，不要把这两个参数误作一个u16摄像机通道。

语音识别还发现 `https://openapi.baidu.com/oauth/2.0/token` 与 `http://vop.baidu.com/server_api`，字段含 `format`、`rate`、`channel`、`token`、`lan`、`cuid`、`speech`、`len` 和 `result`。这是对第三方 API 的调用，尚未验证本版本的账号配置与平台当前兼容性；不提交应用密钥。

本轮未对辅助服务发送报警、KVM、录制、语音或设备写入请求。这里区分已恢复格式与仅定位到入口的路径，接入时优先选择证据明确的消息。
