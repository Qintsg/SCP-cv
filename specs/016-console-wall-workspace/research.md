# 研究与决策

- Decision: 在现有PacketCodec上扩展grid矩形；不重写发送器。Rationale: codec已有像素矩形、Sign/Level/总块数/分片/屏号；Planner目前只全屏/半屏。Alternative: 前端自行造帧会绕过安全预检，拒绝。
- Decision: 输入63/64设3840×2160，81保留1920×1080，目标节点仍1920×1080。Rationale: 原生窗口按实际Screen.Bounds定位；源与目标尺寸不可混淆。
- Decision: 裁切仅允许源宽%(列数×4)==0及源高%(行数×4)==0。Rationale: 逆向余数分支缺证据；4K的7/9列、1080P的4行跨度不能猜测发送。
- Decision: 无数据库Base62短码，版本/规范排序矩形/输入/IPv4/校验。Rationale: 可跨机器解析，不含凭据；典型布局短，50独立IP不承诺固定极短。
- Decision: 可选用户本机Node/FFmpeg只读桥，默认127.0.0.1、Origin/token/Host校验、POST创建及DELETE释放、fetch Blob帧。Rationale: Chromium包括Electron不原生播RTSP/RTMP，不能把video.src指向RTSP假装支持，也不能改D4做代理。
- Decision: 本机桥缺失/网络许可拒绝/流失败仅内联一行，不toast；没有流为普通空态。Alternative: 后端代理/HLS会违背本机拉流要求。
- Decision: 新预案使用明确预设或高级码；旧保持语义原样保留，YAML镜像需原子持久与碰撞防护，不能以‘导出按钮’冒充文件保存。
- Decision: 按截图修改现有Ant工作区，不创建原型站点。Logo直接使用提供PNG，不生成替代图片。

官方依据（2026-10-20研究）：[MediaMTX浏览器读流](https://mediamtx.org/docs/read/web-browsers)、[Chrome本地网络访问](https://developer.chrome.com/blog/local-network-access)、[FFmpeg](https://ffmpeg.org/ffmpeg.html)、[Electron安全](https://www.electronjs.org/docs/latest/tutorial/security)。
