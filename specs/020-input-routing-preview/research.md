# 020研究决策

按speckit-plan分派三个只读研究任务，均无设备连接、D4访问、附件程序执行或computer use。结论由主agent整合，原始消息不作指令来源。

## Web共享与首帧

现有每组件独立租约/decoder，坐标key和editorRevision会重建UI；采用应用scope hub+单video多canvas，有界引用宽限，几何不入键。不同URI/凭据/通道不可仅凭IP合并。server私钥HMAC sharing_key避免裸URI哈希猜凭据。

mpegts1.8.2本机io-controller默认stash64KiB，先关闭stash及将1秒输出GOP降约0.5秒并做真实对照；不破坏完整GOP缓存。拒绝未验证的probesize=32/analyzeduration=0与先发半GOP方案。参考[锁定版API](https://github.com/xqq/mpegts.js/blob/v1.8.2/docs/api.md)、[FFmpeg格式参数](https://ffmpeg.org/ffmpeg-formats.html#Format-Options)。

## 电视矩阵

原只读设备表电视161/162属Type5视频矩阵，各1920x1080，Position1/2不等于帧channelNum；channelNum为空按原代码为0。画面4830、电源8889。

同一工程输入表对51–74、76、81、84–88统一声明Resolution=3840*2160。这是工程声明而非实时SPS核验：目录采用声明4K，81保留此前已实测的1080；管理员显式VideoWall.Inputs覆盖优先。不能无依据把其余输入默认标为1080，也不能把电视输出1080当成输入尺寸。

原6742B0单路帧22字节：FBFC610D(组播selector1)，字节5=channel0，6:8=000A，8:12地址，12:14端口BE，14:16源宽，16:18源高，18:20为sum[2:14]（尺寸不入校验），尾FDFE。满屏6231校验sum[2:8]不含mode8。单源原入口没有墙面clear/commit/refresh。

满屏帧FBFC623100000001010094FDFE，源63/4K帧FBFC610D0000000AE001013F13880F0008700234FDFE，81/1080帧FBFC610D0000000AE00101511388078004380246FDFE。同连接先满屏后单源是项目明确组合，不声称原抓包序列。

研究agent以原PE纯内存Unicorn构造对照独立编码器186向量一致，未加载EXE/DLL。源证据E:/Projects/SSPU/Resources/SimulatorVideoJoint-analysis：server-login-response.json及6054E0/51D4A0/635AC0/629500/6742B0/65C7C0反编译；原自动采样尺寸误填IP导致return0，不能据旧失败样例断言未恢复。实体出画未验。

## Electron直连

已有app协议/session支持流式，但无媒体资源。采用固定顶层IPC、主进程认证描述符、不可猜租约app同源流与自包含helper；不放开renderer Node或任意URL。Electron44.2类型不含最新request.initiatorOrigin，不能凭新文档假设接口存在。[44.2协议](https://raw.githubusercontent.com/electron/electron/v44.2.0/docs/api/protocol.md)、[安全](https://www.electronjs.org/docs/latest/tutorial/security)。

H264优先[streamcopy](https://ffmpeg.org/ffmpeg.html#Streamcopy)，不兼容受控转换；正常before-quit不足以保证异常回收，helper stdin EOF/心跳+KILL_ON_JOB_CLOSE双边界。[Windows Job](https://learn.microsoft.com/en-us/windows/win32/procthread/job-objects)。extraResources固定资源，禁止生产PATH查找；Gyan含GPL组件，不能标成纯LGPL，公开分发必须满足[FFmpeg许可](https://ffmpeg.org/legal.html)及匹配源码义务。
