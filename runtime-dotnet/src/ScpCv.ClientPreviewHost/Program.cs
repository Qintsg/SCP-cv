// 自包含客户端预览宿主入口；只通过私有标准管道接收目标，不提供通用命令执行。
return await ScpCv.ClientPreviewHost.ClientPreviewApplication.RunAsync().ConfigureAwait(false);
