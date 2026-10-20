# 验证指南

1. 用`rtsp://operator:example-pass@192.0.2.188:554/streaming/channels/501`创建无名称源，重读/修改，核完整URI与安全自动名称。
2. 测编码、旧源、UDP空userinfo和SRT查询；非法协议/空用户名带密码拒绝且无记录变化。
3. 回环RTSP返回401 Basic挑战，真实Player提交测试凭据才允许DESCRIBE/SETUP；无真实媒体/音量输出。
4. 纯展示、真实Ant默认隐藏/显式查看、创建/编辑原值与播放摘要分别验证；构建/只读浏览器不代表实体摄像机验收。
5. 运行xUnit、前端测试/类型/构建、Spec Kit与Redocly；未执行项如实记录。不部署或重启D4。
