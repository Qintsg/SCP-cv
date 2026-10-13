# 2026-10-13接续：稳定性、原生声音与目录失败路径

## 运行组故障与修复

D4本轮入场只有ControlHost14488和Node23616存活，桌面会话1保持登录。昨晚23:31（香港时间）的日志显示：共享OPEN等待首帧触发TaskCanceledException，Player退出后Supervisor停止其余成员。不是根据残留PPT源推断Office崩溃。原日志复制保留在D4忽略的`D:/SCP-cv/.validation/remaining-012-20261013/before-recovery.*.log`。

独立核对无Supervisor/Player/Audio/MediaMTX/Office残余后，通过认证restart显式恢复原012f；不重放旧OPEN，使用真实CLOSE清除旧实际源投影。修复命令执行循环：只有其自身令牌取消才退出，命令局部截止时间失败回报后继续领取。真实命名管道测试先红后绿，后续CLOSE、原调用方取消、协作停机及结果重放均保持。

空背景列表在恢复后显式STOP实际完成，但旧Supervisor错误仍保留，复现了成功快照缺少error_message。修复为正常原生回执明确空错误、故障回执明确诊断；不在入队、重启或只凭Worker在线时伪造清除。

## D4软件声音隔离实测

实测使用正式PlayerRuntimeHost、VlcAudioAdapter及共享输出模块；自有隐藏WPF窗口无HWND，不占用额外显示器。原始短音增益0.005、实例音量10，仅三秒；不改系统默认声卡、主音量、墙面或DSP。通过原admin交互任务运行于Session1，完成后删除仅本轮辅助任务，无探针进程残留。

原显式module参数只更新下次模块配置，当前aout没有切换；原生MTA/STA对照均显示默认/空ID，否定了线程模式猜测。改用当前输出设备切换，并在五秒内以真实ID确认后恢复实例静音。依据[LibVLC原实现](https://raw.githubusercontent.com/videolan/vlc/3.0.x/lib/audio.c)与[LibVLCSharp接口](https://raw.githubusercontent.com/videolan/libvlcsharp/3.9.4/src/LibVLCSharp/Shared/MediaPlayer.cs)核对。

最终两个Player模块和Audio模块在63/64各三轮首次OPEN、健康同源/暂停恢复、STOP→PLAY、CLOSE通过；18条真实端点与进度观测，另两条缺失端点拒绝，无默认回退。夹具文件可独占打开，证明所有自有资源已释放。完整结果位于D4`.validation/remaining-012-20261013/audio-run-6/result.json`。

早期诊断包编译失败、原绑定失败及关闭夹具误读取空资源进度的失败均保留：CLOSE须核实际命令回执，空资源本来不产生普通VLC进度样本；没有放松播放/绑定断言或延长期限。该结果不代表WebView2、LibreOffice、真实话筒、来源路由或扬声器可听验收。

## 目录与固定监控

- D4原11源ID1/2/53/83–88/90/91入场已存在，全部保留；90/91为本轮入场前已有TestPPT和机械臂，不当作可删除测试夹具。
- 在独立`/qa-20261013-busy`上传自有92/93，两项保存到真实目录；92真实image播放期间，单项/批量删除和移动均拒绝。混合批次仅93移动到根目录，逐项结果明确且失败92仍被选择；文件摘要不变。
- 真实关闭92并确认后，页面搜索跨目录仅选两项，自有92/93删除成功，磁盘文件消失；随后删除已空的本轮目录，原11源保持。
- 独立自有94的原件持有FileShare.None只读句柄时，页面删除失败；解除句柄后核对记录仍存在、摘要相同，不用数据库删除冒充磁盘删除。最终清理状态另在部署记录登记。
- 固定监控76的正式源88在012f窗口1进入playing，actual_source_id=88、VLC适配器、目标/观测代次922/922，随后真实CLOSE。仍未核现场墙面像素或监控声音，未移动任何摄像机。
- 摄像机与音量手机390px页面的未知云台/话筒/来源入口仍禁用；不把禁用检查当控制功能完成。重新只读核对C:/CVS及原程序目录，相关IPC/Audio/中控命令表仍为空，没有新协议绑定证据。

## 上传交互

真实桌面错误通知层4000遮挡添加源抽屉默认2000，实际保存点击被拦截。上传抽屉调整到已有7500层；旧错误不删除，上传失败仍通过抽屉内Alert可见。真实组件DOM测试先红后绿，补充触控回归后6/6；不使用force点击或隐藏错误。

012g正式网页复核：旧拒绝通知4000仍保留，抽屉7500，保存按钮中心命中button，正常点击上传自有95成功；94/95随后正式删除且磁盘文件消失。390px无横向溢出，但按钮实际仅32px，追加44px触控补丁，桌面保持紧凑；该补丁只更新网页，运行包仍固定91d883b。

最终609ae2c网页补丁已部署，ZIP摘要`7308F74893B44DE659CC99DE68FDB8E4FF8BE8189F4DC768FC5CD2C7F8897B58`，index摘要`3A2813F7ACFB57ACFEB06A4D3F65A86E8E03CEF8AA27D223DA02DE7338D57511`。真实HTTP浏览器加载`index-Cv-WMlf1.js`；390×844选择自有图片后上传/取消均44px且中心命中按钮，未提交重复夹具，正常取消成功。1280px桌面按钮仍32px，两尺寸无横向溢出。正常注销200后关闭仅本轮标签；原about:blank保留。这里只验收上传页脚，不声称其它页面所有触控目标均已全面审计。

## 验证与边界

Release整套构建0警告/0错误，锁定还原通过。最终常规后端Domain41、Contracts337、Infrastructure176、Windows198、ControlHost446、Integration281，共1479通过，0失败/跳过，独立TRX保留在`.validation/remaining-012-20261013/trx/final/`。前端最终460/460及类型/Web构建通过，通知/触控6项红绿通过；保留既有构建提示。Redocly实际校验0错误、4条既有警告；Spec Kit通过，hooks文件不存在。

012g六进程及原Node在网页补丁前后PID/会话/启动时刻未变，HTTP80/HTTPS443严格TLS访问均200，后端Healthy；原11源保留，两窗online idle、背景stopped且无旧错误。网页旧版可从`.validation/deploy-012g/frontend-dist-before-touch`恢复，运行包及更早备份不删。T019完整目录流程勾选完成，当前34项任务27完成、7未完成；服务保持运行。

未完成：完整软件声音（WebView2/LibreOffice）、IP81/SCP-cv实体音频路由、云台/话筒型号与控制帧、现场共享文字清晰度和更长混合稳定性。不猜测协议，不用可选绑定、构建成功或隔离探针替代这些验收。
