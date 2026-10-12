# 数据模型

- MediaFolder：原字段；增加SystemKey（空为普通目录，camera为内置目录），DTO投影is_protected。根目录ID=null；相对路径公开为`/folder`。
- MediaSource：原字段；增加SystemKey（空为普通源，camera:ID为系统项）与EntryName（虚拟流叶名）；DTO增加path/is_protected。实体文件path以media根相对文件路径为准；RTSP为目录+逻辑文件名。内置摄像机由服务配置同步，禁止用户写入、移动、删除或向保护目录新增。
- CameraDefinition：稳定ID、名称、路径、RTSP、movable、control binding/evidence。配置只在主机配置侧管理，用户API只读。未确认流地址不得显示可用。
- VenueAudioState：来源ip81/scp_cv、screen/handheld/hanging三个0..100值、输出绑定、发送阶段与详情。意图与实体确认分离。
- ScenarioFolder：Id/Name/ParentId/CreatedAt/UpdatedAt；拒绝循环和同父同名。
- Scenario：增加FolderId、AdvancedJson；旧记录位于根且高级字段为空。Advanced包含camera_positions（ID→位置或null）、可选audio（source/screen_volume/handheld_volume/hanging_volume）；空或null表示保持，不等于关闭/归零。FolderId由服务验证而非新增外键重建旧Scenario表。
- ShareQuality：捕获与发送真实宽高/fps/差分Mbps/qualityLimitationReason、建议上限及参数接受状态；每个共享实例独立，停止后不采样/不发布迟到结果。

## 持久化与安全

ControlHost唯一写入SQLite。使用EF增量迁移，新增列有空/false默认值；不重建或覆盖旧库。内置标记与设备配置不依赖前端字段。公开路径不透露机器绝对目录。文件操作保留原已有越界、符号链接、占用、重名和事务补偿保护。
