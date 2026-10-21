# CameraBOX只读核对

用户文件：E:/Projects/Resources/Common/CameraBOX.zip；SHA256 `0C4BB657187E4EBFDCBC81781AC80F96DBC84C970E17BAD8D1670B45884432D2`。压缩项总展开约54KB，只读取Python源码／README；未运行、导入其模块、解压pyc或连接设备。

## 已确认对应

camera_controller/camera_control.py构造9字节：`FF 06 01 0A 00 code 00 state FA`。方向／变焦code为1F–24，pressed=01／release=00。与项目VenueJoinProtocol.Digital、VenueCameraController的Join31–36逐字段相同，端口8889/TCP，与84–88现有绑定一致。

附件PRESET_A=27（39）是现有调用模式，PRESET_B=2D（45）是位置1按钮，不证明只有两个预置位。现有调用39后45–50、保存38后位置按钮来自更完整触控工程，本轮不降级或把A/B误标成两个机位。附件README帧示例漏一个01字节，源码是9字节；不按错误文字重写。

## 不能照搬的部分

- `_receive_response`只尝试recv(11)，没有帧语义校验，也未被发送路径调用；不能凭此把写出升级为设备确认。
- run.py示例主机127.0.0.1不是现场地址；其finally仅disconnect，没有保证释放全部按下动作。现有短租约、停止围栏、看门狗和保存确认继续保留。
- 不引入Python运行时、不执行--test自动转动，也不将Xbox示例当浏览器已获得手柄权限；手柄控制非本轮范围。

原型摄像机预览与控制布局已展示，真实云台／位置保存／实际回应仍未测试，正式页接线在确认demo后的任务中跟进。
