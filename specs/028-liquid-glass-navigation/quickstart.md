# 验证入口

1. `npm --prefix frontend install`
2. `npm --prefix frontend run typecheck`
3. `node frontend/scripts/verify-glass-motion.mjs --playwright <Playwright模块路径> --browser <浏览器程序路径>`
4. `npm --prefix frontend run build:web`及`npm --prefix frontend run build:app`。
5. 真实本机ControlHost与Vite页面以只读浏览器检查手机390×844、844×390、Pad600×960、960×600、800×1280、1280×800及深浅色。
6. 检查五个44px入口、安全区、预览保持、无浏览器错误及减弱效果；截图保存在忽略的.validation/glass-028/。
7. 复制Capacitor资源，生成arm64-v8a release APK，使用原签名证书签名；检查签名、16KiB ZIP排列及当前App资源一致性。

真实浏览器技术接口覆盖连续位置与导航次数，实体Android手指操作作为单独验证边界记录。
