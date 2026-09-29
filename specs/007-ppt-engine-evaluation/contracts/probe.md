# 临时诊断合同

仅本机原型，不新增生产REST合同。

- `window.pptProbe.ready` 与异步 `dispatch({action,...})`。
- source固定AllinOne/TestPPT，源为回环/本地只读副本。
- load/next/prev/goto/step/pause/resume/state/close；goto为0起始。
- 不支持动作明确报错，不伪造完成/内部store推进。
- 状态含实际页、加载、错误、媒体时间/资源；动画与导航分开。
- .NET记录请求和真实响应；超时、页面失败、退出分开。
- UNO记录真实controller命令；外部窗口不算.NET内嵌。
