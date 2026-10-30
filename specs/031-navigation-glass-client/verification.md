# 本机验证

日期：2026-10-30。本轮包含030工作区修改，更新源码与客户端制品；不更新D4服务或操作现场设备。

## 导航与命中

`frontend/scripts/verify-navigation-031.mjs`使用真实Vue、Pinia、Ant Design Vue、AppShell和路由，保持设备状态未知。没有替换API、平台桥或控件，未派发鼠标、键盘及触摸事件。

- 56组页面通过：Web四种视口、触摸/非触摸、深浅色和两个路由；App手机及Pad六种视口、深浅色和两个路由。
- Web全部保留左侧菜单；App手机/Pad显示五入口底栏；无水平溢出、无页面异常、API请求为零。
- 650个真实DOM内部位置的elementFromPoint结果与生产navigationPosition一致；五个入口中心的连续索引正确。
- Pad底栏宽度720px保持，横屏1280到竖屏800再返回，left为280→40→280，命中持续正确。
- 真实SVG位移滤镜、reveal方法和根层无transform通过；减弱动态、减弱透明及停止背景模糊生效。

报告：`.validation/navigation-031/run-1793368668597/results.json`。App编译布局检查不代表实体Android手指验证。

最终暗色仅调整选中颜色，另复查2页及50个命中。计算颜色rgb(214,234,255)，图标和文字可读；镜片及交互几何保持。报告及截图：`.validation/navigation-031/run-1793369061843/`。

## 运动

`verify-glass-motion.mjs`最终11项通过：连续跟随、按压及速度形变、拖动零导航、松手一次提交、同页保持、取消、重复更多、实际路由同步、空闲停止、减弱动态、卸载清理。真实文件提交为`2,4,4,4`，策略svg。

证据：`.validation/glass-028/motion-e79b0ee66fa8/`。

## 构建与范围

前端类型、Web/App构建、Electron主进程编译通过；原生桥和导航安全边界的两项纯验证通过。030的真实缓存刷新、Kestrel/Named Pipe/WebView共享收发及27组媒体预览证据见[030验证](../030-media-preview-sharing/verification.md)，本轮保留该实现及其验证边界。

真实Electron44.2.0资源检查通过：隔离userData、sessionData和crashDumps，直接运行正式main/preload/session/protocol。app://的PDF .mjs返回200及JavaScript类型，真实模块Worker收到ready；正式bridge.platform为electron，Node不暴露，contextIsolation、sandbox、webSecurity保持。

回环iframe的原CSP及X-Frame-Options原样保留；普通连接页没有悬浮底栏，未登录侧栏不计为通过。原PID76832正常退出0。证据：`.validation/navigation-031/electron-resources-TUWKTy/`；入口为`frontend/scripts/verify-electron-resources-031.mjs`，没有替换生产桥或协议。

最终制品检查随[制品记录](packages.md)记录。打包不更新D4；030新增后端接口需要后续同步ControlHost和PlayerWorker后在D4使用。

实现提交a278227已推送两个仓库。Windows109个代码文件、195个媒体资产及ZIP完整性通过；Android1.0.3/code4的Release、Lint、签名、16KiB排列、89个网页文件及公开CA均通过。三份桌面制品已更新，旧包存档，用户配置保持。
