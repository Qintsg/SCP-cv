# HTTP 与 WebSocket

相关资料：[拓扑](01-topology-and-lifecycle.md)、[OpenAPI](contracts/http-openapi.yaml)、[状态资料](07-text-control-and-project-transfer.md)。

## 管理 HTTP 是同一路径上的请求分派

客户端向 `http://{webServerIp}:9988/jeesite/a/clogin` 发送 JSON。除登录外，多个管理操作也复用这个 URL，以 `request` 字段分派。不能把路径名理解为“这里只能登录”。

已确认的公共头：`Content-Type: application/json`，客户端还设置 `Connection: Keep-Alive`。字符串经 Qt JSON 序列化生成 UTF-8，请求大小按字节计算。

登录请求示例：

```json
{"request":"cclogin","username":"<用户名>","password":"<密码>"}
```

主程序 `0x0060C0C0` 构造请求；`0x0060D500` 为按钮流程。计时器设置为 8000 毫秒。`0x004A8290` 是另一登录控件的同类路径，并包含外网／本地服务器地址选择。

本次实际业务成功返回：`response` 为字符串 `"200"`，`userid` 为字符串。不能只凭 HTTP 状态码判断登录；GET 该 URL 返回 405，正确 POST 才返回业务资料。`OverDate`、`NoUser` 与超时、网络错误在客户端中分别处理，但没有对所有错误分支做服务器实测。

## 管理操作表

下表为静态确认的请求名及 JSON 键。请求值大小写和拼写应原样保留。

| request | 附加字段 | 操作性质 | 主要函数 |
| --- | --- | --- | --- |
| `cclogin` | `username`、`password` | 认证 | `0x0060C0C0` |
| `scdata` | `userid` | 获取预案资料 | `0x0046FF30` |
| `csdata` | `userid`、`data` | 上传当前控制资料 | `0x00445A60` |
| `downJointInfo` | `userid` | 获取墙面预览／预案信息 | `0x004709B0` |
| `addJointInfo` | `userid`、`index`、`jointid`、`picname`、`data` | 保存预览资料；`data` 内含矩形记录 | `0x00507AC0` |
| `ReNameDevice` | `DeviceId`、`NewDeviceName` | 修改设备名称 | `0x0054BD20` |
| `ReductionName` | `DeviceId` | 恢复名称 | `0x0054C3D0` |
| `downcollection` | `userid` | 获取收藏 | `0x00556470` |
| `collection` | `userid`、`fathername`、`nodename`、`multicastip`、`multicastport`、`ipcip`、`ipcport` | 创建收藏 | `0x00556F20` |
| `deletecollection` | `userid`、`id` | 删除收藏 | `0x00559AC0` |
| `updatecollection` | `userid`、`id`、`nodename`、`fathername` | 修改收藏 | `0x0055AAA0` |
| `downaudioandvideo` | `userid` | 获取音视频绑定 | `0x00689470` |
| `deleteaudioandvideo` | `userid`、`btnname` | 删除绑定 | `0x00689BD0` |
| `audioandvideo` | 下列音视频字段 | 保存音视频绑定 | `0x0068A450` |

`audioandvideo` 的字段：`vmulticastip`、`vmulticastport`、`vipcip`、`vipcport`、`amulticastip`、`amulticastport`、`channelnumber`、`aipcip`、`aipcport`、`danteip`、`dantechannel`、`btnname`、`userid`。

`addJointInfo.data` 中能看到 `nX`、`nY`、`nWidth`、`nHeight`。外层标识是 `index`、`jointid`、`picname`，不能把矩形字段简单全部搬到外层。

还发现 `addLog`、`userid`、`logContent` 常量；其完整 HTTP 调用链尚未确认，因此没有把它伪装成已验证的 OpenAPI 操作。

## 登录资料与编码层次

成功资料的主要结构是：

```text
response
userid
VideoJoint[]
  Area[]   层级、墙面、矩阵、输出节点
  Input[]  输入设备、流地址与参数
  Limit[]  用户控制限制
  Other[]  其它树节点
```

树节点可含 `ID`、`ParentID`、`Type`、`Layer`、`Name`、`IP`、`Position`、`Row`、`Colunm`、`Resolution`。原字段是 `Colunm`，不能自行改成 `Column`。

输入字段可含 `GroupIp`、`GroupPort`、`machinePort`、`logginName`、`password`、`machineParm`、`machineParmLow`、`Video`、`Audio`、`IndexCode`、`control` 等。它们兼有设备资料和媒体参数；不应把所有 IP 输入直接视为相同的组播编码器。

`Plan.Neutral` 的历史资料中，`data` 是字符串包裹的 JSON，含额外引号转义；客户端还有对应的替换／再解析过程。第一次 JSON 解码得到字符串时，不应直接按数组遍历。`Data.Neutral`、`Plan.Neutral` 和 `PlanPre.Neutral` 是客户端缓存，不是服务器协议本身。

## 用户通知 WebSocket

连接地址：

```text
ws://{webServerIp}:9988/jeesite/websocket/socketServer?userid={userid}&type=windows
```

`0x0071B030` 检查连接状态：连接成立时发送 `userid={userid}&type=windows` 文本；否则重新建立连接。它不是仅凭 `QWebSocket::open` 返回就认为会话有效。

`0x0071B730` 用字符串包含检查识别下列通知：

| 消息标记 | 原客户端行为 |
| --- | --- |
| `over#101#over` | 提示账号已在其它地方登录，关闭客户端 |
| `over#103#over` | 提示服务器修改节点／设备，要求重启软件 |
| `over#104#over` | 提示密码或用户组权限修改，要求重新登录 |

这些标记不是 JSON。匹配成功后停止计时器、断开 WebSocket，并进入对应提示和退出／重载路径。未用同一用户 ID 新连现场 WebSocket，以免干扰用户已有会话。

## 国标节点 WebSocket

`CWebSocketGbNode` 连接 `ws://{GbNodeServerIp}:10056`。发送 JSON 使用 `Operate` 键，初始请求为 `GetAllNode`；接收 `Operate=AddOneNode` 的节点资料，并处理 `ParentId`、`Name`、`Status`、`CivilCode`、`PtzType`。

这条通道与用户通知 WebSocket不同，不能发送 `userid=…` 注册消息来代替节点请求。已确认构造／解析键及连接 URL，完整节点事件全集尚未有服务端样本。

## 会议与摄像机 HTTP 代理

主程序中存在两套同类会议接口实现，分别在 `0x004988A0` 附近和 `0x00734570` 附近：

| 接口 | 客户端用途／参数 |
| --- | --- |
| `/api/user/login` | `account`、`password` 的 JSON 登录 |
| `/api/conference/query` | `fireTypes=Normal`、`queryType=AllConfOfCurrentUser` |
| `/api/user/getUserById` | 用户信息；路径／查询参数的完整组合待补 |
| `/api/conference/getConfSitesTreeByConfId` | 会议终端树，第二实现使用 `cmConfId` 查询 |
| `/api/video/getDeviceRtsp` | `confId`、`deviceId`、`epId`、`deviceType` 等查询参数 |
| `/api/confcontrol/{会议标识}/getConventionerInfo/` | `cmConfId`、`epId`，查询终端／分辨率信息 |

后续请求使用 `Authorization` 头。当前证据只证明客户端传入保存的头值，没有证明它自行附加 `Bearer `；不要凭接口风格添加前缀。

辅助播放器的关键帧请求已补足为 `POST http://{host}:9999/api/confcontrol/{cmConfId}/reqrtspiframe?cmConfId={cmConfId}`，JSON 为 `{"epId":"<终端标识>"}`，同样带 `Authorization`。它与核心管理服务的9988路径不同，见 [OpenAPI](contracts/http-openapi.yaml)。

摄像机平台代理模板包括：

```text
/api/video/v1/cameras/previewURLs?url=…&key=…&sk=…&cameraIndexCode=…&streamType=…&protocol=…&transmode=…&expand=…
/api/resource/v1/regions/subRegions?url=…&key=…&sk=…&parentIndexCode=…&treeCode=…
/api/resource/v1/regions/regionIndexCode/cameras?url=…&key=…&sk=…&pageNo=…&pageSize=…&regionIndexCode=…&treeCode=…
```

这是原软件访问代理的模板，不等同于厂商平台直接接口的签名规范。`url`、`key`、`sk` 来源于配置，参数的编码和代理返回须按该代理核对。辅助播放器另有 `/reqrtspiframe` 关键帧请求，见 [05](05-media-and-local-ipc.md)。
