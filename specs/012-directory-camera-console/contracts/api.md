# API合同设计

OpenAPI3.0主合同更新`docs/openapi.yaml`并用Redocly校验。均沿用session/CSRF与权限。

- GET `/api/sources/`：原列表，源新增`path`与`is_protected`；GET folders新增`is_protected`、前导`/`的relative_path。原创建/上传/move/update/delete在保护源和目录返回明确失败。
- 批量前端调用既有单项写API，逐项记录结果，失败不自动重放；避免另造无事务保证的批量成功合同。
- GET `/api/venue/`：相机与音频能力、可用原因、当前配置；不回传认证秘密。原`/api/devices/`继续只承载电源，避免路由冲突。
- POST `/api/venue/cameras/{id}/motion/`：direction、phase(start/hold/stop)、motion_id、issued_at_ms；开始2秒新鲜度，300ms续租、900ms失效释放，迟到请求不得启动或影响新动作。五摄像机白名单，固定监控拒绝。`positions/{position}/{action}/`仅1–6、save/call；不自动重试。返回camera.status/detail，sent_unconfirmed不等于机械动作已确认。
- PUT `/api/venue/audio/`：screen_volume保留155增益读回，computer_volume、handheld_volume、headset_volume为电脑/手持/悬挂（原头戴1/2）请求百分比，按现场Lock=19换算20档。hanging_volume为用户确认同组的旧输入别名：单值或同值仅发Join1一次，headset_volume为唯一保存/响应和sent_fields字段，hanging_volume规范化为null；两个非空值不同则409且首包前零写出。source固定二选一；IP81独立可发送，SCP-cv须显式63号输出、唯一活动Render端点、两窗口私有网页/Office引擎和已知声音策略接口只读预检通过，否则409且全部零写出。GET能力source为传输装配，source_ip81/source_scp_cv区分具体可用项。POST`audio/read/`仅读155且不清除其他字段。结果区分confirmed/simulated/sent_unconfirmed/mismatch/uncertain，source为本次已写出请求（不是实体路由），sent_fields、failed_field标明部分阶段；后续失败保留已写出来源，不冒充DSP读回或人工可听验证。
- GET/POST/PUT/DELETE `/api/scenario-folders/`：独立目录CRUD；预设写合同新增`folder_id`与`advanced`，DTO增加path。未提交高级项不改变设备。
- 首页原墙面apply响应增加声音阶段结果，墙面失败不发声音；墙面成功声音失败保留画面事实且提示，不重复墙面。
- 共享质量仅有限纯值诊断，不包含地址、SDP、候选或token；首样本/缺失字段为未知，不替代原生命周期合同。
