# 播放合同设计

## REST（OpenAPI3实际合同仍在docs/openapi.yaml）

- 原`/api/settings/powerpoint/`明确退役，零COM副作用；新`/api/settings/presentation/`报告依赖与能力。
- 既有两窗OPEN增加显式presentation_mode：libreoffice默认；PDF原源继续pdf；同源静态产物只在明确选择且修订匹配时使用。
- 既有next/prev保持翻页语义，新增next_effect/previous_effect；goto外部1起始，内部adapter转换0起始。
- 当前统一媒体暂停/恢复/定位capabilities=false，前端禁用并说明原因，后端先返回unsupported且无原生副作用；不Completed假成功。将来声明支持的控制仍须依据真实媒体结果。
- 实際confirmed adapter/mode与desired request分开，转换queued不等于可播出。

## 本机进程协议

player-1/2仍唯一Display所有者；impress-1/2及converter不领取Display命令。侧车持有UNOproxy，协议不携带原生对象。

纯值identity包含operationId、parentcommand/claim、window/group/owner/engine/document/source修订及generation、deadline、父HWND/PID/start/session/DPI。

协议v1已实现于 `runtime-dotnet/src/ScpCv.Presentation.Protocol/README.md`：4字节小端长度+1..65536字节UTF-8 JSON，固定DTO与深度/对象数配额、结构/期限/可信currentScope及保守能力门禁，先独立快照再执行；请求/结果规范SHA不充当身份认证。
converter以parentJob关联，shutdown以已认证通知的parentShutdownId关联且与Display claim互斥；动态页码1..500、具体原生预算更短于5分钟通用上限。调用方须验证真实OS/租约/原件并保存独立请求/最新scope，accepted不能占终态指纹槽。

完成必须验证同组/同实例/同文稿；晚到结果丢弃并可诊断；超时只能取消等待、不证明原生未执行。关闭顺序：停放映/媒体→关闭文稿→退出自有引擎→精确原生退出证据。
