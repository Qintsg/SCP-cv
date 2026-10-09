// 真实管道停止必须等待自有数据库分派释放，不用全局清池或延迟重试掩盖遗留任务。
using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using ScpCv.Contracts.Ipc;
using ScpCv.ControlHost.Events;
using ScpCv.ControlHost.Ipc;
using ScpCv.Infrastructure.Audio;
using ScpCv.Infrastructure.Commands;
using ScpCv.Infrastructure.Persistence;
using ScpCv.Infrastructure.Playback;
using ScpCv.Infrastructure.Runtime;
using ScpCv.Integration.Tests.Fixtures;

namespace ScpCv.Integration.Tests;

public sealed partial class RuntimePipeBrokerTests
{
    /// <summary>:returns: 停止接受连接不等于在途事务已释放，公开停止必须等待实际收尾。</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task StopWaitsForInFlightDispatchToReleaseItsDatabaseTransaction(bool duringHandshake) =>
        VerifyStopDrainAsync(duringHandshake, cancelFirstStop: false);

    /// <summary>:returns: 停止预算取消只能取消等待，后续停止仍须等待同一自有展开。</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task CancelledStopDoesNotDeclareStoppedAndLaterStopStillDrains(bool duringHandshake) =>
        VerifyStopDrainAsync(duringHandshake, cancelFirstStop: true);

    /// <summary>通过公开停止与真实握手/状态消息分别覆盖两层自有任务，不读取私有登记结构。</summary>
    private static async Task VerifyStopDrainAsync(bool duringHandshake, bool cancelFirstStop)
    {
        await using var fixture = await ControlHostFixture.CreateAsync();
        var request = Guid.NewGuid();
        var starting = await fixture.RuntimeAuthority.BeginStartAsync(request);
        await fixture.RuntimeAuthority.ArmAsync(request, starting.GroupEpoch);
        var saveGate = new SaveUnwindGate();
        using var controlledWrites = new WriteCoordinator(new InterceptedFactory(fixture.Database, saveGate));
        using var process = Process.GetCurrentProcess();
        var registry = new RegisteredProcessRegistry();
        var server = new NamedPipeServer(Guid.NewGuid(), process.SessionId, registry);
        var authority = duringHandshake
            ? new RuntimeAuthorityRepository(fixture.Database, controlledWrites, fixture.TimeProvider)
            : fixture.RuntimeAuthority;
        using var broker = CreateDrainBroker(fixture, controlledWrites, registry, server, authority);
        await broker.StartAsync(CancellationToken.None);
        var instance = Guid.NewGuid();
        registry.Register(CurrentIdentity(process, "player-1", instance));
        await using var client = await ConnectAsync(server.PipeName);
        using var stopBudget = new CancellationTokenSource();
        try
        {
            await WriteHelloAsync(client, process, "player-1", instance, DisplayTarget(1));
            if (!duringHandshake)
            {
                var welcome = await ReadAsync(client);
                Assert.Equal("welcome", welcome.MessageType);
                await WriteAsync(client, Frame("state_report", instance, welcome.OwnerEpoch, DisplayTarget(1), new StateReportDto
                {
                    SourceGeneration = 0,
                    State = System.Text.Json.JsonSerializer.SerializeToElement(new { playback_state = "idle" }),
                }));
            }
            await saveGate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));

            var stopping = broker.StopAsync(cancelFirstStop ? stopBudget.Token : CancellationToken.None);
            await saveGate.CancellationSeen.Task.WaitAsync(TimeSpan.FromSeconds(5));
            if (cancelFirstStop)
            {
                stopBudget.Cancel();
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => stopping.WaitAsync(TimeSpan.FromSeconds(5)));
                stopping = broker.StopAsync(CancellationToken.None);
            }
            // 这是截止时间断言，不是靠 sleep 猜测数据库何时释放：门禁尚未允许事务展开。
            await Assert.ThrowsAsync<TimeoutException>(() => stopping.WaitAsync(TimeSpan.FromMilliseconds(100)));
            var concurrentStop = broker.StopAsync(CancellationToken.None);
            Assert.False(concurrentStop.IsCompleted);
            saveGate.Release.TrySetResult();
            await Task.WhenAll(stopping, concurrentStop).WaitAsync(TimeSpan.FromSeconds(5));
        }
        finally
        {
            saveGate.Release.TrySetResult();
            await broker.StopAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));
            // 红阶段也等公开写入队列归还，避免失败断言留下测试自己持有的事务。
            await controlledWrites.ExecuteAsync((_, _) => Task.CompletedTask).WaitAsync(TimeSpan.FromSeconds(5));
        }
    }

    /// <summary>:returns: 仅投影写入使用可控的公开EF依赖，其它broker业务和管道均为生产实现。</summary>
    private static RuntimePipeBroker CreateDrainBroker(ControlHostFixture fixture, WriteCoordinator projectionWrites,
        RegisteredProcessRegistry registry, NamedPipeServer server, RuntimeAuthorityRepository authority)
    {
        var coordinator = new CommandCoordinator(fixture.Commands, new NullCommandWakeNotifier());
        var runtime = new RuntimeStateService(fixture.Database, fixture.Writes, coordinator, fixture.TimeProvider);
        var audio = new BackgroundAudioService(fixture.Database, fixture.Writes, coordinator, fixture.TimeProvider);
        var events = new SseEventHub(runtime, audio, new SseEventStreamOptions());
        var projections = new RuntimeProjectionPublisher(projectionWrites, events, fixture.TimeProvider);
        var leases = new CommandLeaseService(fixture.Commands, fixture.RuntimeAuthority,
            fixture.Database, fixture.Writes, fixture.TimeProvider);
        var dispatcher = new RuntimeMessageDispatcher(leases, new CommandResultService(fixture.Commands), projections);
        return new RuntimePipeBroker(server, registry, dispatcher, authority,
            NullLogger<RuntimePipeBroker>.Instance);
    }

    /// <summary>只改当前测试连接的EF拦截器，不改变SQLite池、事务或数据库文件。</summary>
    private sealed class InterceptedFactory(ControlDbContextFactory database, SaveUnwindGate gate)
        : IDbContextFactory<ControlDbContext>
    {
        public ControlDbContext CreateDbContext()
        {
            using var original = database.CreateDbContext();
            var options = new DbContextOptionsBuilder<ControlDbContext>()
                .UseSqlite(original.Database.GetConnectionString())
                .AddInterceptors(gate)
                .Options;
            return new ControlDbContext(options);
        }
    }

    /// <summary>模拟取消已收到但事务尚在展开的外部依赖；所有等待均有测试自有释放。</summary>
    private sealed class SaveUnwindGate : SaveChangesInterceptor
    {
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource CancellationSeen { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            using var registration = cancellationToken.Register(() => CancellationSeen.TrySetResult());
            Entered.TrySetResult();
            await Release.Task.ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            return result;
        }
    }
}
