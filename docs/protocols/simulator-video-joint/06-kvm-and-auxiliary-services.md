# KVM、标注与辅助服务

相关资料：[端口表](01-topology-and-lifecycle.md)、[媒体](05-media-and-local-ipc.md)、[成帧](03-device-framing-and-crypto.md)。

## KVM 预览与控制分别设置

`dllKVMTouch.dll`、`dllKVMMouse.dll` 和 `DLLPCToControl.dll` 导出媒体设置、`setUDPIp`、显示／适配窗口、播放、重播和销毁函数。触摸库还导出 `setAllResolution` 和 `PlayVideoChannel`。

`setMulticastIp` 设置预览来源；`setUDPIp` 设置输入事件的目标地址和端口。不能把预览输入的组播端口自动当作 KVM 控制端口。

## 鼠标短报文

触摸库 `0x6DCCAB50` 与 PC 控制库 `0x649878A0` 都构造：

```text
FC FD 06 01 event:u8 x:be16 y:be16 button:u8 DE
```

共 11 字节，没有设备窗口帧的两字节累加校验和。

坐标按预览区域缩放：`x*1920/previewWidth`、`y*1080/previewHeight`，使用整数除法。触摸库该入口会把负坐标钳为零；不同 DLL 的负数处理不能未经复核就统一。原预览区域宽高由右下点减左上点加一计算。

`event` 和 `button` 来自调用方，不把它们直接解释为 Windows 原始消息码。Qt 信号是 `si_WriteUDPMousePos(int,int,int,int)`；具体枚举与事件入口相关。

两个预览尺寸下的相同中心点都得到：

```text
FCFD06010103C0021C01DE
```

四个鼠标参数例子与原 DLL 截取的 UDP 字节相同。UDP 调用只在模拟器中记录，没有真正发送。

## 系统短报文

`dllKVMTouch:0x6DCCAF60`、`DLLPCToControl:0x64987BC0` 使用：

```text
FC FD 05 06 value:u8 07 80 04 38 DE
```

共 10 字节，后四字节固定表示 1920、1080 的大端数值。Qt 方法名为 `sl_WriteUDPSystem(int)`；不能仅因为它在 KVM 库里，就把 `value` 命名为键盘扫描码。

值 0、1、2、255 的四例与原 DLL 输出一致。尚未把这四个值赋予未经验证的业务枚举。

## 多点触摸

`dllKVMTouch` 的 `0x6DCC8F30`、`0x6DCC9700`、`0x6DCC9ED0` 构造另一种固定长度触摸数据。已静态定位：

- 开头为 `FC FD 0E 07 02`。
- 坐标转换到 4096 单位的范围，按预览区域尺寸缩放。
- 每条记录含状态、触点编号、x、y；坐标以低字节在前的顺序写出。
- 每次最多处理 10 个触点，每包组合两个记录；首包另携带触点数量。
- 状态值由 Qt touch point 状态生成，释放与其它状态写不同数值。
- 包内有 `DE` 标记与剩余填零字段，不能套鼠标 11 字节布局。

具体记录偏移及完整多点事件生命周期尚未做与独立编码器的差分，本节保持静态证据等级。不要因鼠标八组验证通过，就把触摸路径也标为验证通过。

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

`CTcpSocketPainter`／标注界面还使用 `0x0065F680`～`0x0065FD60` 的控制构造；清除、形状、颜色、线宽、撤销和重做可从调用链定位。状态资料同步中存在 `MarkItem`、`Type`、`WidthLine`、`Color`、`PressPoint`、`EndPoint`、`Undo`、`Redo`。

这是已确认的标注业务与字段；全部图形类型枚举、坐标系和数据字段仍未逐项建立独立编码验证。统一索引保留相关函数和模板。

## Dante、报警、语音和软录制

| 服务 | 已确认的消息／配置 |
| --- | --- |
| Dante | UDP 发送类保存端口 `5062`；状态字段见下表 |
| 设备信息 | `DevInfoServerIp`，周期计时器 10000 毫秒；具体消息参数待确认 |
| CVS 控制服务 | `CvsControlServerIp`；存在 `WindowStartPolling`、`WindowStopPolling`、`Operate`、`JointId`、`WindowId`、`UserId` 相关消息 |
| 报警 | UDP 8932 接收；解析出 `Open`、`Close` 和恢复／满屏置顶相关事件 |
| 摄像机辅助 | `CUdpSocketHK` 的 UDP 9998 发送；完整厂商事件语义未确认 |
| 音频识别 | `AudioRecServerIp`；存在四段字符串解析、唤醒检查和命令分派 |
| 软录制 | UDP 9888 发送、9889 接收；返回负载的全部字段待确认 |

Dante 状态 JSON 字段包括：`DanteInputIp`、`DanteInputIndex`、`DanteOutputIp`、`DanteOutputIndex`、`DanteInputValue`、`DanteOutputValue`、`DanteDelayTime`、`DanteInputMute`、`DanteOutputMute`、`ContainsDante`、`MixArr`。

语音识别还发现 `https://openapi.baidu.com/oauth/2.0/token` 与 `http://vop.baidu.com/server_api`，字段含 `format`、`rate`、`channel`、`token`、`lan`、`cuid`、`speech`、`len` 和 `result`。这是对第三方 API 的调用，尚未验证本版本的账号配置与平台当前兼容性；不提交应用密钥。

本轮未对辅助服务发送报警、KVM、录制、语音或设备写入请求。这里区分已恢复格式与仅定位到入口的路径，接入时优先选择证据明确的消息。
