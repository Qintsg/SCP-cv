# 实际Player快速点击、撤销、跨页与关闭（2026-10-03）

## 用户规则与有限结果

用户确认“快速完成当前动画后下一步，类似普通PPT放映”；沿既有T053内存主序空尾实现，不重新叠双调用/固定delay。
正式PlayerWindow/PlayerRuntimeHost→生产候选容器→最新包LO侧车，无PresentationFactory替换；自有800×450/DPI96小窗、只读TestPPT副本、第六页fitfalse，不启动正式App/全组或访问D4。
两个全新独占实例各三轮：冷OPEN后立即两次next、两次previous、三次next跨至第七页，下一轮GOTO6；六轮文字像素与有限页码全部符合预期。
最后GOTO6→首next返回后立即CLOSE，没有等待标题动画自然结束。空actual/idle/页0/布局null、原样关闭图黑、自有job正常退出、副本释放及原件摘要保持。
T060仅此实际Player样例完成，T053/T044复杂/末页/真正UI与完成语义、两输出/完整组/60分钟仍未完成；不能用这些短轮标整体目标完成。

## 先失败再调查，不把异步采样改成假业务红绿

首capture-1db119079ddd49a592b8810687ae4df2（parent39400/host63004、launcher64604/listener67392、port29313）正常清理但parent exit2，11.01秒，原失败保持。
两next后标题2059/副标题1721，两previous后0/0已通过；三next全部返回，第一次SampleProgress仍页6，QA复用了“整个1.5秒必须一直是目标页”的ObserveFor，在首样本立即抛异常并关闭，未取得后续时序。
代码追踪显示native gotoNextEffect异步队列后立即Observe，不具有动画跨页完成回执；该首断言不能据此定为丢失点击，亦不能直接判假失败。
仅QA新增ObserveTransition，原1.5秒观察窗口/250ms采样不延长，允许已知6→目标7，未知页/错误仍失败，预算内没见7仍失败；不再发点击、不sleep于连续控制之间、不改生产完成合同或预算。
两fresh实测每轮首次样本仍6（约13–15ms），第二样本已7（约285–295ms），随后稳定7直到原窗口结束；这是有限采样首次观测时刻，不是GPU完成时间或通用延迟保证。
因此首运行是“QA未容纳已知异步观测”的失败，后续取得时序证据；不是播放器本轮业务修复红绿，T044的响应/动画完成区别仍须收敛。

## 两fresh归属与计数

所有capture/run日志、原件副本位于`.validation/008-native-player/`，独立目录保留，不覆盖旧失败。

| capture根 | run根 | parent/host | Office、port | 结果 |
| --- | --- | --- | --- | --- |
| capture-990d9fab5a1449d68daf86450a5a4090 | run-374443cb858c4098b4fac388b7f0afd6 | 30740/40516 | 13172/12360、50317 | exit0，27.56秒 |
| capture-dc0f9b8d4dfc470fa17aba96b0f57559 | run-900be374f2a84c7fac1f257b658c5526 | 37728/17320 | 37344/58356、16908 | exit0，27.37秒 |

每组明确控制25：GOTO3/NEXT_EFFECT16/PREVIOUS_EFFECT6，另OPEN1/CLOSE1，不将Observe/截图计入控制。连续点击各为独立租约/序号，只await返回，没有自然等待、采样或截图插入。
组内截图之间1.5秒只为有限QA看像素，不是原生就绪/完成规则；原30秒请求/15秒关闭/5秒换页不变，90秒外部看门狗未到期。
正式DLL/侧车保持既有包，无生产前后端、协议、依赖/机器配置或Frame几何修改。

## 画面和原件证据

图片位于`C:/Users/qintsg/AppData/Local/Temp/SCP-cv-native-player-shots/af5e31bd6244427ca49c95aa88c2f387`及`ec46c45fb87248bf82e3b76d951fda80`。
各轮rapid-N-complete/rewound/next-page.png及after-close.png，共十图。只读宽文字区R+G+B<600：标题x80..429/y120..224、副标题x80..429/y245..309；完整2059/1721，撤销0/0，六轮一致。这不是OCR/通用版式/完整viewport判据。
root逐张核第一组第一/第三轮及关闭七图、第二组第三轮及关闭四图；PPTX技能要求的独立视觉agent核第一组七图，标题字幕→空白→第七页一致，无菜单/导航/文字残留。没有人工核完全部二十图或复杂文稿。
原PNG黑带厚度的只读量化更正见[视频记录](native-video-close-20261003.md)：不再引用35px视觉粗估，不据仅底2行判所有承载问题已修。
两份副本FileShare.None阶段/收尾都成功，SHA均`0BBAFC49B6F155F11EA27205B7A9A792AE253B5E423A115A6AB3E8E238F25C22`；Resources TestPPT/AllinOne原摘要保持。
两个正式host均document_closed/termination_requested及job active0/members空，收尾精确parent/host/Office PID与本轮/失败监听全部消失，不用强杀或全局进程名清理。

## 验证边界

新增仅ignored QA的RapidFrames与小参数接线，原失败未删；构建0错误但旧assets NU1900告警保留，没有禁审计。
另用`pnpm --prefix frontend exec node --test scripts/presentation-effects.test.mjs`复跑前端动画组件10/10、0skip：网络在途防重/错误释放/真实通知、受理不改实际页码、迟到REST不盖新SSE及卸载旧错误。没有前端生产变更，这些DOM不替代真实浏览器/REST到原生完整链路；全后端1002/前端177仍为上轮历史，本轮未冒称全量重跑。
Spec Kit与git diff --check通过；官方缓存Redocly2.55.0为0错误/3个既有退役410入口缺2XX告警。无extension hooks文件，无额外自动动作。
原件、库、profile、包、dump与日志保留；D4、电源、墙面、系统音量、部署/提交/推送均未操作。
末页完成后保留内容还是结束清空的用户选择仍待回答；媒体时钟/音频、自动/重复/交互复杂序列、双物理输出、用户Office共存、最终60分钟及全组仍欠。
