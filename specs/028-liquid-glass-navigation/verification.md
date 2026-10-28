# 验证记录

日期：2026-10-28。

## 浏览器运动

生产`navigationMotion.ts`在真实Edge中通过11项检查，选择回调实际写入本机文件。包括连续位置与速度形变、拖动期间零导航、松手一次提交、同页保持、取消复位、快速按压选择、重复打开更多、实际路由同步、空闲停止、减弱动态与卸载清理。

命令：`node frontend/scripts/verify-glass-motion.mjs --playwright <本机Playwright模块> --browser <Edge程序>`。
最终结果：`.validation/glass-028/motion-b80eecf00c28/result.json`；四次真实文件提交为`2,4,4,4`。空闲700ms绘制计数不增加，卸载后不绘制或提交。

## 真实页面

本机真实ControlHost、独立数据库、实际PNG上传与Vite，正常账号及CSRF登录。预览服务限定回环地址，未访问D4或现场输入。

- 56组页面：手机390×844、844×390；Pad600×960、960×600、800×1280、1280×800；桌面1365×900，各深浅色，仪表盘、媒体、摄像机、文稿专注页。
- 无页面异常及横向溢出；底栏五个触控入口均超过44px，72px手机底栏与80px Pad底栏在视口内，末尾内容保留滚动空间。
- 手机预览组件、媒体图像及预览请求均为零；Pad保留预览与图像，四种旋转场景原预览节点保持连接。
- 实际Vue页检查玻璃组件内部方法、SVG位移滤镜和计算样式，二级页面选中更多。
- 最终材质8组复查通过；额外检查系统减弱动态偏好与减弱透明后关闭折射、停止背景模糊。
- 最终截图已目视检查，图标与玻璃分别绘制，保留背景折射。

证据：`.validation/glass-028/pages-1793159431541/results.json`（56组、4次旋转）、`pages-1793159752738/results.json`（最终8组、1次旋转）。技术验证采用生产运动接口；页面只读检查未模拟鼠标、键盘或触摸。未执行实体Android手指操作。

## 构建

- `npm --prefix frontend run typecheck`通过。
- 最终Web与App构建通过，日志为`.validation/glass-028/build-web.log`和`build-app.log`；Vite提示未来配置加载方式的导入扩展名要求，当前构建成功。
- Spec Kit校验通过，规范、设计、任务与实现按FR-001至FR-006复核，未发现缺失接口或宪章冲突。
- MIT依赖精确版本及其附带组件的许可随网页与安装包分发。

## 运行范围

本轮仅本机客户端实现与打包；D4服务、显示模式、实体设备和媒体数据保持现状。

## Android制品

1.0.2（versionCode 3），应用ID为`cn.edu.sspu.scpcv.control`，沿用已有签名。Release构建成功，v2/v3签名、16KiB ZIP排列、arm64-v8a与版本校验通过；83个网页文件逐一与App构建摘要比较一致，内置公共根证书摘要保持。

桌面文件：

- `C:/Users/qintsg/Desktop/SCP-cv-Android-arm64-v8a-1.0.2.apk`
- `C:/Users/qintsg/Desktop/SCP-cv-Pad-arm64-v8a-1.0.2.apk`

两份为同一自适应应用，分别按手机、Pad命名，安装后根据尺寸与方向显示。大小均为4,606,992字节，SHA256均为`2B9F6CD93BFE2AD80555115CA4C63F6BD3B4EAF7F2AB7D3CCBE62910F09A6D0A`。

本机完整制品检查：`.validation/glass-028/apk-integrity.json`、`signature.txt`、`build-apk.log`。签名私钥与口令保持本机私有存储。

实现提交`d4dc0b09869ae5fd8737abdb82568be368709ce2`已推送GitHub和GitLab的main。只读页面检查所启动的本机ControlHost进程均已退出。
