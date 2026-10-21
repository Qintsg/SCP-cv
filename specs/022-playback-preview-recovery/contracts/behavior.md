# 行为合同

已有REST认证、CSRF、运行组身份及来源代次校验保持。

媒体成功需要当前Worker完成及真实进度/页码；发送命令受理不代表实际播放。当前有效音频停止观测可以更新历史错误，真正失败继续报告。

## 空闲音频启动观测

内部Welcome可向已认证的AudioWorker提供可选`initial_idle_audio_generation`。只有当前组处于Starting/Armed、背景音乐没有来源且没有Pending/Processing音频命令时提供当前代次，其它角色不携带此字段。

新AudioWorker在没有已执行来源、没有实际媒体对象并读到原生停止状态时，上报该代次及`initial_idle=true`、`source_id=null`。服务端再次验证组、Worker实例、owner、目标代次、空来源与没有在途命令，才接受停止状态。这个观测不修改音量、静音、循环、播放列表或系统输出设备。执行任何OPEN/STOP后，初始观测不再使用；旧进程和旧代次保持拒绝。

摄像机仍按来源共享和按需启动，客户端失败应返回明确原因，资源清理失败应可诊断。内部成熟格式组件不接受任意外部发布；任何内部监听只绑定回环并验证自有会话身份。

本轮当前命令使用npm；Python辅助命令使用uv。公开接口变化必须更新`docs/openapi.yaml`并运行Redocly。
