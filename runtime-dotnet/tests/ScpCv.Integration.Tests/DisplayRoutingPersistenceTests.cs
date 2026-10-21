// TV发送前/后SQLite故障注入；提交回执丢失也必须失效旧指纹，不能自动重放。
using System.Data.Common;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using ScpCv.Infrastructure.DisplayRouting;
using ScpCv.Infrastructure.Persistence;
using ScpCv.Infrastructure.VideoWall;
using ScpCv.Integration.Tests.Fixtures;

namespace ScpCv.Integration.Tests;

public sealed class DisplayRoutingPersistenceTests
{
    /// <summary>:param failAt: 第1个事务为发送前，第2个为写出后记账。:param afterCommit: 已提交但未获得可靠回执。</summary>
    [Theory]
    [InlineData(1, false)]
    [InlineData(1, true)]
    [InlineData(2, false)]
    [InlineData(2, true)]
    public async Task PersistenceUncertaintyNeverCreatesReusableSuccess(int failAt, bool afterCommit)
    {
        await using var fixture = await ControlHostFixture.CreateAsync(); using var gate = new VideoWallOperationGate();
        var factory = new FaultFactory(fixture.Database.Layout.DatabasePath,
            afterCommit ? new CommitFailure(failAt) : new SaveFailure(failAt));
        using var writes = new WriteCoordinator(factory);
        var controller = new DisplayRoutingServiceTests.RecordingTelevisions();
        var service = new DisplayRoutingService(factory, writes, controller, gate, new());
        var error = await Assert.ThrowsAsync<DisplayRoutingException>(() => service.ApplyAsync("tv_left", new() { Kind = "window_1" }, false));
        Assert.Equal(failAt == 1 ? "display_routing_persistence_failed" : "display_routing_uncertain", error.Code);
        Assert.Equal(failAt == 1 ? 0 : 1, controller.Calls.Count);
        Assert.Equal("uncertain", (await service.GetAsync())["tv_left"].Status);
        await service.ApplyAsync("tv_left", new() { Kind = "window_1" }, false);
        Assert.Equal(failAt == 1 ? 1 : 2, controller.Calls.Count);
        Assert.Equal("sent_unconfirmed", (await service.GetAsync())["tv_left"].Status);
    }

    private sealed class FaultFactory(string path, IInterceptor interceptor) : IDbContextFactory<ControlDbContext>
    {
        public ControlDbContext CreateDbContext() => new(new DbContextOptionsBuilder<ControlDbContext>()
            .UseSqlite(new SqliteConnectionStringBuilder { DataSource = path, ForeignKeys = true, Pooling = false }.ToString())
            .AddInterceptors(interceptor).Options);
    }
    private sealed class SaveFailure(int failAt) : SaveChangesInterceptor
    {
        private int _calls;
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default) =>
            ++_calls == failAt ? throw new DbUpdateException("故障注入：保存失败") : new(result);
    }
    private sealed class CommitFailure(int failAt) : DbTransactionInterceptor
    {
        private int _calls;
        public override Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData,
            CancellationToken cancellationToken = default) =>
            ++_calls == failAt ? throw new InvalidOperationException("故障注入：提交回执失败") : Task.CompletedTask;
    }
}
