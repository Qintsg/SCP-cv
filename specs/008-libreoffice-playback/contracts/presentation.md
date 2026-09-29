# 播放合同设计

## REST（OpenAPI3实际合同仍在docs/openapi.yaml）

- 原`/api/settings/powerpoint/`明确退役，零COM副作用；新`/api/settings/presentation/`报告依赖与能力。
- 既有两窗OPEN增加显式presentation_mode：libreoffice默认；PDF原源继续pdf；同源静态产物只在明确选择且修订匹配时使用。
- 既有next/prev保持翻页语义，新增next_effect/previous_effect；goto外部1起始，内部adapter转换0起始。
- 统一暂停必须依据媒体实际结果；未验证能力明确unsupported，不Completed假成功。
- 实際confirmed adapter/mode与desired request分开，转换queued不等于可播出。

## 本机进程协议

player-1/2仍唯一Display所有者；impress-1/2及converter不领取Display命令。侧车持有UNOproxy，协议不携带原生对象。

纯值identity包含operationId、parentcommand/claim、window/group/owner/engine/document/source修订及generation、deadline、父HWND/PID/start/session/DPI。

完成必须验证同组/同实例/同文稿；晚到结果丢弃并可诊断；超时只能取消等待、不证明原生未执行。关闭顺序：停放映/媒体→关闭文稿→退出自有引擎→精确原生退出证据。
