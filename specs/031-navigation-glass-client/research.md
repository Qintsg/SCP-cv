# 研究结论

## 导航平台

Decision：保留mobileControls/tabletControls的内容策略，只限定touchNavigation的构建平台；Web和Electron使用侧栏。

Rationale：当前条件仅检查尺寸与粗指针，触摸设备上的网页也进入底栏。AppShell、PptFocusView及ScreenShareTray共用该条件，集中修正可以同时改变底部留白。

Alternatives considered：不按用户代理字符串识别设备，不分别复制页面或导航业务。

## Pad定位

Decision：手势使用当前DOMRect和实际入口几何，位置变化及旋转使旧手势失效。

Rationale：Pad横屏1280和竖屏800时底栏都可能保持720×80，左边位置从280变40；ResizeObserver只看宽高，旧left偏移240px。视觉视差也不能移动命中层。

Alternatives considered：不采用固定像素补偿，不单纯增加ResizeObserver回调。

## 玻璃与打包

Decision：复用现有Motion和simple-liquid-glass，保持系统减弱效果偏好；Windows便携包与Android自适应应用采用1.0.3，Android code4及原签名。

Rationale：组件、私有预览资产、SDK与签名已有实际验证流程，能够直接更新现有客户端。

Alternatives considered：不引入第二套客户端界面或玻璃渲染器。手机和Pad继续同一应用，两份命名。
