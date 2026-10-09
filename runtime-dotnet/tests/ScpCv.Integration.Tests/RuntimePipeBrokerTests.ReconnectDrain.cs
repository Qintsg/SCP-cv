// 同实例重连须先归还旧连接自有分派；真实管道/提交回执门禁，不持SQLite写锁或启动原生组件。
using System.Data.Common;
using System.Diagnostics;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using ScpCv.Contracts.Ipc;
using ScpCv.ControlHost.Ipc;
using ScpCv.Infrastructure.Persistence;
using ScpCv.Infrastructure.Runtime;
using ScpCv.Integration.Tests.Fixtures;

namespace ScpCv.Integration.Tests;

public sealed partial class RuntimePipeBrokerTests
{
    /// <summary>:param disconnectAndWake: 旧pipe断开后先发失败Wake。:returns: 旧分派未展开时不能绕过退休提前welcome。</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SameInstanceReconnectWaitsForOldCommittedDispatchBeforeBecomingReady(bool disconnectAndWake)
    {
        await using var fixture = await ControlHostFixture.CreateAsync();
        var request = Guid.NewGuid();
        var group = await fixture.RuntimeAuthority.BeginStartAsync(request);
        await fixture.RuntimeAuthority.ArmAsync(request, group.GroupEpoch);
        var oldCommit = new ReconnectCommitGate();
        var newRegistration = new ReconnectCommitSignal();
        using var projectionWrites = new WriteCoordinator(new ReconnectFactory(fixture.Database, oldCommit));
        using var authorityWrites = new WriteCoordinator(new ReconnectFactory(fixture.Database, newRegistration));
        var authority = new RuntimeAuthorityRepository(fixture.Database, authorityWrites, fixture.TimeProvider);
        using var process = Process.GetCurrentProcess();
        var instance = Guid.NewGuid();
        var registry = new RegisteredProcessRegistry();
        registry.Register(CurrentIdentity(process, "player-1", instance));
        var server = new NamedPipeServer(Guid.NewGuid(), process.SessionId, registry);
        using var broker = CreateDrainBroker(fixture, projectionWrites, registry, server, authority);
        await broker.StartAsync(CancellationToken.None);
        await using var first = await ConnectAsync(server.PipeName);
        try
        {
            await WriteHelloAsync(first, process, "player-1", instance, DisplayTarget(1));
            var welcome = await ReadAsync(first);
            Assert.Equal("welcome", welcome.MessageType);
            await WriteAsync(first, Frame("worker_ready", instance, welcome.OwnerEpoch, DisplayTarget(1), ReadyForReconnect()));
            Assert.True((await ReadAsync(first)).Payload.GetProperty("accepted").GetBoolean());

            oldCommit.Arm();
            await WriteAsync(first, Frame("state_report", instance, welcome.OwnerEpoch, DisplayTarget(1), new StateReportDto
            {
                SourceGeneration = 0,
                State = JsonSerializer.SerializeToElement(new { playback_state = "idle" }),
            }));
            await oldCommit.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));

            if (disconnectAndWake)
            {
                await first.DisposeAsync();
                await broker.WakeAsync(new ScpCv.Infrastructure.Commands.CommandWakeSignal(
                    ScpCv.Domain.Model.CommandTargetKind.Display, 1, 47)).AsTask().WaitAsync(TimeSpan.FromSeconds(5));
            }

            // 独立writer的下一次提交证明新hello已登记，排除“SQLite锁使重连看似在等待”的假绿。
            newRegistration.Arm();
            await using var replacement = await ConnectAsync(server.PipeName);
            await WriteHelloAsync(replacement, process, "player-1", instance, DisplayTarget(1));
            await newRegistration.Seen.Task.WaitAsync(TimeSpan.FromSeconds(5));
            var replacedWelcome = ReadAsync(replacement);
            await Assert.ThrowsAsync<TimeoutException>(() => replacedWelcome.WaitAsync(TimeSpan.FromMilliseconds(200)));
            Assert.False(oldCommit.Release.Task.IsCompleted, "旧分派门禁尚未允许展开。");

            oldCommit.Release.TrySetResult();
            await oldCommit.Exited.Task.WaitAsync(TimeSpan.FromSeconds(5));
            var reconnected = await replacedWelcome.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal("welcome", reconnected.MessageType);
            Assert.Equal(welcome.OwnerEpoch, reconnected.OwnerEpoch);
            await WriteAsync(replacement, Frame("worker_ready", instance, reconnected.OwnerEpoch, DisplayTarget(1), ReadyForReconnect()));
            Assert.True((await ReadAsync(replacement)).Payload.GetProperty("accepted").GetBoolean());
            Assert.DoesNotContain("player-1", broker.GetRuntimeReadiness(group.GroupEpoch).MissingRoles);
            await broker.WakeAsync(new ScpCv.Infrastructure.Commands.CommandWakeSignal(
                ScpCv.Domain.Model.CommandTargetKind.Display, 1, 48));
            Assert.Equal("wake", (await ReadAsync(replacement)).MessageType);
            Assert.DoesNotContain("player-1", broker.GetRuntimeReadiness(group.GroupEpoch).MissingRoles);
        }
        finally
        {
            oldCommit.Release.TrySetResult();
            await broker.StopAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));
            await projectionWrites.ExecuteAsync((_, _) => Task.CompletedTask).WaitAsync(TimeSpan.FromSeconds(5));
            await authorityWrites.ExecuteAsync((_, _) => Task.CompletedTask).WaitAsync(TimeSpan.FromSeconds(5));
        }
    }

    /// <summary>:param database: 独立自有SQLite根。:param interceptor: 仅本writer的外部提交观察。:returns: 生产配置且附加受控EF接缝。</summary>
    private sealed class ReconnectFactory(ControlDbContextFactory database, IInterceptor interceptor)
        : IDbContextFactory<ControlDbContext>
    {
        /// <summary>:returns: 同自有连接串的上下文；不清全局池、不替换生产写入逻辑。</summary>
        public ControlDbContext CreateDbContext()
        {
            using var original = database.CreateDbContext();
            var options = new DbContextOptionsBuilder<ControlDbContext>()
                .UseSqlite(original.Database.GetConnectionString()).AddInterceptors(interceptor).Options;
            return new ControlDbContext(options);
        }
    }

    /// <summary>只在实际COMMIT之后保持回执在途；原生写锁已释放，测试仍能独立握手。</summary>
    private sealed class ReconnectCommitGate : DbTransactionInterceptor
    {
        private int _armed;
        internal TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource Exited { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>:returns: 只阻塞下一次本writer的真实提交回执。</summary>
        internal void Arm() => Interlocked.Exchange(ref _armed, 1);

        /// <summary>:param transaction: 已提交事务。:param eventData: EF实证。:param cancellationToken: caller寿命。:returns: 明确自有释放后才归还分派。</summary>
        public override async Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData,
            CancellationToken cancellationToken = default)
        {
            if (Interlocked.Exchange(ref _armed, 0) != 1) return;
            Entered.TrySetResult();
            try { await Release.Task.WaitAsync(TimeSpan.FromSeconds(10), CancellationToken.None).ConfigureAwait(false); }
            finally { Exited.TrySetResult(); }
        }
    }

    /// <summary>新hello的独立提交实证，不阻塞该事务或扩大生产握手预算。</summary>
    private sealed class ReconnectCommitSignal : DbTransactionInterceptor
    {
        private int _armed;
        internal TaskCompletionSource Seen { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>:returns: 只观察下一次authority writer提交。</summary>
        internal void Arm() => Interlocked.Exchange(ref _armed, 1);

        /// <summary>:param transaction: 已提交事务。:param eventData: EF实证。:param cancellationToken: 寿命。:returns: 提交完成信号，不暂停事务。</summary>
        public override Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData,
            CancellationToken cancellationToken = default)
        {
            if (Interlocked.Exchange(ref _armed, 0) == 1) Seen.TrySetResult();
            return Task.CompletedTask;
        }
    }
}
