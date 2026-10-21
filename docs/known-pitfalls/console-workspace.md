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

## 018部署追加 - 启动受理、在线与历史观测不能混为一谈

- 新任务开始后Host健康可能先于Worker就绪。桌面启动必须等两个真实播放器在线；新任务冷启动时不能看到零成员就同时再发restart。已在线组重复点击只验证、不重启，跨进程互斥和任务IgnoreNew共同限重。
- D4停机前两窗已idle/source为空，但actual_source_id仍是旧116且观测代次落后。部署后不得把历史字段当当前播放成功，也不能为“验收清零”改数据库或擅自恢复/关闭来源；分别保留原观测证据、当前idle和真实进程身份。
- SSH远程命令结束可能由OpenSSH Job回收其Start-Process子进程；本轮首构建0字节日志且原PID消失，未创建包。改为持有SSH exec通道到实际构建退出，日志、969文件摘要与成功标志完整后才停旧服务。不能把拿到PID当部署完成。

## 020追加 - 共享上游不等于浏览器已共享，组件寿命也不是流寿命

- 018后端同URI已共享上游，但每Vue组件仍独立GET／decoder；坐标key与单/双/自定义分支重建导致画面重连。020用独立应用scope池、一个video和多canvas，几何／标签／quality提示不入键，最后引用1秒宽限吸收组件替换；认证／主机失效立即清画面。不能把第一个组件的`useRequestContext`闭包当整池owner。
- 不同媒体别名合并后若只留最先创建的消费租约，最先那个source_id修订会误伤其它同URI消费者。每target保留验证租约，独立消费租约以同归属／同URI的有效`aliases`续租；来源改变只迁该target，跨流／跨人／过期／超限拒绝。公开键是随机私钥HMAC，不暴露可离线猜凭据的裸URI哈希。
- 同一个request timer同时做重试与空闲回收时，迟到失败会覆盖timer引用，旧空闲timer仍可杀死重新加入的消费者。020将重试与空闲timer分开，并覆盖“待创建→移除→失败→再加入”的回归；不靠延长等待掩盖。
- Electron正式入口先mount后安装PlatformAdapter；首屏直接缓存无能力会使后续一直无法预览。受限preload桥在挂载前已同步就绪，020薄适配使用同一桥；没有桥则明确不可用，不静默把服务端中转说成本机直连。
- 锁定mpegts.js1.8.2源码IO stash默认64KiB，在线文档384KB不能替代本机事实。禁stash和半秒GOP经5+5轮实际帧基准改善p50/p95；保持完整GOP、慢队列、时间戳与带宽边界。实际66canvas一流一decoder不等于D4／全部编码／网络抖动验收，详见020 `verification-preview.md`。
