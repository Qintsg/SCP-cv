// 文件描述：用真实SQLite验证换源保留窗口维护意图及同类命令合并。
// 项目：SCP-cv；文件：WindowForegroundQueueTests.cs；作者：Qintsg；日期：2026-10-29。
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ScpCv.Domain.Model;
using ScpCv.Infrastructure.Commands;
using ScpCv.Infrastructure.Persistence;

namespace ScpCv.Integration.Tests;

public sealed partial class WindowForegroundRealTests
{
    /// <summary>:returns: 媒体替换保留维护命令，较新维护意图只合并同类待执行命令。</summary>
    [Fact]
    public async Task RealQueuePreservesForegroundIntentAcrossMediaReplacement()
    {
        using var factory = new WindowForegroundHostFactory();
        var repository = factory.Services.GetRequiredService<CommandRepository>();
        var first = await repository.EnqueueAsync(new(CommandTargetKind.Display, 1, "SET_WINDOW_FOREGROUND", "{\"foreground\":false}", 0, 0));
        var open = await repository.EnqueueAsync(new(CommandTargetKind.Display, 1, "OPEN", "{}", 1, 0));
        var contexts = factory.Services.GetRequiredService<ControlDbContextFactory>();
        await using (var database = contexts.CreateDbContext())
        {
            Assert.Equal(CommandStatus.Pending, (await database.CommandRecords.SingleAsync(item => item.CommandId == first.CommandId)).Status);
        }
        var second = await repository.EnqueueAsync(new(CommandTargetKind.Display, 1, "SET_WINDOW_FOREGROUND", "{\"foreground\":true}", 1, 0));
        await using (var database = contexts.CreateDbContext())
        {
            Assert.Equal(CommandStatus.Superseded, (await database.CommandRecords.SingleAsync(item => item.CommandId == first.CommandId)).Status);
            Assert.Equal(CommandStatus.Pending, (await database.CommandRecords.SingleAsync(item => item.CommandId == open.CommandId)).Status);
            Assert.Equal(CommandStatus.Pending, (await database.CommandRecords.SingleAsync(item => item.CommandId == second.CommandId)).Status);
        }
    }
}
