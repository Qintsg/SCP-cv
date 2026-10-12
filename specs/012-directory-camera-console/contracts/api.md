# API合同设计

OpenAPI3.0主合同更新`docs/openapi.yaml`并用Redocly校验。均沿用session/CSRF与权限。

- GET `/api/sources/`：原列表，源新增`path`与`is_protected`；GET folders新增`is_protected`、前导`/`的relative_path。原创建/上传/move/update/delete在保护源和目录返回明确失败。
- 批量前端调用既有单项写API，逐项记录结果，失败不自动重放；避免另造无事务保证的批量成功合同。
- GET `/api/venue/`：相机与音频能力、可用原因、当前配置；不回传认证秘密。原`/api/devices/`继续只承载电源，避免路由冲突。
- POST `/api/venue/cameras/{id}/motion/`与`positions/{position}/{action}/`：当前运动绑定未知，409且零实体写入。
- PUT `/api/venue/audio/`：可选来源与三个音量；预检全部绑定，当前仅screen_volume可执行。来源/话筒未知拒绝且零写入。POST`audio/read/`只读查询。结果区分confirmed/simulated/sent_unconfirmed/mismatch；发送后读回确认，不冒充人工可听验证。
- GET/POST/PUT/DELETE `/api/scenario-folders/`：独立目录CRUD；预设写合同新增`folder_id`与`advanced`，DTO增加path。未提交高级项不改变设备。
- 首页原墙面apply响应增加声音阶段结果，墙面失败不发声音；墙面成功声音失败保留画面事实且提示，不重复墙面。
- 共享质量仅有限纯值诊断，不包含地址、SDP、候选或token；首样本/缺失字段为未知，不替代原生命周期合同。
