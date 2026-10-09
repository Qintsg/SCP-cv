// 墙面已写出后的提交失败、取消与跨服务排他；只用受控替身和独立SQLite。
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using ScpCv.Domain.Model;
using ScpCv.Infrastructure.Commands;
using ScpCv.Infrastructure.Persistence;
using ScpCv.Infrastructure.Playback;
using ScpCv.Infrastructure.VideoWall;
using ScpCv.Integration.Tests.Fixtures;

namespace ScpCv.Integration.Tests;

public sealed class VideoWallLayoutRecoveryTests
{
    [Fact]
    public async Task FailureOfInitialCommitReceiptDoesNotLeaveDispatchingOrSendAnyFrames()
    {
        await using var fixture = await ControlHostFixture.CreateAsync();
        var factory = new FaultFactory(fixture.Database.Layout.DatabasePath, new FirstReceiptFailure());
        using var writes = new WriteCoordinator(factory);
        var wall = new CallbackWall(() => { });
        var runtime = Runtime(fixture, writes, wall);
        await Assert.ThrowsAsync<PlaybackServiceException>(() => runtime.ApplyWallLayoutAsync(Layout(WallInputKind.Window2), 0));
        Assert.Equal(0, wall.Dispatches);
        var state = await new VideoWallLayoutService(fixture.Database, fixture.Writes, runtime).GetAsync();
        Assert.Equal("uncertain", state.DispatchStatus);
        Assert.Contains("发送前", state.DispatchDetail);
        Assert.Null(state.Applied);
    }

    [Fact]
    public async Task FailureToPersistUncertainDoesNotHideOriginalPostDispatchFailure()
    {
        await using var fixture = await ControlHostFixture.CreateAsync();
        var failure = new ArmedFailure();
        var factory = new FaultFactory(fixture.Database.Layout.DatabasePath, failure);
        using var writes = new WriteCoordinator(factory);
        var wall = new CallbackWall(() => failure.Armed = true);
        var runtime = Runtime(fixture, writes, wall);
        var error = await Assert.ThrowsAsync<PlaybackServiceException>(() => runtime.ApplyWallLayoutAsync(Layout(WallInputKind.Window2), 0));
        Assert.Equal("wall_state_persistence_failed", error.Code);
        Assert.Contains("注入提交失败", error.Message);
        Assert.Equal(1, wall.Dispatches);
        // 连不确定标记也无法持久化，只能保留中断状态并在下次启动恢复，不自动重发。
        var service = new VideoWallLayoutService(fixture.Database, fixture.Writes, Runtime(fixture, fixture.Writes, wall));
        Assert.Equal("dispatching", (await service.GetAsync()).DispatchStatus);
        await new DatabaseInitializer(fixture.Database).InitializeAsync();
        Assert.Equal("uncertain", (await service.GetAsync()).DispatchStatus);
        Assert.Null((await service.GetAsync()).Applied);
    }

    [Fact]
    public async Task CancellationAfterDispatchIsUncertainAndNeverReplayed()
    {
        await using var fixture = await ControlHostFixture.CreateAsync();
        using var cancel = new CancellationTokenSource();
        var wall = new CallbackWall(cancel.Cancel);
        var runtime = Runtime(fixture, fixture.Writes, wall);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => runtime.ApplyWallLayoutAsync(Layout(WallInputKind.Window2), 0, cancel.Token));
        var state = await new VideoWallLayoutService(fixture.Database, fixture.Writes, runtime).GetAsync();
        Assert.Equal("uncertain", state.DispatchStatus);
        Assert.Null(state.Applied);
        Assert.Equal(1, wall.Dispatches);
    }

    [Fact]
    public async Task SharedGateKeepsDispatchAndPersistenceOrderedAcrossServiceInstances()
    {
        await using var fixture = await ControlHostFixture.CreateAsync();
        using var gate = new VideoWallOperationGate();
        var wall = new BlockingWall();
        var firstRuntime = Runtime(fixture, fixture.Writes, wall, gate);
        var secondRuntime = Runtime(fixture, fixture.Writes, wall, gate);
        var first = firstRuntime.ApplyWallLayoutAsync(Layout(WallInputKind.Window2), 0);
        await wall.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var second = secondRuntime.ApplyWallLayoutAsync(Layout(WallInputKind.Laptop), 0);
        Assert.False(wall.SecondStarted.Task.IsCompleted);
        wall.Release.SetResult();
        await Task.WhenAll(first, second);
        Assert.True(wall.SecondStarted.Task.IsCompleted);
        var state = await new VideoWallLayoutService(fixture.Database, fixture.Writes, secondRuntime).GetAsync();
        Assert.Equal("laptop", Assert.Single(state.Applied!.Mappings).Input.Kind);
        Assert.Equal("sent_unconfirmed", state.DispatchStatus);
    }

    private static WallLayout Layout(WallInputKind kind) => new("恢复测试", [new(WallRegion.Fullscreen, new(kind))]);

    private static RuntimeStateService Runtime(ControlHostFixture fixture, WriteCoordinator writes,
        IVideoWallController wall, VideoWallOperationGate? gate = null) => new(fixture.Database, writes,
        new CommandCoordinator(fixture.Commands, new NullCommandWakeNotifier()), videoWall: wall, wallGate: gate);

    private sealed class CallbackWall(Action callback) : IVideoWallController
    {
        public bool IsHardware => true;
        public int Dispatches { get; private set; }
        public Task DispatchAsync(string mode, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<VideoWallDispatchReceipt> DispatchLayoutAsync(WallLayout layout, CancellationToken cancellationToken = default)
        {
            Dispatches++;
            callback();
            return Task.FromResult(new VideoWallDispatchReceipt(true, 200));
        }
    }

    private sealed class BlockingWall : IVideoWallController
    {
        private int _dispatches;
        public bool IsHardware => true;
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource SecondStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task DispatchAsync(string mode, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public async Task<VideoWallDispatchReceipt> DispatchLayoutAsync(WallLayout layout, CancellationToken cancellationToken = default)
        {
            if (Interlocked.Increment(ref _dispatches) == 1)
            {
                Entered.SetResult();
                await Release.Task.WaitAsync(cancellationToken);
            }
            else SecondStarted.SetResult();
            return new(true, 200);
        }
    }

    private sealed class ArmedFailure : SaveChangesInterceptor
    {
        public bool Armed { get; set; }
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default) =>
            Armed ? throw new DbUpdateException("注入提交失败") : ValueTask.FromResult(result);
    }

    private sealed class FirstReceiptFailure : DbTransactionInterceptor
    {
        private int _commits;
        public override Task TransactionCommittedAsync(System.Data.Common.DbTransaction transaction,
            TransactionEndEventData eventData, CancellationToken cancellationToken = default) =>
            Interlocked.Increment(ref _commits) == 1
                ? throw new InvalidOperationException("首个提交已完成但回执失败") : Task.CompletedTask;
    }

    private sealed class FaultFactory(string path, IInterceptor interceptor) : IDbContextFactory<ControlDbContext>
    {
        public ControlDbContext CreateDbContext() => new(new DbContextOptionsBuilder<ControlDbContext>()
            .UseSqlite(new SqliteConnectionStringBuilder { DataSource = path, ForeignKeys = true, Pooling = false }.ToString())
            .AddInterceptors(interceptor).Options);
    }
}
