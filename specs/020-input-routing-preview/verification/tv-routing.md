# 电视路由、输出记账与预案接线（2026-10-21）

## 实现范围

- T008：两电视固定161/162:4830，独立矩阵codec；同一TCP连接顺序写6231满屏、610D单源，不套用50节点清屏/提交/刷新。此顺序为项目组合策略，不冒称原抓包序列。
- T009：RuntimeState新增DisplayRoutingJson；迁移只增列，旧记录unknown。每目标完整成功后保存last_sent、私有包级fingerprint、状态与时间；首包前置dispatching，异常/取消/提交不确定均不可沿旧成功跳过。启动将中断的dispatching转uncertain，不自动重放。
- 预案门禁内重读判等，按目标跳过；几何、实际包或配置变化仍下发。仪表盘force可重发。电视和墙面电源动作在同一门禁内先使对应路由失效，防止交错保存成功记录。
- 保留原系统音量、媒体、摄像机和失败中止语义；墙面跳过不等于跳过整案。TV左成功右失败保留左侧记录，明确重试只补右侧。
- T011：两电视独立保持/切换及方案A选源，保存/编辑/JSON/YAML一致；旧预案保持。深拷贝两台输入，不对Vue Proxy做structuredClone。现场参数重置不擦掉电视；摘要使用短IP。
- 前端“从当前状态生成”先只读GET显示记录并绑定主机/账号/页面/编辑意图；仅sent_unconfirmed/simulated可捕获，unknown/uncertain/dispatching不借旧last_sent或runtime模式补造当前布局。后端Capture也遵同样逻辑状态边界。
- 同源窗口别名与自定义IP的静音策略按受信解析后的单播地址统一，避免IP63等价于窗口1时误静音；显式配置优先，不改实体声卡或设备音源。

## 已执行

- 原PE纯内存构造与独立编码器186向量一致，详见research；未加载附件EXE/DLL。
- Infrastructure：TelevisionPacketCodecTests/TelevisionFrameWriterTests合计10/10，含黄金帧、源尺寸/校验排除宽高、未恢复模式拒绝、同流顺序、部分失败和等价来源静音。
- Integration：DisplayRouting*与ScenarioDisplayRoutingTests合计30/30，含65并发一次发送、持久重建/别名/force、未知/配置变化、逐目标失败、启动恢复、发送前后SQLite保存/提交回执故障、完整场景YAML、原声音/媒体继续执行、正式HTTP/CSRF/75拒绝，以及Hardware分支真实控制器装配但不调用Dispatch。
- 相关Integration广回归曾65/65；ControlHost场景/设备相关25/25。原提交故障测试明确定位第二个事务（写出后记账），仍保留原物理替身/音量/媒体严格断言，并新增首事务失败零外部动作回归。
- 显式Category=Physical的TelevisionLoopbackTests 1/1：仅127.0.0.1真实Socket，收齐35字节黄金序列且无第二连接。不把此回环当161/162出画。
- 前端scenario-editor-dom/scenario-capture/scenario-televisions三套35/35，含真实Ant内存DOM、保持清除旧源、另开新建/同ID重登的迟到GET、两层深拷贝及旧字段默认；未模拟OS或真实浏览器输入。
- 已安装vue-tsc直接运行退出0；git diff --check通过。pnpm exec曾因v11自动依赖状态检查触发install而被NO_TTY阻止purge，未删除依赖、未用CI强行覆盖。

## 失败取证与边界

- 旧提交故障两项最初命中新增的发送前事务，故障发生在首包之前；改夹具注入位置而不放宽生产门禁，另增首事务失败测试。TV故障四项曾仅在Dispose锁库：测试独立连接串建立第二SQLite池，改仅该Factory Pooling=false后通过，没有全局清池或修改生产。
- 一项DOM选择器误用含空格文字，而visibleText会移除空白；只改选择器，产品未为测试降级。
- 相关旧GridPlanner的窗口3/4拒发断言与新需求冲突由根agent处理，不在本记录冒称最初全套全绿；最终全量、Redocly和真实浏览器视觉验收由T017统一报告。
- 当前除IP81既有1080证据外，固定表4K来自原工程声明及D4要求，不是本轮SPS/现场实测。输入仍经受信目录和显式配置，不从任意IP猜源。
- 本轮没有访问D4或向现场发包、操作电源/云台/声卡。电视实际出画、所有设备源尺寸和现场长稳均未验证。未提交Git、未推送部署。
