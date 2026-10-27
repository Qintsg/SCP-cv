# 验证

1. 使用真实本地服务检查登录、未登录、连接失败及恢复。
2. 检查已保存布局的恢复、自定义编辑和错误反馈，确认不会发送设备指令。
3. 执行前端类型检查与构建、后端构建、Spec Kit 校验和差异检查。
4. 在只读无头浏览器中查看相关页面，确认文本和状态呈现。

本机真实服务验证需要已构建的 Release 版 ControlHost：

```powershell
dotnet build runtime-dotnet/ScpCv.sln -c Release --no-restore
node frontend/scripts/verify-auth-wall-real.mjs
```
