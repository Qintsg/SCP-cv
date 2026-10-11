# T015：可信HTTPS完整页面、原生拒绝与失败回收

## 范围与层级

2026-10-11，从签名保存点054e3ae继续。使用正式`build:web`产物、完整Vue外壳和真实登录/CSRF，ControlHost以独立DataRoot的Simulation运行，不启动Worker。浏览器为Chromium153.0.8010.12，访问真实LAN HTTPS，`ignore_https_errors=False`；浏览器正常使用已确认的Windows CA信任。Playwright的Node请求客户端另用公开CA校验，二者不混为一项。

自有采集内容是明确标识的动态HTML标签页；只对该HTML提供浏览器路由，不声称它包含于dist。不采集用户桌面、麦克风或其他应用，不发D4播放、墙面或电源命令。公开在线/共享状态替身在下面单列，不造接收帧或原生释放证明。

## 发现并修复的真实问题

- 正式网页未配置`VITE_BACKEND_TARGET`时，API误回退到当前主机的8000端口，完整页面登录实际`ERR_CONNECTION_REFUSED`。先运行回归得到0/1红，随后网页默认走同源路径；显式构建地址与已选主机仍优先，未配置封装端仍保留8000回退。四项回归及相关20/20通过，不关闭TLS或降低Cookie/CSRF。
- 手机播放控制条的“停止共享”实际高32px；补真实几何断言后失败，保留`9c6858431a554a9fb80ddb0f17a30abb`。仅局部CSS将手机播控按钮最小高度设为44px，桌面保持紧凑；新增计算样式回归1/1通过，最终真实浏览器两种手机主题均44px。

## 权限与采集后失败：44个场景

证据：`.validation/qa-screen-share-010/t015-full-page-runs/5991b3cfdb40428f951b12f648f4f69f/evidence.json`，实际退出0、completed=true。1440/390px×浅深×六类场景24项，再独立重复五轮、每轮两目标×两条关键路径20项。

| 场景 | 数量 | 实际边界与结果 |
| --- | --- | --- |
| 播放器离线 | 4 | 真实API离线；禁用采集、零轨道/peer/共享写请求 |
| 不安全LAN HTTP | 4 | 真实HTTP前后端及登录；secureContext=false，明确限制且零采集/共享写请求 |
| 不支持采集 | 4 | 仅能力字段undefined替身；入口禁用，不当作真实设备能力结论 |
| 权限策略拒绝 | 4 | 正式HTML额外`display-capture=()`，原生NotAllowedError；零资源/共享写请求 |
| 浏览器原生拒绝 | 14 | Chromium官方QA参数`--auto-reject-capture`走原生用户拒绝路径，NotAllowedError；不是JS抛错替身，也不冒称人工点击普通系统选择器 |
| 已采集后API拒绝 | 14 | 原生采集，仅只读GET/SSE在线字段替身；真实共享POST返回503/share_player_offline，全部21条实际返回轨道ended，零peer、零DELETE/普通CLOSE/自动重试 |

原生采集/拒绝的键盘Enter调用均核瞬时用户激活；包含内容音频仅在明确勾选后请求，深色目标2使用自有测试音。未改变本机原系统端点静音/音量。Chromium测试参数仅供QA，生产代码不自动选择或拒绝权限；原生拒绝路径依据[官方测试源码](https://chromium.googlesource.com/chromium/src/+/refs/heads/main/chrome/browser/media/webrtc/webrtc_getdisplaymedia_browsertest.cc)。

共享面板按钮/内容音频选项至少44px，Tab进入主按钮的焦点描边至少2px；根、main和面板无横向溢出。窗口1/2导航不新增播放或墙面写请求。pageerror为0；只允许精确场景对应的权限策略拒绝和真实503控制台错误，普通warning保留，不宣称所有console输出为0。

## 公开状态、停止及手机可达性：最终4个场景

最新证据：`.validation/qa-screen-share-010/t015-public-state-runs/e5589618aad94baeac69b90c93a4beb2/evidence.json`，实际退出0、四项passed/completed=true。1440/390px×浅深；真实登录/TLS与正式App，HTTP/SSE共享快照和固定原ID DELETE明确为替身，不采集/创建peer或接收资源。

- 零帧playing仍显示等待，不绿色化；正帧显示30帧；离线标明最后报告且停止禁用；恢复后键盘Enter只停止原分享ID一次。
- 意图清除而实际资源仍在时显示停止待确认，保留最后帧，不声称已释放；普通音量/静音禁用，零共享缩略图。
- 手机页签方向键选择/focus与44px/2px描边通过。稳态截图等待登录成功通知实际消失，不用任意睡眠或删除通知DOM。
- 使用Playwright trial点击保留真实滚动和遮挡检查，随后独立核按钮四角/中心的elementFromPoint及完整视口范围。手机停止按钮top825/bottom869、高44，五点无遮挡；底部导航没有挡住。主代理查看最后两种手机主题视口原图。
- pageerror/console error为0，零轨道/peer；真实客户端仍发送CSRF头，但假DELETE受理不能证明后端释放。

旧公开状态通过记录`7d34ed15cbdb4880850bca5ca64acad3`保留，不重复计作新增用例。补遮挡检查首轮`c2283f279cfc4646b974a3715ff8bd98`在关闭上下文时与route.fetch竞争；只修ignored QA，在关闭前`unroute_all(behavior='wait')`等待已有拦截处理结束，不忽略异常。按[官方生命周期合同](https://playwright.dev/python/docs/api/class-browsercontext#browser-context-unroute-all)处理后四项通过记录`9c15e190220f41bf8cbd23a6d69d5c81`保留；之后增加44px严格断言才检出上述真实样式问题。

## 其他失败证据与验证

全部失败目录保留：auth/status形状误判51ea67c、标题误判d3d586a、HTTP前端对接TLS后端的Secure Cookie夹具错误2c0b648、Node客户端未信任CA的f36b5a7、开发HMR网络检查ebe6eb9、真实默认8000问题101684a、status locator歧义ea36ee0。只改对应QA边界或上述两项生产缺陷，不把夹具失败称作Player故障；不回显Cookie、随机QA口令或私有密钥。

- 默认地址修复后全前端429/429、零失败/跳过/取消/todo，实际退出0；`t015-web-origin-full-frontend.log`。`build:web`含vue-tsc实际退出0，21.46秒；新增触控修复后再次构建退出0、2.58秒，`t015-touch-build.log`。入口1138.31kB/324.68kB gzip的既有体积告警保留。
- 触控修复后的最终全前端430/430、零失败/跳过/取消/todo，实际退出0，120149.4317ms；`t015-final-full-frontend.log`。本机默认pnpm入口曾长时间未进入命令，最终原构建/专项实际成功；最终全套命令仅当前进程使用`pnpm_config_pm_on_fail=ignore`，仍为pnpm，仓库CERNET、依赖及packageManager不变。没有证据确定启动延迟的完整根因，不称作业务修复。
- API/IPC、.NET和依赖未变，本阶段不将上一1427/.NET或Redocly算作新运行；最终T018仍需重新执行完整门禁。
- 自有三服务器在finally终结且wait退出，主动结束的exit1不是业务失败。最终5212/5213/5214/5215无监听；浏览器上下文/进程由本轮拥有并关闭，不停止其他浏览器或Node。CA仍仅为本轮后续HTTPS/D4验收保留，结束后按精确指纹撤销，不重复导入或删除其他根。

## 剩余边界

T015的真实浏览器权限、失败回收和完整页面门禁通过，不代表D4出画。T016仍须本机→D4两正式Worker、跨机ICE、同时两会话连续五分钟、十轮及断线/换源/非目标保持；现场可听另按实际证据报告。T017最终文档与T018完整验证仍未完成。D4仍fd64de6源码/deploy-20261009-c824924稳定009，未部署010。
