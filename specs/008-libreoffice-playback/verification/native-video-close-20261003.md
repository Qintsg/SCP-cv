# 实际Player视频、换源、关闭与图片量化更正（2026-10-03）

## 范围与结论

正式PlayerWindow/PlayerRuntimeHost及生产候选容器，无PresentationFactory替换，侧车明确来自最新本机包`008-package-current-9ce5436f55854ec6b07e1c1e5c74dca7/libreoffice/`。
只读TestPPT副本，原排版fitfalse、第3页，在自有800×450/DPI96小窗公开NEXT_EFFECT触发已有媒体；不启动正式App/监督运行组或连接D4。
两组后期原样图均显示真实人物动作和模型屏幕变化，先期两图的视频矩形仍黑。只证明这些有限帧的解码/显示，不证明立即就绪、音频、媒体时钟或所有格式稳定。
播放期间CLOSE及播放期间OPEN AllinOne后再CLOSE两组均正常退出；后者新源source12/generation2/current2/total9与图像一致，原视频没有可见残留。
T059有限样例完成，T013/T018/SC-003完整门禁、统一媒体时钟/控制、两输出/长稳仍未完成。

## 实际运行与归属

所有capture及run证据位于`.validation/008-native-player/`，保留原始stdout/stderr/events、副本/profile，不覆盖历史失败。

| capture根 | parent/host | Office launcher/listener、port | 正常退出 |
| --- | --- | --- | --- |
| capture-df5a1160fb8a46dfbcd291f68449829c | 47360/41560 | 64976/50656、29384 | exit0，22.11秒 |
| capture-22521339e95f405684d9f82747f858fc | 33832/68856→70260 | 56728/4692、41323；22024/10548、41337 | exit0，29.65秒 |

运行目录分别`run-d800347e649c42f2bedd52cfaa7540fa`及`run-66b16eeb592145b18e4ab5b5caac573b`。
只恢复本轮子环境Host路径；原30秒请求/15秒关闭/5秒换页预算不变。外部90秒看门狗没有到期，不用强杀当正常关闭。
三个host job均日志active0/members空、document_closed/termination_requested；收尾另核精确PID和三个监听已消失。
第一组TestPPT关闭后、第二组TestPPT换源后均FileShare.None成功；后续收尾再次独占读两份TestPPT及AllinOne副本成功，AllinOne的后关闭锁证据属于收尾检查，不是原程序阶段断言。
原件与副本SHA保持：TestPPT为`0BBAFC49B6F155F11EA27205B7A9A792AE253B5E423A115A6AB3E8E238F25C22`，AllinOne为`2401652F7CD610DFCDFE4B5F2F32CD112D9645F1E30CEF44ECB3D716B82A55F0`。

## 画面，不以SHA变化替代视频证据

原图位于`C:/Users/qintsg/AppData/Local/Temp/SCP-cv-native-player-shots/`：第一组`5bfaa19f9b2d417e882bdfab3644379f`，第二组`bbe9ebb737b34280a95c54f5c2124096`。
各有video-before-effect/early/later-1/later-2/after-close.png，第二组另有replacement-page.png。
root逐张核第二组六图及第一组五图；PPTX技能要求的独立视觉agent也分别核原图：后期从坐姿背对镜头到左侧站立操作，模型视图改变；换源显示研究图表页，关闭目视全黑。
未见菜单、导航、任务栏或播放器控件。视频内部红字贴右缘及换源图中图形近缘仅为观感，未据此改原件/布局或声明保真。
两张after-close原PNG以System.Drawing只读逐像素核800×450、A255且RGB0，共360000像素全黑、完整SHA相同`C9A9948FB1933D31705B5D39E6799CCB7847FC2A75D0F26239BB3A678AEE261B`。
关闭后自有Office全部退出支持该样例媒体不再由此实例运行；没有独立音频端点/时钟测量，不扩成所有媒体证明。播放样本position/duration仍null，统一暂停/恢复/定位及音量/静音能力不变。

## 更正：工具图像中的黑带粗估不是原PNG量化

前轮T058及本轮目视报告分别称底部约35px/约一成黑带。重新只读原PNG发现不支持这个厚度，应撤回，而不是作为新产品缺陷或声称已修复。
T058两个目录的四张first/second-effect全部800×450，最底连续全宽A255/RGB0仅2行（y448、449）；中部y420为A255/RGB255。
本轮两组video-later-2及第二组replacement-page同样仅2行；第二组中心x400的y390..447逐行白，排除“近黑而非零”或alpha透明解释。
T058四图未覆盖，两个first的完整SHA同为`EFB5F508E2EAA27FE6031345FE09984A6B4896B1CB3D27AD0ECEC5366A31555E`，两个second同为`35E40F00D61BB9A3AF381FA70A59B5AE6BD33873766F1001B928B3F6DCFD968D`。
这是视觉粗估与原文件像素的矛盾更正；工具呈现的具体原因未定位，不猜测产品几何或平台渲染原因。没有修改图片、Office承载或几何。
两行黑色也不能单独证明内部完整viewport、任意宽高/DPI无裁切；早期不同WinForms承载的70px报告/三个失败几何实验不被本次更正反向抹除。T045继续完整显示门禁及承载讨论。

## 收尾

原件、数据库、历史失败/dump/图和最新包保留，无新生产依赖、生产代码修改、系统音量/墙面/电源、D4访问或部署推送。
本轮QA构建有旧assets NU1900警告，0错误；不禁审计，不将旧警告改成正式包当前失败。全后端1002/前端177是此前结果，本轮未重跑，不能作为这些实际媒体门禁的自动化数量。
Spec Kit/diff/API校验与当前文档同步另做；复杂/末页、媒体时钟、用户Office共存、全组/真实双输出/60分钟仍未验收。
