# 大文稿上传与正式两窗补测

日期：2026-10-05。对应T075及T069/T073/T074的有限补测，不标完整优化完成。

## 缺陷与修复

实际D4完整Kestrel组上传`TestPPT.pptx`（98756629字节）被默认30000000字节请求上限截断，客户端socket10054、服务端BadHttpRequestException。尚未进入UNO或转换，不能当文稿兼容失败。
失败根`api-run-08dbb5430416490c8de1f5bc4462dfaa`、证据`.validation/008-supplement-evidence-1791154280000/`保留；当次实际视频运动与暂停已通过，Host正常退出0、QA因上传退出2。

生产上传端点在读取前设置有限1073741824字节（1GiB）总请求和Multipart上限。已声明超限返回413/`upload_too_large`；畸形表单400/`invalid_upload_form`，不登记源。普通JSON等请求不放宽，认证/CSRF过滤、原件目录和摘要计算不变；代理/IIS自身限制仍须单独配置。
两条公开认证HTTP回归先复现旧错误后通过：超限声明不读正文且源列表为空、缺Multipart boundary明确400且列表为空。超限测试仅声明长度，不是实际传输1GB或chunked超限实测。

新文件`MediaEndpoints.UploadLimits.cs`承接解析边界，避免继续堆积MediaEndpoints。OpenAPI上传413、明确有限上限及使用文档同步。

## 当前二进制与D4实测

仅ControlHost重发布，DLL SHA256 `8649CDF455755221CDDB2BFF6258C26C18AE10CC26A91F230AE682B41C068660`，Contracts63304E与Framework Host404B7D保持同版；补丁ZIP SHA `5652050A2377EFE427D824F5E60CF1D65CDE7ADD747386CB5E22BB09072B6FE5`。
新独立QA根`D:/SCP-cv/.validation/008-upload-limit-group-1791155100000`，运行根`runtime/api-run-75d096e5af0e48a1b304873253f92a6a`；并非源码secondary推送/远端拉取。
Host19072/start UTC2026-10-04T23:06:13.2042677Z/session1，实际HTTP回环57497。
Player1=18752/start23:06:22.1670597Z、Player2=13452/start23:06:22.1905204Z。精确窗口身份、PerMonitorV2捕获、DWM1920×1080及九点无遮挡检查，分别只占DISPLAY2/3，不截图或改变控制DISPLAY1。

- 视频：窗口1两张超过片头的图显示人物及模型运动；暂停后两次实际时钟相同，两张PNG SHA均`58C9BA60FFAB0C694D0386969F9034B802A6F52104D9E13BFACB8CBD4EA8725E`。窗口2图片整幅保持。关闭后实际源null且全黑。
- 实际大文稿上传成功，登记source8；上传后的原件SHA `0BBAFC49B6F155F11EA27205B7A9A792AE253B5E423A115A6AB3E8E238F25C22`与测试副本一致。原文件保留，未调用PowerPoint。
- 两窗同时动态打开原排版、实际第6/7页；窗口1两次NEXT_EFFECT后的标题/副标题完整，窗口2初始白底保持。白图本身不能证明UNO或页码，另有真实会话/进程绑定取证。
- 窗口1实际跳到第7页后NEXT_EFFECT的精确持久回执：command_id `a53c6c7f-645f-480f-903f-0bd29183827d`、source_generation4、completed、`presentation_at_last_slide`。末页与随后保持图同SHA `F116CA0BADADCAE8751A1A9BCA8C01E4A9536A150AF11A0A551E903DAC3A9E03`。该页无后续动画，不替代复杂动画末页验收。
- 正式转换日志4202确认源8导出7页；独立缓存实际存在page-0001至0007及presentation.pdf。关闭后原件与产物均可独占读，原件SHA不变。
- 两窗关闭均实际源null/idle、整幅黑图同SHA `C6C634671806E7B55658335A1168F8A48B3E78E555800DD1DF9E6ACCCB8B490D`。shutdown200，Host于UTC23:07:19.3336362正常退出0。

三个自有Framework job退出均active=0、members空。自有Office launcher/listener分别18524/10372（57502）、20184/13300（57503）、17108/11836（57511）；不凭顺序猜转换/播放角色。
任务`SCP-cv-owned-upload-limit-1791155100000`确认Ready/instances0/result0后仅移除登记；后续只读核上述精确PID、57502/57503/57511监听及ScpCv项目进程均不存在，D4源码工作区仍干净。不是强清当正常退出。

## 图像与回归证据

事件、stdout/stderr与16张原样PNG已下载至`.validation/008-upload-limit-evidence-1791155300000/`，压缩包SHA `B615E5E6B09A56883EC289B6571EC28A2D96AC4B85128F97819F191774169CAB`。
独立视觉复核逐张查看全部16张图：没有新裁切、菜单/任务栏、桌面边条、重叠、串窗或关闭残留；视频素材自身红/白字覆盖不当作新增UI缺陷。图像保持另以整幅SHA佐证，不从单图推断动画执行次数。

完整Release构建0错误/0警告；本轮串行非Physical回归1139/1139、0跳过：Domain41、Contracts322、Infrastructure127、ControlHost280、Integration242、Windows127。TRX位于`.validation/008-upload-limit-regression-1791155189255/`。
本轮没有改前端生产代码；后续按pnpm重新跑完整前端271/271、0跳过（421577.8ms），vue-tsc及Web构建通过。构建仍报告入口大于500kB的既有体积警告，没有关闭警告；这些自动化及旧浏览器夹具不冒称当前D4控制台动态联动。

## 保留的风险和未完成范围

VLC stderr包含SetThumbNailClip0x800706f4及hardware acceleration picture allocation/get_buffer/decode_slice_header/no frame错误；本轮后续运动图证明最终有出画，但不能称默认硬解全程健康或长期没有掉帧。保留原日志，后续媒体稳定性测试继续承接。
PPT/直播/音频混合60分钟、两窗交叉10轮/100操作、MediaMTX完整组、UDP重开、共享控制台动态SSE/真实提示、封装客户端及正式源码同步仍未完成。已完成的文件/网页3613.7秒观察另见[group长稳记录](group-visual-soak-20261005.md)，不能合并成所有媒体长稳。
