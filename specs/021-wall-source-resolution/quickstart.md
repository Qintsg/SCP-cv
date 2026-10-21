# 验证方式

1. `node --test tools/input-resolution/protocol.test.cjs`，仅字节/TCP回环。
2. `dotnet test runtime-dotnet/tests/ScpCv.Infrastructure.Tests -c Release --filter "FullyQualifiedName~SiteInputCatalog|FullyQualifiedName~VideoWall|FullyQualifiedName~Television"`，不访问现场。
3. `dotnet test runtime-dotnet/tests/ScpCv.ControlHost.Tests -c Release --filter FullyQualifiedName~VideoWallRegistration`，只装配不发送。
4. 现场网络下`node tools/input-resolution/probe.cjs`只读调查，结果与主流/SPS交叉核验，不执行设备返回内容。
5. 更换输入后重新核验，显式改VideoWall:Inputs并重启新包。本轮不改Windows或自动部署/下发映射。
