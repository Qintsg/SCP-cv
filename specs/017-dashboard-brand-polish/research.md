# 017 调研与诊断

## 滚动根因

- Decision：先移除无意义的独立行，再实测尺寸，不改32:9或用overflow:hidden裁切。
- Evidence：2002×990只读夹具自定义状态，main可见920而scrollHeight978；重复标题行加间距37px，网格默认footer最低38px加8px边距。两者使正常自定义比快捷模式更易溢出。
- Alternatives：整体缩放、隐藏底部、强行改画面比例均不满足操作可达性。

## 侧栏与品牌

- Decision：浅色雾蓝、深色蓝灰，用主题令牌统一菜单／品牌／底部。品牌改RouterLink，不另建业务handler。
- Rationale：原硬编码dark菜单搭配浅色底会出现文字冲突，需按现有useTheme有效主题接线；纵向留白与菜单项高度需一并按视口高度收紧。
- Alternatives：只换容器底色而保留深色菜单，会造成两块不同底色，不采用。
- Asset：原图含蓝拱门、SSPU#及AILAB；内白保留，外部透明。内置图像编辑工具生成独立PNG，不覆盖用户原图。

## 免安装RTMP预览

- Decision：本轮说明可行路径，不把新Vue壳包装成原始RTMP原生支持，不接入未知设备端点。
- Evidence：mpegts.js官方支持HTTP(S)／WebSocket承载的FLV／MPEG-TS，不是浏览器直接RTMP TCP连接；Jessibuca开源版同样列HTTP-FLV／WS-FLV等，Pro能力不可当开源能力。
- Alternatives：设备提供HTTPS-FLV／HLS／WebRTC时浏览器可直拉；否则需要现场网关转换。项目已有MediaMTX可作为后续复用候选，浏览器不需Node／FFmpeg，但这将改变016“完全不经D4”的边界。编解码兼容、跨域、HTTPS与网络还需验证。
- Sources：
  - [mpegts.js官方项目](https://github.com/xqq/mpegts.js/)
  - [官方直播传输说明](https://github.com/xqq/mpegts.js/blob/master/docs/livestream.md)
  - [Jessibuca开源／Pro差异](https://www.jessibuca.com/)
  - [MediaMTX浏览器嵌入](https://mediamtx.org/docs/read/web-browsers)

无新增技术选择或未决高风险实现假设，沿用现有模块；无需分派研究任务。
