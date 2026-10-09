# 主序重复安全限制与多源选择状态（2026-10-05）

## 风险范围

沿T071/T072执行。此前两份D4原生AV、官方符号与堆状态证明主序SEQ重复第二轮时，
`mnFinishedChildren == maChildren.size()`仍访问末尾外元素；末个PAR组重复对照则正常退出。
固定版本父容器一轮结束先减1，再以剩余>=1排第二轮，所以有限次数边界是>=2，
不是所有大于1的分数。依据
[顺序容器](https://raw.githubusercontent.com/LibreOffice/core/libreoffice-26.8.0.3/slideshow/source/engine/animationnodes/sequentialtimecontainer.cxx)和
[父容器](https://raw.githubusercontent.com/LibreOffice/core/libreoffice-26.8.0.3/slideshow/source/engine/animationnodes/basecontainernode.cxx)。

正式保护只检查非空MAIN_SEQUENCE SEQ的UNO double RepeatCount>=2或Timing.INDEFINITE，
在追加空尾和原生放映启动前拒绝。单轮、void/其它类型、1.5分数、叶子/并行组重复不由此拒绝；
没有依据泛拒RepeatDuration、所有Restart或交互动画，也没有证明其它复杂结构安全。
不保存/改原件、不删动画、不自动回退，具体原因提示用户手动选择静态页图/PDF。
候选只有文稿正常清理后返回failed；清理不确认仍沿既有uncertain，不盲重放。

## 回归

次数2、无限各业务红→绿，另5项非重复保护，7/7。
发现Player adapter丢宿主ErrorDetail；公开Player→真实外部stdio拒绝红→保留有界detail→2/2绿，
双清理失败仍保原因/uncertain。首断言误写夹具文案不算有效业务红，随后用真实“外部夹具明确拒绝打开”再次红并留TRX。
IPC/HTTP结构不改，不新增堆栈字段。
本轮完整非Physical1114/1114、0跳过：Domain41/Contracts310/Infrastructure127/Host269/Integration240/Windows127。
首次solution统一TRX名被覆盖，仅最后Windows文件保留；六项目工具结果通过，不冒称六份独立TRX。
随后同版逐项目独立TRX再次1114/1114、0失败/0未执行，六文件保存于`.validation/008-repeat-guard/full-final/`，
计数41/310/127/269/240/127与首轮一致；旧覆盖事实保留，不把重跑文件当首轮原件。
正式Framework与QA构建0警告/0错误；首QA审计源NU1900保留，既有独立audit.config及进程代理协议修正后正常，审计未屏蔽。

## D4实际Player

SSH只读确认admin console1在线、项目服务停止、源码清洁；旧防火墙通知仍存在，未操作许可/策略。
仅窗口2 DISPLAY3的800×450矩形(2000,80)-(2800,530)，控制DISPLAY1与其它输出不动。
QA宿主只在只读模型加载后注入RepeatCount，其余保护/空尾/启动/关闭/协议链接本轮正式源码。
这不是自然作者完整重复语料或正式发布包。

首parent11044因QA IntPtr不能JSON序列化失败，未打开Office。
第二parent11120明确拒绝main2后，QA误把图片不适用的null周期采样当身份消失；只修QA，不改生产采样。
失败日志/包保留，均不当业务绿。
最终新根`D:\SCP-cv\.validation\008-d4-repeat-guard-final-1791131800773`，
parent9464/start UTC2026-10-04T16:38:07.7066398/session1；task/parent正常0，结束16:38:50.6027595Z。

- main2与INDEFINITE各注入2个非空主序，被正式保护拒绝，未进入slideshow_started。
- 两次拒绝后旧图原PNG和SHA不变：`4E48A3CF93D6B69929EA1413CCE3CA72F8186F910CD8BD53B0E79B79802A97B5`。
- original/group2均第6页打开，4次效果控制页序6/6/6/7，正常关闭。末图是可读第7页，不证明每帧动画/末页noop。
- 关闭图全黑；PPT98756629字节、图片594958字节副本独占读通过，PPT原件/副本SHA `0BBAFC49B6F155F11EA27205B7A9A792AE253B5E423A115A6AB3E8E238F25C22`保持。
- Client Dispose正常确认，末核OwnProcesses0/ProjectServices0。无限候选宿主shutdown时曾记录helper13444暂留，后续仍由既有完整job预算确认；不凭单条active计数判终态。

QA Host EXE `DB178ADC0264D1F0B2C8BC7F6738FA51ADBFED28D4815ED7F214EC5CC7BD647D`，
Player DLL `1A1AE7F7A6334C716C72B54B879B2A294AAA7F162953F5433E3B24E5B6B836CC`；
正式Host EXE `B41AAC96F12CF7E7F2EF0613296F4A1EB7E879FD8E847E4573D643F106E476D3`，不是同一EXE。
证据ZIP `1B8C3A54B1041A7857297F0A412803B4AD74283807DB70B3662A83B61DBCFC48`本远核SHA，
拉取保留`.validation/008-repeat-guard/d4-final/`。三个已完成自有任务精确删除，包/副本/失败证据不删。
未操作电源/墙面/音量/防火墙/执行策略/业务库，QA上传不当secondary源码同步。

按pptx技能要求独立视觉复核7图：旧拼贴图布局相同，细报纸字在800×450难逐字辨认；
第7页标题/小字无重叠裁切，两闭图全黑；effects底部有黑带及轻微彩色抗锯齿。
独立复核目测“约24px”不能当像素事实；随后只读原PNG逐像素统计，两effects均仅2条全宽RGB零行，
两closed则450行全黑。更正估计，不改图/生产几何、不称满幅验收；小差异登记后不继续追求逐像素保真。

## 前端多源

SourcePicker此前用请求source_id标当前项；公开DOM在desired视频52/actual图片51时旧源aria-current空，业务红。
改为复用resolveActualOutputSource，以实际ID/适配器/库类型取当前输出；请求另标处理中/失败/未确认/离线。
离线保留“当前输出（离线，最后报告）”，不当实时出画，明确空actual不借请求源。
新增9项，相关15/15；图片/网页/视频/RTSP/SRT/自定义流/PDF文稿及空输出/离线。
首改动误用不存在byId getter渲染失败保留，改实际findById后绿，不屏蔽Vue错误或替换组件。
完整前端254/254、0跳过，Node24/pnpm11.22，类型/Web构建通过；主包1106.12kB/gzip315.33kB告警保留。

真实浏览器独立20891回环模拟HTTP，真实SourcePicker/PlaybackControl/Pinia/Naive，不连接D4。
390 pending旧图/请求中、RTSP失败旧实际项，1440离线两按钮禁用和最后报告通过；
documentWidth380/1430小于viewport390/1440，无横向溢出；Errors0/Warnings0。
原图`.validation/008-picker-browser/{mobile-pending,mobile-failed-stream,desktop-offline}.png`已读核。
只关闭自有tab、恢复viewport、停止精确Vite PID73296/start16:37:36.1473455Z，20891无监听。
仅前端状态/布局证据，不是RTSP实体解码或出帧。

## 剩余

T071仍欠其它危险组合/自然作者样例、旧动态输出/另一窗保持及完整失败矩阵；不勾完成。
T069有主序末页、T072全能力/错误通知、T073登记API/UDP恢复/音频HTTP竞态、
T074正式同版包两窗混合10轮/100控制/60分钟和secondary源码同步不据本切片完成。
允许小PPT限制不豁免崩溃/挂死/不确定清理。shuorenhua仅用于文档和汇报措辞。

本轮Spec Kit、git diff --check及改动代码/新增证据文档的严格UTF-8、无CR检查通过。
最后监听查询没有20891结果，其命令返回1是“未找到监听”，不当构建/测试失败；服务未重新启动。
