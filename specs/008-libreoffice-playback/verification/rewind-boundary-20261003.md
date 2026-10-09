# 撤销像素门禁、误报更正与动画末页（2026-10-03）

## 更正：此前副标题残留没有原图证据

上一轮formal31ec4a的初次视觉复核曾把undo-subtitle报告为仍有副标题。它不是实际产品失败，后续必须以原图而非旧报告为准。
重新逐张查看目录`C:/Users/qintsg/AppData/Local/Temp/SCP-cv-animation-shots/4424fdb8bbda471793f6454914db5e01`：
return-six为标题+副标题，undo-subtitle只有标题，undo-title全白；独立视觉agent重新核同三图确认旧说法应撤回。
文件创建/修改时间仍是10:52原阶段，未覆盖；title-only与undo-subtitle完整SHA均为7B4E94C2DDAC3E4246893B226644878F39703A297C31F1D0FACF13457DE6B57C，
before-effects与undo-title均为ABE061F658722A9D272075AF8882BFB64D7692244D579A98CD5FBD5BE8B4D5F7。
同一已人工定位字幕宽区域、R+G+B<600的深色像素计数为0，不存在旧报告所称的字幕。这里没有用户数据写入/图片编辑，也没有为该误报改原生播放实现。
因此不继续称“未定位偶发撤销残留”，相关任务/指南/维护/CHANGELOG/known-pitfalls均已更正；原始图、日志及报告历史保留。
这次更正不能反向证明全部文稿/所有时序/所有DPI下撤销稳定，只消除一个没有证据的缺陷结论。

## 四轮真实像素判据

公开Client/正式Host、自有Form800×450/DPI96、TestPPT副本及fresh独占profile。两新实例分别诊断ON/OFF，各两轮同一窗口。
每轮自然播放标题/副标题→页7→previous_effect回6完整→一次previous_effect只标题→第二次previous_effect空白，随后goto2/6再下一轮。
截图用screenshot skill的Windows助手，只捕获此自有窗口，保存于系统临时目录，读图不改图。截图前1.5秒仍只是有限采样，不是产品完成协议。
像素门禁先确认空白参考图；在足够宽、避开标题栏/边框的已知标题/副标题区计深色像素，不能当OCR或通用版式验证。
完整阶段至少标题300/副标题100，否则截图或显示无效；一次撤销保留标题且副标题≤空白+20，二次两区≤空白+20。
失败时只再做一次晚观测/原样截图，不补发第二次撤销、不擦首次失败。程序仍保留effects_visual_verified=false，有限像素判据独立记录。

| capture根 | 诊断 | 轮数 | 实测文字区 | 完整关闭 |
| --- | --- | --- | --- | --- |
| run-2a168ea35feb4569928df8b8450f52fc | ON/节点身份ON | 2 | 完整2059/1721→2059/0→0/0，两轮一致 | exit0，33.90秒 |
| run-8fc158c30012484687af032d4045ab42 | OFF | 2 | 相同两轮 | exit0，32.70秒 |

截图目录分别为`C:/Users/qintsg/AppData/Local/Temp/SCP-cv-rewind-shots/1785e963c4d545cf87893b4f572644e6`及`1e5fead019b24f00a27dafbb78c714c5`。
独立视觉agent逐张核OFF两轮，与像素门禁一致，无可见菜单/导航或文字残留；QA Form标题栏不视为产品编辑器。
没有原生代码修复，所以不能把这四轮绿包装为业务修复红绿。

## 新的真实失败：带动画末页黑屏仍被同页观测确认

隔离Framework QA Host复用正式协议/归属/关闭及播放实现，只在内存通过官方CustomPresentationAccess建立自定义放映：原第六页是唯一放映页。
原件仍只读、未保存；当前放映列表count=1/index0，真实模型页仍是源第六页，证明不能用源文件页号猜首/末页。
流程open1→有限采样→下一步标题→下一步副标题→完成后再next_effect。
运行`run-5191ad1ba7f04639815982018510dbc3`返回的CurrentSlide=1/TotalSlides=1/Confirmed=true持续不变，但图片明确变黑并显示“按鼠标左键退出演示…”。
文字区深色像素51064/38789，远大于内容截图2059/1721；像素断言失败，parent保持exit2，总15.16秒，完整自有job成员退出。
不是图片误读：root及独立视觉agent都核对complete与after-last-next原图。图片在`C:/Users/qintsg/AppData/Local/Temp/SCP-cv-boundary-shots/2052ca513f4a4bcf87464742e70505d0`。
状态合同只确认页码，不能据此说画面正常；黑屏是当前原生结束行为，后续一旦继续按下一步可能退出放映，不能假报仍在有效播出。
本轮已询问用户末页后保持最后一页还是结束并清空窗口。尚未得到选择前不改变结束策略、不为测试伪造完成，也不以简单页码/动画事件数猜最后一个点击阶段。
首/末页带动画、快速动画末步直接跨出、复杂重复/自动序列及相应状态/前端闭环由T053/T044继续承接。

## 文档与收尾

新增仅QA的RewindVisual.cs/AnimatedBoundary.cs，没有新增生产依赖或运行时服务。正式代码本轮未改，不重新跑完整前后端冒称覆盖这个物理门禁。
QA与隔离Host构建0警告/错误，Spec/diff通过。CHANGELOG原1032行按月拆到docs/changelog/并保留入口；历史正文逐字核对，不改写当时栈/结论。
全部本轮自有Office/Host/Probe/监听退出，用户库与Resources原件保持，失败日志/图/profile保留。无D4访问、设备电源、墙面、系统音量、部署、提交或推送。
上一目标turn属于真实实现与证据进展；本轮新增像素回归、更正误报及真实末页失败，也不是仅状态重述。整体目标仍active，不据这些有限门禁complete/blocked。

官方依据：[CustomPresentationAccess](https://raw.githubusercontent.com/LibreOffice/core/libreoffice-26.8.0.3/offapi/com/sun/star/presentation/CustomPresentationAccess.idl)、
[CustomPresentation](https://raw.githubusercontent.com/LibreOffice/core/libreoffice-26.8.0.3/offapi/com/sun/star/presentation/CustomPresentation.idl)、
[Presentation](https://raw.githubusercontent.com/LibreOffice/core/libreoffice-26.8.0.3/offapi/com/sun/star/presentation/Presentation.idl)。
