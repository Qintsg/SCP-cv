# T018最终完整门禁

2026-10-11，最终生产代码签名保存点`4f76a104277de47a347549731272c066d0fc0023`。后续仅补文档/验收记录；本记录只列实际执行结果，不累加阶段计数或抹掉失败。

## 代码回归与构建

原始证据在ignored `.validation/qa-screen-share-010/t018-20261011-final/`。

| 门禁 | 结果 | 原始文件 |
| --- | --- | --- |
| .NET锁定还原 | 成功，无依赖改动 | `restore.log` |
| Release完整解决方案构建 | 退出0，0警告/0错误，4.44秒 | `t019-build.log` |
| 最终非Physical.NET | 1437通过、0失败/跳过；六独立TRX核total=executed=passed | `t019-full-test.log`、`t019-full-results/` |
| 全前端 | 430通过，0失败/取消/跳过/todo，128691.607ms | `frontend-test.log` |
| 类型检查 | vue-tsc退出0 | `typecheck.log` |
| 正式Web构建 | 退出0，14.41秒；入口1138.31kB/324.68gzip | `web-build.log` |
| OpenAPI3.0 Redocly | 有效，退出0，三项既有退役PowerPoint端点无2xx告警 | `redocly.log` |

后端最终计数：Domain41、Contracts325、Windows197、Infrastructure171、Integration279、ControlHost424。测试通过不代替真实采集、运动像素或硬件声音，后者在[T016](t016-d4.md)按层登记。Vite既有>500kB和插件耗时提示保留，没有关闭阈值或改镜像/依赖以消除提示。

## 真实失败、定位与红绿

- 首完整回归1426/1427：Receiver合同夹具只模拟一次在线心跳却使用墙钟及真实后台回收，慢机超过生产5秒窗口被合法撤销。独立回归等待六秒真实红404；仅该夹具固定TimeProvider，显式推进六秒后仍由真实Cleanup确认失效，九项Receiver用例绿。生产五秒时限和清理没有放宽，签名保存点`27523ef`。
- D4原Player死亡并明确重启后，旧实际共享身份残留。真实Program/SQLite回归先红`Assert.Null`，再让确证关闭按原完整Target/GUID/OPEN代次清除内存投影。晚到报告不能复活、晚到释放不关后来共享，六种错误身份及两窗/重复释放分别核验；相关29项绿后完整1437绿。原红/绿文件为`release-projection-red.log`、`release-projection-related.log`及各独立TRX。
- D4新包原OS退出/首次closed真实成立；协调器因外部回执复制延迟错过一分钟终态保留期，原completed=false保留。独立复核原证据，再一次明确重启/两新授权/两窗后帧及实际停止通过，不把404当资源存活或伪造原轨道记录，见T016。
- D4独立音频meter因精确自有浏览器身份不匹配退出1；无有效采样，不能称现场听音或D4原生峰值通过。

## 最终审计与清理

最后的Spec Kit全目录、diff、严格UTF8/LF、变更文件长度、凭据/私钥/构建产物与Git暂存范围分别检查通过；requirements只读16/16，未改审查清单，`.specify/extensions.yml`不存在，因此无实现后扩展hook。变更文档最长CHANGELOG473行，生产新增/修改代码均不足500行。

临时浏览器在finally关闭；只解除归属已核对、已经Ready的本轮D4像素/meter QA任务，原证据、数据库/媒体/备份和旧包保留。最终远端审计退出0，正式ControlHost/Infrastructure摘要匹配4f76a10包，六项目进程路径/会话正确、运行组文件登记四受管子进程；正式runtime和frontend任务Running。两窗在线idle，代次808/541实际与请求一致、源/共享四身份均null、无错误；DISPLAY2/3为唯一播放目标，DISPLAY1/4/5不绑定，原控制桌面不占用。自有Player profile的WebView进程0，QA任务精确解除后检查不存在；未发墙面、电源或系统音量操作。

本轮CurrentUser测试CA只按精确指纹撤销并检查，不删除其它根或关闭TLS保护；组织证书及其它设备信任仍是正式访问前提。操作结果见[T014收尾](t014-https.md)，不能把临时测试证书当无限期正式证书。
