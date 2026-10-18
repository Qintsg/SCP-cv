# T046 音频应用身份隔离（2026-10-18，本机）

## 状态与范围

已完成本机软件修复与分层验证，未推送或部署D4；D4仍按上次正式发布记录运行。本轮不使用Computer Use或模拟输入，不控制现场墙面、摄像机、机位、DSP、音源、电源或正式播放内容。012共46项、36完成，原10项现场/人工欠账不改。

本机仅修改自有短寿命诊断进程的桌面应用上下文、创建自有WPF/环回网页并调用正式Player接口。没有更改系统默认输出或总音量，没有授予用户页面脚本手势。无包API矩阵会绑定本轮私有WebView映像的应用输出，这是明确的自有策略写入，不称作只读测试。

## 失败与定位

此前[手势夹具](20261018-local-web-gesture.md)返回未通过：真实点击未执行，自动播放NotAllowedError，媒体时间0；读取的默认端点前后不同。不能据此直接判定系统全局默认改变。

Microsoft说明Windows默认输出选择会优先考虑应用偏好，再考虑系统偏好，参见[默认端点选择](https://learn.microsoft.com/en-us/windows-hardware/drivers/audio/default-audio-endpoint-selection)。因此本轮先只读比较作用域，不运行Bind或改变音量：

- 原命令行进程继承`OpenAI.Codex_2p2nqsd0c76g0!App`身份，Console/Multimedia有效端点为29d。仅为当前短寿命进程设置随机显式AppID后，继承的应用身份并未消失，应用策略读回仍为29d；显式AppID不是隔离方案。
- 以SDK与[微软文档](https://learn.microsoft.com/en-us/windows/win32/api/processthreadsapi/nf-processthreadsapi-updateprocthreadattribute)中的桌面应用上下文策略创建自有broker，再启动同一dotnet映像的只读子进程。实际GetCurrentApplicationUserModelId返回15703；Console/Multimedia仍是原886，Communications为29d，三个应用路由读回为空。
- 两轮原上下文/独立上下文结果一致。GetPackageFullName的实际测试又独立确认有包分支与无包15700分支。这定位了共享包身份下的应用作用域污染；不是已证明的系统全局默认变化。
- 最初未设置显式AppID的非发声只读进程查询路由返回80070057，按读取失败记录，未当作空策略。没有把失败吞成“未绑定”。

原生产策略只核PID、会话与私有路径，未核包身份。继承同一外部应用身份时，私有路径并不足以保证策略只作用于项目；这是FR-012/US4-AC5和私有引擎隔离方案的缺口。

## 修复

- `NativeApplicationAudioPolicy`在策略激活前核当前进程，Bind持有实际目标原句柄且匹配路径/会话后，再独立核目标进程。只接受GetPackageFullName明确返回15700；0/122视为有包拒绝，其它错误保留原Win32码并失败，不以读取不确定授权写入。
- 普通网页与屏幕共享共用的Browser入口在创建私有引擎前预检；Office动态播放在暖机前预检。ControlHost既有策略构造能力检查同样失败关闭。converter和未配置显式网页输出的原行为不变，未改VLC逐资源端点绑定。
- 不在生产引入桌面上下文启动器，不设置用户页面手势、不放宽autoplay、不写系统默认/总音量，也不清空全局应用策略。以后若支持包内原生部署，需要另行证明身份隔离，不能用改AppID或关门禁绕行。

## 验证

真实Windows回归先红：`ActualCurrentProcessIdentityMustMatchPolicyIsolationGate`在有包环境预期拒绝，但原代码无异常。修复后新增12项回归在有包与独立无包环境分别全部通过。无包TRX明确记录current/target的原生状态均15700；不是只测拒绝分支。

完整`dotnet build runtime-dotnet/ScpCv.sln -c Release --no-restore -m:1`实际0警告0错误，包括Framework4.8 LibreOffice链接。随后串行`dotnet test ... --no-build --no-restore --filter "Category!=Physical"`实际1615/1615，0失败、0跳过：Contracts345、Host468、Domain41、Infrastructure206、Integration283、Windows272。六份独立TRX计数已逐份读取，不由最后一套覆盖推算。

无包正式Player接口矩阵另证正常路径未被一刀切禁用：

- 本机原进程23648、独立profile、环回35082，调用真实OPEN网页701；正式零采样暖机完成实际声音进程绑定与WASAPI端点复核。
- 两轮image702→保温web701，最后CLOSE完成；六个实际状态依次为web/image/web/image/web/idle，关闭后source_id=null、视图0。没有模拟按钮点击或把脚本输入当真人手势。
- `mode=binding-api-matrix-only`、completed=true、failures为空；6个捕获原生句柄全退出0。原broker实际退出0；OS核精确私有路径及本轮CLI无存活，35082/49491无监听。
- 从无包上下文读到的系统三个默认角色、两活动端点总音量/静音与初始基线逐值一致。`genuineGesturePassed=false`，用户内容仍自动播放被拒绝；只证明绑定与生命周期，不证明普通网页实际有声播放、现场扬声器可听或D4完整验收。

本轮没有前端/HTTP合同变更，未重跑前端1029项或Redocly，不将历史结果当本轮新验证。提交前Spec Kit全目录和diff检查均通过。

## 清理与剩余项

自有窗口/浏览器/CLI/监听已正常结束，保留私有副本与证据，没有强杀无关应用。初轮绑定留下继承应用身份的Console/Multimedia覆盖；初始原策略未记录，不能精确逆转。用户随后明确授权“允许恢复跟随系统默认”，据此仅清理该应用的指定两角色，不声称还原未知的历史专属配置。

一次性自有清理程序先核确切Codex应用身份、已验证Windows版本与当前两项仍为本轮29d覆盖，再逐项清空并读回。实际changedRoles=[0,1]、before=[29d,29d,空]、after=[空,空,空]、completed=true、failures为空，通讯角色不改。只调用按当前自有PID/明确角色的应用策略设置，没有调用清空全部应用或系统默认/音量写入。清理不属于生产功能，不修改生产包身份拒绝门禁；忽略目录中的一次执行闩阻止重复运行。

清理前后独立无包探针的三个系统默认角色、两端点总音量/静音逐值一致；清理后的原Codex上下文也读回Console/Multimedia为原886、Communications为29d。再次OS检查本轮CLI/私有原生及35082/49491监听均无残留。应用偏好恢复跟随系统，不代表对其它正在运行的音频流做了重建或可听验收。

D4原10项仍包括普通网页真实用户手势、人工共享质量、暂挂摄像机/既有位置、DSP与真实来源；本轮没有关闭这些任务或扩大部署授权。

后续新的本机运行已由用户本人点击，trusted事件/实际媒体推进/指定非默认会话及两轮切换/关闭通过，见[真人点击验证](20261018-human-web-gesture.md)。本页API-only运行的genuineGesturePassed=false保持原样，D4普通网页真手势仍未验。

## 原始证据位置

忽略根`.validation/web-gesture-012-20261018/`：

- `runs/context-20261018-033648-362bc96119a346c895bb63ae11909d91`及`context-20261018-033808-f3647c88143745a88ea61893e3e6e4e2`为第一组；`context-20261018-033859-f35dc4585dc743a4a9c6d0ba237386ae`与`context-20261018-033859-036b39383cee446ba34c70a3ad9378c8`为第二组。
- `identity-tests-a152773733af44e7aa2ef3137df53711/isolation.trx`为无包12项；`dotnet-all/`六份TRX为完整1615项与有包状态输出。
- `runs/20261018-034637-7ede81bfc55340cbbef75f7e20f07b0b/{ready,current,result}.json`为正式接口矩阵，旧未通过手势结果仍保留在原目录。
- `authorized-codex-audio-restore-result.json`为用户授权两角色清理；`runs/context-20261018-035225-5a7a6c8d4ca04d0db3cd2c30d7cbea72`与`context-20261018-035256-019739b838c04b1499392cebe9a997cd`为清理前后独立基线。
