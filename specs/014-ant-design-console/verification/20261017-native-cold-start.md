# T023 原生冷启动与封装端补验

日期：2026-10-17。仅本机，独立userData/session/crash目录；不碰原控制端资料、不连接D4。HTTPS端点为回环内存夹具，不是ControlHost或播放器实机。

## 原始失败与修复

- 当前Electron44.2.0运行生产app://包，空配置连接页仍自动探测`http://scp-cv:8000/api/auth/csrf/`及`status/`；两个逻辑GET触发6条renderer CSP错误，不是6笔实际网络请求。原策略已拦截，不能改CSP掩盖。
- 正式API曾从页面协议/主机或编译代理地址推测封装端后端；根App无条件调用Auth初始化，绕过了路由中已有的“先选择主机”约束。
- 先持久化实际app模式/API/Auth/Runtime测试：针对性18项含4项原测试，初轮6绿12红；旧8000回退断言据真实失败纠正，不把原错误行为保留为合同。
- 共享`platform/serverSelection.ts`控制空配置入口：自动Auth保持未知，显式认证、REST（含绝对URL）及上传在派发/创建XHR前明确拒绝，SSE保持关闭。App继续复用Auth，不只在页面过滤异常。
- 不用编译上游替代用户选择。网页开发/生产仍同源；明确选择的HTTPS及受控回环HTTP不变。CSP、沙箱和受限桥没有放宽。

## 实跑门禁

| 层级 | 结果 |
| --- | --- |
| 针对性真实模块红→绿 | 18/18；新增14项，空配置零派发、未知身份，网页和已配主机正对照通过 |
| 完整前端 | 775/775，0失败/跳过/取消，无Vue/Ant警告或结束后异步异常 |
| Web/App完整类型与生产构建 | exit0，既有Vite扩展名提示保留，不关闭诊断 |
| Electron主进程TypeScript | exit0 |
| SFC、Spec Kit、diff | 54组件无失败；规范与差异通过 |
| 网页生产构建操作 | 10组、9笔内存业务写入，0未知错误/警告；非设备指令 |
| 网页页面矩阵 | 56组，0错误/警告/横向溢出/外网请求 |

## 真实Electron与HTTPS

不是旧release目录的可执行程序：使用本机声明的44.2.0运行时和本次dist-app/electron-dist。缺失运行时通过pnpm显式`install-electron`从淘宝Electron镜像安装；归档SHA256与随包校验表匹配。Windows Authenticode返回NotSigned，没有宣称签名验证通过；未关闭TLS、校验或改供应链审批配置。

隔离探针加载正式main/preload和生产app协议，唯一窗口被动显示、网络只允许app资产及127.0.0.1的18882/18883；不修改HTTP、IPC、CSP或认证逻辑。不是对原用户窗口操作，也未增加业务桥权限。最初隐藏窗口可读DOM但无法完成截图，后续被动显示后实际截图成功；不把该工具等待失败算产品崩溃。

- 空配置实际进入`app://scp-cv/#/connect`，初始15笔均为本地资产，0认证/API派发，0renderer错误。
- 实际webPreferences为nodeIntegration=false、contextIsolation=true、sandbox=true、webSecurity=true；页面没有require/process。桥只暴露既有8项平台/文件/会话/主机/生命周期能力。
- 使用用户已有SSPU108证书的127.0.0.1 SAN与本机已有根信任，正常TLS的curl返回200、verify=0；Electron请求也成功。未安装/删除CA、未复制或输出私钥、未使用ignore-certificate-errors。
- UI保存HTTPS A→登录→工作台→账户退出→登录A→切换HTTPS B→登录B→重载工作台；共3次假账号登录POST、2次退出POST。实际CORS Origin为app://scp-cv，受保护GET/SSE携带HttpOnly会话Cookie。
- 通过既有IPC实际保存/读取A、B配置，独立目录的server-profile.json与页面一致。重载后选中的B及其登录会话正确恢复；不是Windows重启或安装包验收。
- 原生分区Cookie计数为0→1→0→1→0→1→0。最后直接调用既有clearSession IPC，再重载，状态接口确认为未登录、回到登录页、密码为空，B配置仍保留；不把服务端会话已撤销与本地Cookie清除混为一谈。
- 最终0renderer错误/警告、0不允许的出网；HTTPS夹具所有业务写入均禁止，实际业务写入0。播放器在线/媒体数据均为假数据，不是现场确认。
- 本轮测试窗口通过自身window.close正常结束，报告含before-quit；资料目录与截图保留，不删除用户资源。

## 证据与边界

忽略目录`.validation/ant-console-014/`保存native-cold-red/green、native-all、native-build-web/app/main、native-web-functional-final与native-web-visual-final日志。`native-cold-20261017/`保存baseline-r1、final-r3、https-report，以及cold-fixed、dashboard-b-reload、login-after-native-clear截图。旧隐藏截图等待/解释器缺依赖和离线解析失败保留，不计为产品失败；最终网页复用uv管理调用的本机既有Playwright环境，无新增依赖。

本轮补验不证明ASP.NET/跨设备网络Cookie、安装包签名、原生文件选择/保存对话框、真实OS焦点/最小化恢复、Android、PPT/VLC或实体声音/画面。012现场欠账与暂停摄像机不变；未推送或部署。
