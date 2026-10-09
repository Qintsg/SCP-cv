# 2026-10-03：实际文稿布局预览闭环（T040）

范围仅本机。D4继续暂停，未推送、部署、启动或操作电源/墙面/系统音量。
原件、数据库和历史失败记录保留；本轮没有启动LibreOffice或PowerPoint。

## 后端读取与制品门禁

两个GET统一单值`fit_text=true/false`，缺省false，非法/重复值400。
图片URL保留所选布局；有效制品无持久页行时生成id=0/源登记时间的只读资源，不写库。
缺所选版时可保留既有备注/媒体，图片为空、PNG404，不借另一版或历史row图地址。

独立PresentationPreviewArtifact模块显式接收受管理根，复用既有摘要/修订/安全数字规则；
检查页数1..500、当前目录精确连续页数、root到制品目录及每个页文件无重解析点、长度>8与PNG签名。
`text_layout`提供时须为string且匹配所选键；历史缺声明仍沿明确原/fit键兼容，不重新约束摘要为64hex。
坏类型、路径/IO/权限异常返回制品不可用，不泄漏元数据中的路径，不重写metadata或删除原件。
这是属性/签名快检，不是完整PNG解码或校验后恶意文件替换的原子安全保证；root以上祖先未由该检查证明。

生产列表及add/upload/move/update/prepare返回均从配置对象明传根，不从URI或连接串推导。
列表默认缩略图来自有效原排版制品；fit-only列表不借fit冒充默认版。
旧无root静态ToSourceDto调用仅保留metadata-only页数兼容，不广告PPT图片，不是文件可用证据。
缺页后的显式prepare沿同门禁重排新作业；旧running/uncertain仍拒绝盲重放。

## 后端实证

所有坏条件仅在自有Simulation fixture中构造，结果通过真实认证HTTP观察，不操作用户库/媒体。
正式上传→Claim原版→明确Fail→Retry fit→确认配方→Publish，两页fit-only资源及PNG字节正确。
以下业务先红记录保留在`.validation/008-preview-layout/`：

- fit-only-red.trx：Expected2/Actual0资源；fit-only-green.trx修后通过。
- missing-page-red.trx：第2页丢失时第1页仍200；全manifest门禁修后两项通过。
- page-count-red.trx：元数据2→1仍有两PNG被放行；精确数量/连续名称修后通过。
- library-red-safety-green.trx：两个列表外链/错源地址红；明传根与canonical地址修后通过。
- preview-repair-red.trx：丢页后prepare返回空状态/复用旧成功；修后Library7/7。
- layout-declaration-red.trx：四种布局声明冲突仍200；修后四项绿及两历史缺声明正例绿。

最初文件SymbolicLink创建缺本机特权，是能力失败而非产品红，原TRX保留。
改用自有目录junction（精确mklink /J、读ReparsePoint）实证拒绝；仅非递归移除link后恢复自有目录，
未改开发模式/系统权限。文件符号链接分支本机未实测，不将目录junction扩大为全部类型证明。

总42条Preview HTTP回归与7条Library/修复闭环通过；含非法query、原/fit字节归属、缺版、
旧修订/空摘要/大小写兼容、数值/根/类型畸形、坏路径/越界、PNG空/仅签名/错签名等保护矩阵。
保护项若复用已正确规则直接绿，未伪造为独立红。CA1068重排参数仅属构建修正，不计业务红。
最终全非Physical876/876、零skip：Domain41/Windows86/Contracts178/Infrastructure127/Integration212/Host232，
六TRX在`.validation/008-preview-final/`。先前870版本及各红/能力失败均保留。

## 前端实证

两页面共用实际预览加载器，取actual_source_id、bool actual_fit_text及实际文稿adapter；
上下文含window/adapter/source/layout/observed_generation，不将普通心跳时间当身份。
未知或完全缺实际观测不猜desired/default图；失败换源的预览和专注标题仍对应旧实际文稿。
SourceThumbnail可显式禁止借源默认缩略图；fit-only显式页图不依赖默认preview_kind。

初始加载、有限8次轮询、旧成功/错误/loading结束、A→B→A、同源重开代次和卸载统一失效；
HTTP200无页图用info明确静态预览未准备，不改原生状态；刷新按钮只GET，不重开/换页或重置自动预算。
真实Vue/Naive/Pinia/API通过外部fetch与一秒任务时钟验证，22条DOM回归通过。
首两请求红、专注标题红、无图提示两红/同源代次红、刷新入口两红均有本轮命令证据。
其余迟到/轮询/unknown直接保护绿，如实分开。

首次全前端121/122：夹具把Naive Button的1000ms Wave动画计成第9轮询；
查本地Wave.mjs和Button调用链后只修QA判据，触发全部待处理一秒任务，以公开资源GET仍为10核预算，
不按私有callback/stack过滤、不修改生产预算。最终前端122/122、零skip；typecheck/build:web通过。
主包1098.12kB分块告警仍保留，未通过抬阈值隐藏。

## 真实浏览器与收尾

IAB自有tab3、127.0.0.1:20883回环夹具，真实组件/路由/主题provider；API模拟且不代理，
SVG仅是有标记的测试占位，不是PPT转换/格式/动画/实体出画证明。
桌面控制页原/fit切换取实际40，专注current/next/rail均为40-fit；标题修前“期望41”截图保留，修后实际40。
404错误与200空图均不借默认缩略图；390控制页业务元素无超界、body宽度/scrollWidth均380。
390专注页在viewport390内无业务超界，按钮/标题可见；首轮夹具缺Naive主题provider造成暗页暗字，
仅补既有生产provider后复核，未将夹具装配问题误判为产品CSS缺陷。

专注页缺图8次补齐结束后，模拟制品完成而会话不变，再点实际刷新按钮：资源GET12→13，当前/下一页/rail恢复。
手机控制页200空图，模拟完成不改会话后点刷新：资源GET4→5，监看恢复40-fit；无API写请求/Native动作。
日志error/warn为空；截图在`.validation/008-preview-browser/`（focus-final-desktop、control-missing-mobile-final、control-refresh-mobile-final等）。
恢复viewport、关闭自有tab；PID21048/start2026-10-03 05:10:18/父66788/命令核对后停止Vite，20883无监听。
原件AllinOne/TestPPT SHA不变，自有Office/TestHost进程为0。浏览器一次性夹具无新增可泛化站点事实，既有站点笔记不变。

T040的预览闭环不替代T043运行依赖、T044导航完成、T045原生裁切/最终长稳或T046完整包门禁。
