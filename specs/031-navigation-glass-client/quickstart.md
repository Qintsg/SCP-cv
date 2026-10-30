# 验证入口

1. `npm --prefix frontend run typecheck`及Web/App构建。
2. `node frontend/scripts/verify-glass-motion.mjs --playwright <实际模块> --browser <Edge>`检查运动与资源释放。
3. `node frontend/scripts/verify-navigation-031.mjs`按脚本参数检查真实Vue几何、旋转和SVG；不模拟输入或设备反馈。
4. 执行Spec Kit、OpenAPI及030相关回归；将结果写入verification.md。
5. 提交并推送两个远端；从固定实现构建Windows便携包及Android code4签名包，核对包内网页、CA和证书，再更新桌面文件。
