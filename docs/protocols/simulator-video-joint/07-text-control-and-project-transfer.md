# 文本控制、状态同步与工程传输

相关资料：[拓扑](01-topology-and-lifecycle.md)、[HTTP 状态资料](02-http-and-websocket.md)、[成帧](03-device-framing-and-crypto.md)。

## CT 文本控制是另一套设备协议

`CMacCodeMatrix` 与 CT 界面使用多种 ASCII／Latin-1 文本命令。它们不是把 `FB FC` 二进制帧转成十六进制字符串。命令之间还存在设备家族差别，不应统一追加 CRLF、分号或校验。

`0x007A2E80` 构造 `<Protocol-Type>`；同一代码区还能看到 `<Type>`、`<Ver>`、`/^Protocol;` 等查询。客户端先识别协议／型号，再选择相应命令路径。

原函数返回需要发送的字节数，按该长度复制／发送，不是把缓冲区的全部容量发出。参数化模板通过 QString 的十进制格式化或字符串拼接生成，之后转为 Latin-1。原拼写 `Vedio` 也必须保留。

## 文本命令分组

| 业务组 | 原模板示例 |
| --- | --- |
| 类型与开关 | `<Protocol-Type>`、`<Type>`、`<Ver>`、`/:MessageOff;`、`/:MessageOn;`、`/%Lock;`、`/%Unlock;` |
| 端口与分辨率 | `<ResIn><ResOut>`、`<ScanPortAll>`、`<ResIn,…>`、`<ReadInType,…>`、`<ReadOutType,…>` |
| 温度 | `<Temp,In,All>`、`<Temp,Out,All>`、`<Temp,In,…>`、`<Temp,Out,…>`、`FanTemp` |
| 默认与音频 | `<Defaults>`、`DefaultIn!`、`DefaultOut!`、`AudioA!`、`AudioD!`、`AudioAOut!`、`AudioDOut!` |
| 网络设置 | `<^SPORT>`、`<^SIPR>`、`<^SUBR>`、`<^GAR>`、`<#SPORT…>`、`<#SIPR…>`、`<#NETDEFAULT>`；还有 `HS` 家族 |
| 矩阵切换／预案 | `<VS,%1,%2>`、`Status%1.`、`Save%1.`、`Recall%1.`、`Clear%1.`、`$%1->`、`$All->` |
| 墙面参数 | `<MAP,W,%1,%2,%3>`、`<SIZE,W,%1,%2,%3>`、`<VIR,W,%1,%2,%3>`、`<MARGIN,W,%1,%2,%3>` |
| 窗口 | `<WIN,W,%1,%2,%3,%4,%5,%6,%7,%8>`、`<MOVE,%1,%2,%3,%4>`、`<RESIZE,%1,%2,%3,%4,%5,%6>`、`<LAYER,%1,%2,%3>`、`<CLOSE,%1,%2>` |
| 输入／输出图像 | `<#VedioSetIn…>`、`<#VedioGetIn…>`、`<#VedioSetOut…>`、`SetVGAIn%1.`、`Bright%1.`、`Contrast%1.`、`Sharp%1.` 等 |
| 字幕 | `<CharColor,W,…>`、`<CharPos,W,…>`、`<CharSize,W,…>`、`<CharDisplay,W,…>`、`<#CharInfo…>` |
| 摄像机方向 | `TurnLeft.`、`TurnRight.`、`TurnUp.`、`TurnDown.`、`TurnStop.` |
| 下载／升级 | `DownLoadCode.`、`DownLoadIn`、`DownLoadOut`、`DownLoadFPGAIn`、`UpdateEDID`、`UpdateEnd.` 等 |

历史静态模板保存在 [text-commands.csv](catalogs/text-commands.csv)。第四版另对106个设备文本构造入口执行原指令及参数变体采样，全部正常返回，完整字节、实参及变体见 [device-text-builders.csv](catalogs/device-text-builders.csv) 和 [采样结果](samples/device-text-probes.json)。另有27组独立完整字节比较；采样完成不等于设备语义或全部型号枚举已经证明。

占位符是格式化顺序，不能无条件当作C函数参数顺序。Qt5.4的`arg`每次替换最低编号的全部剩余占位符，依据[对应源码](https://raw.githubusercontent.com/qt/qtbase/v5.4.0/src/corelib/tools/qstring.cpp)。以下是容易接错的原构造行为，参数编号包括输出缓冲区`param_1`：

| 原入口 | 完整构造／关键行为 |
| --- | --- |
| `0x007ADA00` | `<MS,param_4,param_2,param_3>`，有参数重排 |
| `0x007ACA40` | `<WIN,W,param_2,…,param_9>`；位置`param_6/7`按有符号十进制，其它已测值零扩展 |
| `0x007ABA60` | `$param_2{输入制式}Out!`；选择0/1/2/3对应VGA/YUV/YC/CV，其它返回空制式串 |
| `0x007AC460` | `<Res,param_2,枚举>`；`1920x1080x60Hz`映射17，未识别字符串映射0 |
| `0x007B3990` | `<#LocalSHARa.b.c.d.e.f>`，六段十进制点分MAC参数，不能改成十六进制冒号格式 |
| `0x007B7D90/7B7F50` | `<MapSend,param_2,param_3,1/2>` |
| `0x007B8110` | `<PreviewCH,param_2>` |

字符串拼接字段没有发现逗号、尖括号、分号或`%2`的转义步骤，合成输入会原样参与输出；嵌入NUL也包含在返回长度中。相关独立样例见 [文本验证](samples/device-text-verification.json)。本组额外采样不作为全Unicode的真实Qt ABI验证。

**`0x007ADC50`存在长度异常：只写入`<Ack>`的5字节，却返回12。** 后7字节没有由构造器初始化，当前采样缓冲清零才显示零值；不能将零尾巴写成固定协议，也不能据此宣称发送缓冲安全。

非零缓冲哨兵例再次确认此行为：预置A5后，原函数返回`<Ack>`加7个A5，而非7个零。输入制式选择0～3及无效99的完整终止符也纳入独立比较。

部分静态常量只是前缀，例如 `<#SPORT`，后续函数会追加端口和终止符。只找到前缀不等于已经得到完整可发送命令。

## 整数列表与有效尺寸

六个整数列表入口的原循环及输出与独立预期比较，另加5个有效尺寸例，共29组通过，见 [列表验证](samples/ct-list-verification.json)。

| 原入口 | 非空列表完整形式；index=11、列表=[1,4,7] |
| --- | --- |
| `0x007C8230` | `11R1,4,7.` |
| `0x007C84C0` | `11S1,4,7.` |
| `0x007C8750` | `11Q1,4,7.` |
| `0x007C89E0` | `11F1,4,7.` |
| `0x007C8C70` | `11T1,4,7.` |
| `0x007C8F00` | `<VS,11,1,4,7>` |

列表整数按零扩展值格式化，合成`-1`输出4294967295。原构造在追加后无条件删除末字符，因此空列表前五个入口都输出`11.`，VS入口输出`<VS,11>`；没有设备接受这些空命令的证据。R/S/Q/F/T的固件业务定义不由字母补造。

`0x007C4930`以小写`x`拆分尺寸字符串，恰好两段才构造`<ValidSize,W,index,width,height>`。空串、大写X或三段尺寸返回0；数字转换失败的段变为0，例如`ax1080`生成`<ValidSize,W,11,0,1080>`。这不是输入有效性的保证。

## 分辨率枚举不能混用

`0x007A2A20`与`0x007C1620`是两套辅助表。同一个`1920x1080x60Hz`，前者返回17，后者返回21。前者按完整、区分大小写的字符串匹配；后者的部分路径按包含子串匹配并归一化输入，例如`prefix1920x1080x60Hzsuffix`归一化为`1920x1080x60Hz`并返回21，而前者返回0。

后者接受`0800x0600x60Hz`后返回3，并改写为`800x600x60Hz`；不能将这个归一化输出直接当作该辅助表的等价输入。原常量及未知值共74个采样，5项与静态明确的独立预期一致，见 [枚举表](catalogs/device-resolution-enums.csv) 与 [采样证据](samples/resolution-enum-probes.json)。Qt翻译分支采用英文源文回退，未加载本机翻译目录；枚举表不代表设备允许的输出尺寸全集。

## CT 工程资料与图像流

另有下列具有斜杠字段分隔的文本：

```text
<#PJWall/…>   <#DelPJWall/…>
<#JZPlan/…>   <#DelJZPlan/…>
<#PJPlan/…>   <#WinName/…>   <#DelWinName/…>
<#DeviceMsg/…> <#DeviceName/…> <#OutputMsg/…>
```

图片上传原入口`0x00807990`实际拼接以下单次TCP写入内容：

```text
<Image_start><Wall_num{wall}><Image_num{image}><Pic_size1920,1080>
rawContent[QByteArray实际长度]
<Image_end>
```

这里的换行仅用于展示，线上没有新增换行。第三个整数参数在此包装中没有进入内容；尺寸文字固定1920,1080，不能据此证明传入图片的实际像素尺寸。

标题入口`0x00807D70`拼接：

```text
<Title_start><Wall_num{wall}><Pic_size{width},{height}><Image_len{declaredLength}>
rawContent[QByteArray实际长度]
<Title_end><Wall_num{wall}><Pic_size{width},{height}>
```

`declaredLength`来自独立整数实参，没有用它裁切、填充或验证QByteArray。两入口通过`0x007FCC70`发送；9组原包装及写入参数与独立编码相同，覆盖空内容、嵌入NUL／终止标记、256字节、65536字节及标题声明长度不一致。见 [上传包装验证](samples/ct-upload-verification.json)。

包装里的二进制内容原样保留，没有在这两个入口执行编码、转义或分段确认。内容生产者使用的图像编码、设备接收解析及完成确认仍缺证据；它们与06的`EAEB08`标注PNG包装是两套协议，不能混用。

## XML-RPC

原软件存在 `http://{host}:{port}/RPC2` 的访问路径，构造器在 `0x007A2820`，网络调用在 `0x0079EE30` 附近。

请求包括 XML `methodCall`、`methodName`、`params`、`param` 节点；参数由 QVariant 值序列化。客户端设置过 `Content-Type: application/x-www-form-urlencoded` 和 Basic `Authorization`。

这个 Content-Type 是原实现的实际值，不能在“忠实复现”时未经验证改为另一类型。方法名及参数来自操作路径／配置；服务端支持的完整方法表和默认认证要求尚未实测。

## 状态同步的长度前缀 JSON

`CTcpRecvFile` 默认连接 `mediaServerIp:9997`。发送函数 `0x00701490` 先生成 JSON UTF-8 字节，再把字节数以本机四字节整数放在前面：

```text
json_byte_length:le32 json_utf8[json_byte_length]
```

目标为 Windows x86，本机四字节整数在这里是小端。它与设备帧的大端长度不同，也不是 Qt `QDataStream` 的字符串对象编码。

接收函数 `0x00701D60` 保留总长度、剩余长度和累计 QByteArray，累积完整内容后写入当前资料缓存并触发解析。一次 read 可以包含不完整消息或更多数据，不应按一次网络 read 直接 decode JSON。

这里的四字节长度和工程接收协议的固定记录不是同一协议。`IsUseNoServer` 等分支还允许使用本地 `Cur.Neutral` 资料路径。

## 状态 JSON 的业务块

| 块／字段 | 内容 |
| --- | --- |
| `VideoJoint`／`Windows` | 墙面与窗口的资料 |
| `Matrix` | 视频输出、源地址、端口和通道选择 |
| `Audio` | 音频输出与输入选择 |
| `Mark`／`MarkItem` | 标注对象及撤销、重做资料 |
| `BindInvoke` | 联动／绑定资料 |
| `BindList`、`Polling` | 绑定与轮询 |

窗口记录包含 `WindowLevel`、`WindowSign`、`GroupIp`、`GroupPort`、`IndexCode`、`Name`、`ResolutionW`、`ResolutionH`、`Model`、`Is1G`、`ContainsAudio`、`IsUseDHAlarm`、`DHOldX/Y/W/H`、`IsBind`、`BindIndex`、`BindGroupName`、`ScreenRow`、`ScreenColumn`、`ScreenIndex` 等。

矩阵相关字段包括 `FourScreen`、`GroupIP`、`GroupPort`、`ChannelNumber`、`InputChannelNumber`、`InputIP`、`port`。字段大小写并不统一，例如 `GroupIP` 与 `GroupIp` 同时存在，必须按所属消息处理。

这类 JSON 是控制端之间的状态资料。收到、缓存或更新它，不自动证明输出节点已执行设备控制包。

## 工程发现

UDP 服务 `0x0077DD60` 监听 `9999`。接收函数检测报文是否包含 `IsCreatorDev`，并向发送方的地址与端口回复 ASCII `TouchPanel`。

这只是发现应答，不包含本轮已确认的账号认证。发现成功也不证明随后的 TCP 工程接收会成功。

## 工程文件接收

`0x0077E040` 监听 TCP `20202`。已从遗漏的虚函数入口 `0x0077E920` 恢复工程接收逻辑，包含：

```text
PC_Down{十进制文件数量}
  → TP_ReadyToRecv
目录信息
  → TP_RecvDirOk
固定文件信息记录与文件内容
  → TP_FilePlus
全部文件接收完成
  → TP_Finish
```

固定文件信息读取长度为 `0x22C`，即 556 字节。原实现对目录／启动文本使用另一个较大的读取缓冲，不能把它当成 JSON 帧。

已定位到记录中的：

| 相对记录起点的偏移 | 内容 | 证据 |
| --- | --- | --- |
| 0～255 | 一个本地编码的路径／目录文本区 | 字符串转换及路径处理 |
| 288～291 | 文件内容长度的低 32 位，小端 | 后续剩余长度与累计写入判断 |
| 300～555 | 另一个本地编码的文件名文本区 | Qt 本地编码转换及路径组合 |
| 256～287 | 32字节不解释的元数据区 | 当前接收代码不读取具体字段 |
| 292～299 | 8字节不解释的元数据区 | 当前接收代码不读取具体字段 |

文件内容按声明长度累计接收、写入。文件名转换使用 `QTextCodec::codecForLocale`；两段256字节文本区与剩余40字节元数据的边界已明确。客户端只消费路径、文件名和低32位长度，未使用部分按不解释字节保留，缺少发送程序时不补造文件时间／属性字段名。

固定记录用一次 `QIODevice::read(...,556)` 读取，未看到将不足556字节的记录重新聚齐的检查；接收内容则另有累计长度。此原实现不构成“TCP永远一次读到完整556字节”的保证。文件长度在后续代码中以有符号int参与判断，大文件边界没有实际传输验证。

旧逻辑包含 `touchctrl.db` 的临时改名、替换，以及 `Pic/`、`^Pic/` 的目录操作；还保留 `/mnt/sdcard/TP_Tmp/` 这一触控面板路径。它与本机客户端的 `C:/CVS/VideoJoint` 配置目录不能混用。

本轮没有执行发现、上传或工程替换，以免改变用户现有资料。

## 可配置中控命令

嵌入触控工程的 SQLite 结构还包含：

- `SystemInfo`：地址、远端端口、协议选择、连接与参数。
- `Command`：`cm_Value` 和所属系统。
- `Macro_Command`：宏中的命令顺序与延迟。
- `LogicObject`、`LogicParameter`：逻辑对象与参数。

TCP／UDP 命令值可由这些工程数据配置，软件因此也能发送不属于内置协议表的字节。当前附带数据库的上述系统和命令表为空，不能据空表声称某种电源／串口厂商协议已经恢复。项目已有 `8889` 电源控制来源仍应单独对照其历史代码和设备配置。
