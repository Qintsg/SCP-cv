# HTTP默认HTTPS验证（2026-10-15）

## 实现与本机验证

用户最新要求取代此前“HTTP直接提供控制台”。已配置TLS时，网页服务在业务middleware前返回307/no-store，目标为同主机和实际HTTPS端口，路径/查询保持。GET、HEAD、POST均不先进入HTTP业务；HTTPS不会循环跳转。无TLS开发环境继续HTTP，错误TLS配置仍拒绝启动。

新增8项回归先复现4个HTTP200不跳转及缺少目标构造，修复后通过。首轮测试夹具的middleware不是可调用函数，已修正夹具再取得有效红灯，未把夹具错误当生产缺陷。

| 检查 | 结果 |
| --- | --- |
| 入口、TLS、跳转、地址与连接页相关回归 | 55/55，0失败、跳过；新增8项 |
| vue-tsc | 退出0 |
| Vite网页构建 | 退出0，17.93秒；既有大chunk/配置导入警告保持 |
| Redocly CLI 2.63.0 | access.yaml合法，直接CLI退出0 |

回归命令为`node --test scripts/http-https-redirect.test.mjs scripts/dual-protocol.test.mjs scripts/frontend-https.test.mjs scripts/https-upgrade.test.mjs scripts/client-connection.test.mjs scripts/server-connection-view.test.mjs`。没有变更后端或播放模块，本轮不重复全量.NET与无关前端回归。

HTTP socket回归是真实本机监听；HTTPS分支单测不等同真实握手，正常TLS代理与认证由D4另验。CLI下载最初受代理与Node信任链影响，最终通过当前进程的正常系统CA及npmjs直连取得工具；直接调用缓存Redocly再次退出0。仓库CERNET配置未改，不关闭TLS验证。首次pnpm子进程退出断言保留，不冒充干净退出。

## D4部署

待同步已验证源码与网页，重启原网页任务并核验跳转、正常TLS内容和认证、原六个播放运行进程及两窗状态。旧网页、Node日志及原任务配置保留在私有部署目录，不复制用户证书或口令。
