# 播放合同设计

## REST（OpenAPI3实际合同仍在docs/openapi.yaml）

- 原`/api/settings/powerpoint/`明确退役，零COM副作用；新`/api/settings/presentation/`报告依赖与能力。
- 设置诊断新增`components[{id,status,detail,reason_code}]`，status为installed/unavailable；摘要与available来自同次只读快照。检查运行依赖，不要求SDK/targeting pack，不启动Office或加载UNO程序集进.NET10；不返回配置值/磁盘路径。静态通过不是文稿/出画证明。
- 既有两窗OPEN增加显式presentation_mode：libreoffice默认；PDF原源继续pdf；同源静态产物只在明确选择且修订匹配时使用。
- `fit_text` 默认 false，原排版和适配布局不可静默混用；OPEN与显式prepare采用同一选择，适配仅作用于内存，不改PPT/PPTX原件。
- 只读`GET /sources/{id}/ppt-resources/`与`GET /sources/{id}/slides/{page}/`采用相同可选查询`fit_text=true/false`，缺省false；非法/重复值400，不猜测或回退另一版。
- 控制页及专注页预览按实际`actual_source_id/actual_fit_text`读取，未知实际布局不借期望源/默认图；切源/切布局及卸载使旧请求失效。
- 页资源字段保留；制品有页但未持久登记PptResource行时，资源`id=0`、时间沿源登记时间，不为只读请求写库。备注/媒体沿已有页信息；未准备图片为空、PNG404，不能用旧页row冒充对应布局就绪。
- 预览制品存在`text_layout`声明时必须与所选键一致；历史缺声明沿该键兼容。手动刷新仅GET，独立于原生OPEN/导航，不重置既有8次自动补齐预算。
- 既有next/prev保持翻页语义，新增next_effect/previous_effect；goto外部1起始，内部adapter转换0起始。
- goto必须有JSON正整数int32 target_index，缺/null/非法值入队前400 invalid_navigation；first/last无需显式目标，沿已知页数派生。不新增500限制或把HTTP受理/同页Confirmed当动画结束。
- 当前统一媒体暂停/恢复/定位capabilities=false，前端禁用并说明原因，后端先返回unsupported且无原生副作用；不Completed假成功。将来声明支持的控制仍须依据真实媒体结果。
- 实際confirmed adapter/mode与desired request分开，转换queued不等于可播出。
- 候选切入失败且旧资源已关闭/候选已释放时，Worker返回failed/presentation_activation_failed及明确空actual；REST实际源/布局为空、模式沿既有空串。清理未确认仍uncertain，不能报空输出或自动重试；无当前资源的翻页明确拒绝，前端actual_source_id=null不发导航。
- `pending_command`表示按目标序号最早的未终结指令（Pending或Processing），领取、状态上报和再次入队不能提前隐藏执行中的指令；终态后显示下一指令或空值。该字段不是动画阶段、operation完成身份或视觉完成证明。
- 显示入队响应仅目标会话携带`submitted_command_id`；普通GET/SSE不带旧回执。认证只读`GET /playback/{window_id}/commands/{command_id}/`按Display目标/窗口/UUID精确查询，返回command_id/window_id/command/source_generation/status/result_code/completed_at，不暴露参数、claim或路径；未知/其它目标404且no-store，不下发或重放。
- 前端后台只读查询本次回执，35秒范围不代替原生完成证明。仅completed/presentation_at_last_slide且下一页/下一效果、代次及完成时间匹配时提示；failed/uncertain分别反馈，superseded不提示末页。路由卸载、源/窗口/代次/主机切换及后续本客户端操作使旧提示失效。

## 本机进程协议

player-1/2仍唯一Display所有者；impress-1/2及converter不领取Display命令。侧车持有UNOproxy，协议不携带原生对象。

纯值identity包含operationId、parentcommand/claim、window/group/owner/engine/document/source修订及generation、deadline、父HWND/PID/start/session/DPI。

协议v3已实现于 `runtime-dotnet/src/ScpCv.Presentation.Protocol/README.md`，由Contracts客户端和Net48侧车引用：4字节小端长度+1..65536字节UTF-8 JSON，固定DTO与深度/对象数配额、结构/期限/可信currentScope及保守能力门禁，先独立快照再执行；请求/结果规范SHA不充当身份认证。
请求可选`fit_text`（缺失为false），true仅可用于open/export_slides。规范请求字节在readOnly/macros/links后写fitText，再写parentWindow；请求/结果tag分别为`SCP-CV.Presentation.Request/v3`和`SCP-CV.Presentation.Response/v3`。
只接受protocol_version=3；v1/v2及未知版本在执行/结果采用前拒绝，不自动降级或重盖历史指纹。v2的文字适配历史实证保留；v3增加末页noop结果语义，调用方/宿主同版重编及真实回环必须再验，不能混用尚无末页保护的旧宿主。
converter以parentJob关联，shutdown以已认证通知的parentShutdownId关联且与Display claim互斥；动态页码1..500、具体原生预算更短于5分钟通用上限。调用方须验证真实OS/租约/原件并保存独立请求/最新scope，accepted不能占终态指纹槽。

完成必须验证同组/同实例/同文稿；晚到结果丢弃并可诊断；超时只能取消等待、不证明原生未执行。关闭顺序：停放映/媒体→关闭文稿→退出自有引擎→精确原生退出证据。

共享包预检不凭.pptx后缀授予原生执行：OOXML声明扩展及可解析ZIP内ppt/presentation.xml采用同一外链/XML门禁；非OOXML旧格式不伪称通过OOXML预检。已知纯包拒绝返回Failed/presentation_package_rejected（转换）或候选前异常（动态），原件保留；宏执行和自动更新禁止不等于所有含宏/加密格式都已在包预检识别。见当前内容门禁记录。
OPEN/export_slides本地源路径格式与既有登记对齐：ppt/pps/pot、pptx/pptm/ppsx/ppsm/potx/potm、odp，PDF继续独立适配器，不扩大为任意扩展/URL；不提供密码字段或宏授权。格式策略通过不是该格式原生视觉或安全全部通过。
export_slides仅在加载交互实际Abort、无模型/自有组件/输出、原件保持时可failed/presentation_load_rejected；父调用方必须继续核正常引擎/完整job退出，失败仍typed uncertain。未知加载、导出开始后、旧输出/退出未确认不走此明确失败分支；原uncertain不重放或回写为failed。
调用方动作异常/未成功响应后若自有退出也失败，本地异常链同时保留动作及清理原因，以既有transport uncertain处理；正常退出保持原异常/结果。不新增协议/REST字段或错误码，不承诺将完整Aggregate堆栈写入公开响应或持久终态。
末页无操作返回status=noop/error_code=presentation_at_last_slide及已确认当前源/页观测，仅next_effect/next_slide可采用；源码库不凭页码/点击数确认动画耗尽。实际放映末页可能早于total（尾部隐藏页），该事实须由可信原生宿主确认。正式Host已仅对无主序/实际无后继分支产生noop，边界前不resize或推进；Player动画/整页桥接及HTTP/前端精确反馈已有限验证。有主序耗尽与End.NONE保护、隐藏重复页及完整闭环仍由T069验收，不能将无主序样例称通用保护已交付。
