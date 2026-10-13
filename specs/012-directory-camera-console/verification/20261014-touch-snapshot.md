# 中控快照与三滑杆接收验证（2026-10-14）

本轮修正只读夹具，不改正式HTTP、MCU固件、业务库或软件音频策略。实际控制测试使用固定d67ce53的独立Hardware ControlHost、独立数据根与回环28161，正式运行组保持；控制效果只证明MCU缓存，不当作DSP增益或现场可听验收。

## FE82边界与红绿

原47dca0收到48cb50解析的长度后设置正文剩余数为`declared-1`；48cc90另消费四个未解释字节和FC。已收到的声明2049对应2048字节SQLite、5字节尾，总2059，而非旧夹具假设的2055。原48cb50高字节运算不等同完整BE32，当前诊断只接受已核小长度1–65535，不扩展未知大包。

在Unicorn只执行原纯头/尾解析指令，不执行Qt、文件替换或网络：8项完整/短头/坏头/环绕/尾/短尾/坏尾/四字节变化全部匹配。原尾解析器不验证这四字节，不能把它们称为已验证CRC。原源码和伪代码导出在`.validation/venue-frame-012`，原捕获SHA256仍`72664584B1A4A3C1E6AEAE167278ABA68E005669B5DA57CB95C62BAD86727BA8`。

只读诊断模块SnapshotEnvelope只暴露流读取，先原错误长度InvalidDataException/exit1，再按声明减一正文+5尾修正；实际原捕获accepted/2059/remaining0/exit0。随后10项模块回归通过：完整、短头/正文/尾、坏头/尾、超限、两帧粘连不吞第二帧、提前取消、真实回环TCP的17字节分段。没有放宽坏尾或超限，也不依赖一次read返回整帧。

D4新夹具只发送已有FE02查询，完整2059字节及摘要相同、readonly_cache=true、dsp_confirmed=false。发布归档SHA256`97DCCF48837355C1D1A6AB7EF3BB4E818321ED4D2E9EEC521A49938643996C55`；旧失败夹具/包/记录保留，未将绿色结果回填历史失败。

## 三路真实REST→触控→MCU缓存

用既有正式Host DLL（SHA`63B07694AE28BD00DE5243345F12167B14B7249AEB93B8230B07FBFFB47F2D6C`）启动独立Hardware主机，不启动任何Supervisor/Worker。正常Cookie/CSRF PUT`/api/venue/audio/`到真实5.10:8889，然后通过独立FE02/SQLite只读kind2 Join1–3核缓存；不是Simulation回执。基线不是12/10/9则首包前拒绝。

| 字段/Join | 一档下调请求与MCU值 | 恢复请求与MCU值 |
| --- | --- | --- |
| 头戴/悬挂 Join1 | hanging_volume=58 → 11；另两路10/9保持 | headset/hanging同值64 → 12；sent_fields仅headset_volume |
| 手持 Join2 | handheld_volume=48 → 9；另两路12/9保持 | handheld_volume=53 → 10 |
| 电脑音频 Join3 | computer_volume=43 → 8；另两路12/10保持 | computer_volume=48 → 9 |

六阶段passed=true/探针exit0，每次降低及恢复API均sent_unconfirmed且唯一对应字段；没有伪造confirmed或用155读回替代DSP。故障路径独立恢复原MCU请求；检测非测试的其他通道或新档位则拒绝覆盖，不盲重放。最终缓存12/10/9，独立Host在探针结束后按原句柄精确结束，28161释放、独立运行组状态文件不存在。没有保存现场机位、改源或发送电源/云台命令。

正式业务配置未设置这些试验请求值；原七个进程、两输出、11媒体、来源草稿、系统三个默认角色/四端点总音量静音与背景配置保持。18项现有Host话筒别名/全字段预检/部分失败/来源回归再次通过，0跳过；这不是重新执行全量1601/476。5.9的TCP80/443/5062及HTTP读取未成功，不能由TCP结果判断Dante UDP能力，不尝试猜测协议或账号。

## 归档与仍未完成

初次归档把两个result.json平铺为同名，解压出现重复名错误；原失败包SHA`08BC9EA19002D6F311C4DE470815B45A646BE2D300D68C5417CE3B515522D154`保留，不能当完整可用证据。改用明确标签文件名的六文件归档，下载后检查六个唯一Entry、正常解压和各结果；`.validation/venue-frame-012/microphone-evidence-labeled.zip`与D4 SHA256均`966E31E21EA6CA7CE608CA3ADAF63D4EAAF0AE2471C46F424E0CDDCC05873DE6`。归档不含密码、私钥、业务库或未知设备原包。

协议、别名及实际MCU接收/恢复已有新证据；实际DSP串口执行、IN通道增益和可听效果仍未确认，因此T036及T020/T022/T025不据缓存变化提前勾完成。五摄像机网络、普通网页手势、实体来源与共享清晰度/长稳仍另追踪，012任务仍40/30/10。
