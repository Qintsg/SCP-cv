# 016工作区与本机预览边界

## 坑156 - 源4K、节点1080P和PDF栅格分辨率不是同一个值

只把WPF窗口铺满4K仍可能放大默认PDF栅格；把所有协议尺寸一起改4K又会破坏50节点目标和独立IP81。016仅63/64输入4K，节点/IP81保留原值，PDF显式指定等比例目标像素。配置/包级验证不证明现场编码器SPS已同步4K。

本机125%下原生测试又证明DestinationWidth/Height是DIP，直接传3840会产出4800像素；改用64DIP有界探针校准栅格比例，不修改系统/进程DPI。1080P/4K两档PNG及相邻页缓存均验证通过；官方单位说明见[DestinationWidth](https://learn.microsoft.com/en-us/uwp/api/windows.data.pdf.pdfpagerenderoptions.destinationwidth)。

## 坑157 - Chromium不能直接拉RTSP，进程替身也发现不了FFmpeg参数错误

016时用户要求本机直拉，因此当时D4代拉违背范围；018用户明确改为服务端按需共享FLV，旧本机桥已退役。普通网页/Electron直接设置rtsp video.src仍不构成实现。历史本机桥只监听loopback并验证Origin/token；真实回环发现RTSP demuxer不接受rw_timeout，改用其timeout参数（RTMP使用rw_timeout）。替身测试不替代真实解码器。Windows以signal结束时exitCode仍为null，终端检查还必须看signalCode。

## 坑158 - Ant原有几何与CSS网格直接叠加会造成错位

给Slider强加44px高度/padding使handle偏离track；grid label默认stretch把开关拉长；Ant Card body的clearfix伪元素成为网格项造成空白。分别恢复组件固有几何、switch起点对齐、只在已改grid的body移除clearfix。图标按钮用inline-flex垂直居中；真实只读截图和内存交互分层验收。

移动预案还复现Radio默认baseline与44px命中区叠加，使圆点与文字中心相差11px；统一wrapper的align-items:center，浏览器逐项量中心偏差小于2px，不只看构建或静态CSS字符串。

## 坑159 - YAML与索引不能静默双写或只检查‘可序列化’

016首次全量发现IReadOnlyDictionary无法直接反序列化；明确映射到Dictionary/List后保存/读取/编辑/移动/删除通过。准备阶段仅暂存，失败索引回滚；提交后逐文件原子发布失败必须报告不完整，不宣称全部保存。激活检查同一配置快照，已确认的旧头戴/悬挂别名和0/null机位必须规范化比较。保留SourceId而不把动态SourceName变化当作意图变化；不覆盖身份不符文件或符号链接。

这些本机证据不替代D4输出、现场预览流或任意余数矩形的硬件确认。

## 018追加 - 转协议、网络让路与真实浏览器不是同一层

- MediaMTX本身不输出HTTP-FLV／WS-FLV；只增加Vue播放器不能让浏览器直连RTMP TCP。018复用已有MediaMTX按需上游，单FFmpeg编码、ControlHost有界GOP扇出，观看端无需工具。
- 只丢完整GOP，不随机丢字节／P帧；降档重启保留连接并重映射时间戳。人数突增后不把旧高码率缓存塞给全部新读者，否则降码率后仍被旧数据堵住。
- 首次真实Chromium0帧，而ffprobe两档都可解码；根因是Blob Worker无法解析`/preview018.flv`相对URL。主线程转绝对URL后26帧、1280／320、0错误；不以关闭Worker或压制错误绕过。
- 同账号直接重登可能保留CSRF；预览Owner绑定加密会话票据摘要，不能只看用户ID／CSRF。跨站GET不能创建流，先POST授权租约再消费。
- Idle仅CPU优先级；输出转码只节省出站，入站需低清源。全机网络优先级依赖额外系统／交换机QoS，不能误标共享MediaMTX／ControlHost全部流量为低优先。
- 新Live测试为显式Physical：只启动回环MTX、合成RTMP与自有Job，65观看者、一上游／一编码、HTTP／WS实际解码、动态尺寸与释放；不替代真实摄像机、D4或现场QoS确认。
