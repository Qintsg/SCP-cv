# 技术门禁与选择

## 已确认

- 保存点549c57f+checkpoint-before-libreoffice-20260929；实验代码归档SHA4335B0A4…02788，原件/数据库保留。
- 官方cli_uno为旧C++/CLI，.NET10加载RealProxy相关类型TypeLoadException（managed-net10.jsonl，exit2）。
- 纯C#Net48使用官方uno.ini、显式URE_MORE_TYPES/offapi.rdb并连接自有profile：XDesktop/XComponentLoader/XDesktop2可cast，真实context成功（framework-connection-unoini.jsonl）。
- 原型初始未加载offapi导致cast失败、fundamental.ini配置SEH失败保留；不得把连接成功写成纯播放器全部能力通过。
- 007正常Frame两profile10轮/100goto可行；隐藏父窗口组合崩溃；暂停标志不能代表媒体冻结。

## Decision

纯C#Net48侧车+官方现有LibreOffice，主.NET10不加载旧CLI，协议中只交换纯值。用户已接受未支持的统一媒体控制明确禁用；先完成承载/关闭门禁，再实现完整运行组与转换。协议及能力拒绝回归可先并行，不提前启动生产原生宿主。

## Rationale

保留成熟导入/绘制路径、原生故障隔离与两窗独立性，不引入源码维护或Python运行时。

## Alternatives considered

源码fork不在范围；Python桥仅QA；把旧CLI直接放.NET10已证实不兼容；单全局Office实例不满足两窗隔离。

## 必须以实际观测回答的门禁

纯净Frame画面、PNG/PDF导出和准确退出仍需实证。当前统一媒体暂停/恢复/定位明确不支持，能力表和拒绝路径需验证；用户2026-09-29已接受，不改写此前媒体接口失败证据。

## 2026-10-03：末页无操作与原生生命周期

Decision：独立noop结果使用v3协议，未知/旧v1/v2不得混用；原生末页必须由实际nextIndex及模型页/occurrence判断，不仅current==count-1。前端提示不能冒充已阻止native结束屏。
Rationale：固定版本[主序结束路径](https://github.com/LibreOffice/core/blob/libreoffice-26.8.0.3/slideshow/source/engine/slideshowimpl.cxx#L2293)注册下一次点击为slide-end；[末序skip](https://github.com/LibreOffice/core/blob/libreoffice-26.8.0.3/slideshow/source/engine/animationnodes/sequentialtimecontainer.cxx#L76)及[递补next](https://github.com/LibreOffice/core/blob/libreoffice-26.8.0.3/slideshow/source/engine/usereventqueue.cxx#L243)使自动tail可穿透末页。节点endEvent比延后一帧的主序通知早，但[撤销](https://github.com/LibreOffice/core/blob/libreoffice-26.8.0.3/slideshow/source/engine/effectrewinder.cxx#L173)也会强制结束，不能将其当新轮耗尽。
Alternatives：仅helper读取耗尽仍有自然完成TOCTOU；pause屏障改变播放状态，不作为首选。仅末序End=EventTrigger.NONE/取消tail为待验证候选：[官方NONE](https://github.com/LibreOffice/core/blob/libreoffice-26.8.0.3/offapi/com/sun/star/animations/EventTrigger.idl#L24)、[事件生成](https://github.com/LibreOffice/core/blob/libreoffice-26.8.0.3/slideshow/source/engine/animationnodes/generateevent.cxx#L87)及[容器结束规则](https://github.com/LibreOffice/core/blob/libreoffice-26.8.0.3/slideshow/source/engine/animationnodes/basecontainernode.cxx#L136)支持推导，但不能据此称实测已可靠。需受控初次display、最后真实直属子项身份与世代，验证自动零时长、repeat、隐藏/重复自定义页、倒退/重开/迟到回调及图像保持。

## 2026-10-03：D4免费环境来源

Decision：按CERNET固定路径安装与本机一致的LibreOffice26.8.0.3，校验SHA/Authenticode/MSI身份，静默/norestart/REGISTER_NO_MSO_TYPES=1；不调整执行策略、用户profile或电源。
Rationale：[CERNET固定MSI](https://mirrors.cernet.edu.cn/libreoffice/libreoffice/stable/26.8.0/win/x86_64/LibreOffice_26.8.0_Win_x86-64.msi)的文件名26.8.0在[winget固定26.8.0.3清单](https://raw.githubusercontent.com/microsoft/winget-pkgs/master/manifests/t/TheDocumentFoundation/LibreOffice/26.8.0.3/TheDocumentFoundation.LibreOffice.installer.yaml)和[TDF官方SHA](https://downloadarchive.documentfoundation.org/libreoffice/old/26.8.0.3/win/x86_64/LibreOffice_26.8.0.3_Win_x86-64.msi.sha256)对应同一4AA6C6E1…DB95、374906880字节。D4实际签名Valid、ProductCode5C7E2016…/ProductVersion26.8.0.3已核。
Alternatives：不使用latest或直接猜文件版本；本机旧安装缓存已不存在。远端文件脚本受执行策略拒绝，改用原生下载/MSI命令，不放宽策略；首次MSI误解析D:/路径exit1619，核日志后用文件实际D:\路径安装exit0，无重启。

## 2026-10-04：原生终点、监听回调与重复页

无主序当前页按native nextIndex确认终点，已接正式Host；有主序不能据页码猜耗尽。listener remove不排空快照、首次零时长/同页重显示及repeat、Custom重复页共享模型等研究与候选边界见[生命周期补充](research-terminal-lifecycle.md)。这些仍是完整T069的未完成门禁，不据D4无主序小窗绿改成通用完成。
