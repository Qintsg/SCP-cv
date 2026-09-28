// 故障恢复测试的可控原生观察、提交失败和真 SQLite/pipe 共用夹具。
using System.Diagnostics;
using System.Data.Common;
using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using ScpCv.Contracts.Ipc;
using ScpCv.Contracts.Runtime;
using ScpCv.ControlHost.Events;
using ScpCv.ControlHost.Ipc;
using ScpCv.Domain.Model;
using ScpCv.Infrastructure.Audio;
using ScpCv.Infrastructure.Commands;
using ScpCv.Infrastructure.Playback;
using ScpCv.Infrastructure.Persistence;
using ScpCv.Infrastructure.Runtime;
using ScpCv.Integration.Tests.Fixtures;

namespace ScpCv.Integration.Tests;

public sealed partial class SupervisorExitRecoveryTests
{
    /// <summary>只模拟事务已提交后的回执异常，真实数据库状态保持已提交。</summary>
    private sealed class LostFaultCommitReceiptInterceptor : DbTransactionInterceptor
    {
        private int _failed;
        public TaskCompletionSource Failed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>首次确认提交后丢失回执，后续核对事务正常返回。</summary>
        public override Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData,
            CancellationToken cancellationToken = default)
        {
            if (Interlocked.Exchange(ref _failed, 1) == 0)
            {
                Failed.TrySetResult();
                throw new IOException("故障状态已提交，但首次回执丢失");
            }
            return Task.CompletedTask;
        }
    }

    /// <summary>故障注入只拦截真实 Faulted 状态保存，不替代产品权限或 SQLite 事务。</summary>
    private sealed class FaultCommitInterceptor(bool holdFailures = false) : SaveChangesInterceptor
    {
        private int _holdFailures = holdFailures ? 1 : 0;
        private int _attempts;
        public TaskCompletionSource Failed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int Attempts => Volatile.Read(ref _attempts);
        public void Release() => Volatile.Write(ref _holdFailures, 0);

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (eventData.Context?.ChangeTracker.Entries<RuntimeGroupControl>().Any(entry =>
                entry.State == EntityState.Modified && entry.Entity.State == RuntimeGroupState.Faulted) == true)
            {
                var attempt = Interlocked.Increment(ref _attempts);
                if (attempt == 1 || Volatile.Read(ref _holdFailures) != 0)
                {
                    Failed.TrySetResult();
                    throw new IOException("受控故障状态首次保存异常");
                }
            }
            return ValueTask.FromResult(result);
        }
    }

    /// <summary>独立测试库的 EF 故障 seam；没有伪造返回值或跳过事务。</summary>
    private sealed class InterceptingFactory(string path, IInterceptor interceptor) : IDbContextFactory<ControlDbContext>
    {
        public ControlDbContext CreateDbContext() => new(new DbContextOptionsBuilder<ControlDbContext>()
            .UseSqlite($"Data Source={path};Foreign Keys=True").AddInterceptors(interceptor).Options);
    }

    /// <summary>只有原生观察变化；产品 broker、OS 管道认证与持久化均使用真实实现。</summary>
    private sealed class ControlledExitObserver : IRuntimeProcessExitObserver
    {
        private readonly TaskCompletionSource<RuntimeProcessExitEvidence> _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _calls;
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int Calls => Volatile.Read(ref _calls);
        public bool Cancelled { get; private set; }
        /// <summary>投递本轮受控死亡证据，不触碰 OS。</summary>
        public void Complete(RuntimeProcessExitEvidence evidence) => _completion.TrySetResult(evidence);
        /// <summary>投递观察异常，检查产品诊断而非假成功。</summary>
        public void Fail(Exception exception) => _completion.TrySetException(exception);

        /// <summary>按宿主取消或显式证据结束，避免把 pipe EOF 用作死亡信号。</summary>
        public async Task<RuntimeProcessExitEvidence> WaitForExitAsync(RegisteredProcessIdentity identity, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _calls);
            Started.TrySetResult();
            try { return await _completion.Task.WaitAsync(cancellationToken); }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                Cancelled = true;
                throw;
            }
        }
    }

    /// <summary>固定事件标记建立确定性等待，不以睡眠猜测后台事务已执行。</summary>
    private sealed class RecordingLogger : ILogger<RuntimePipeBroker>
    {
        private readonly ConcurrentDictionary<int, TaskCompletionSource> _events = new();
        /// <summary>本测试不需要外部日志 scope。</summary>
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        /// <summary>保留全部结构化事件供确定性断言。</summary>
        public bool IsEnabled(LogLevel logLevel) => true;
        /// <summary>仅记录事件标识，不存储可能含敏感信息的日志载荷。</summary>
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            _events.GetOrAdd(eventId.Id, _ => new(TaskCreationOptions.RunContinuationsAsynchronously)).TrySetResult();
        /// <summary>等待指定产品日志分支真正到达。</summary>
        public Task WaitForAsync(int eventId) => _events.GetOrAdd(eventId,
            _ => new(TaskCreationOptions.RunContinuationsAsynchronously)).Task.WaitAsync(TimeSpan.FromSeconds(5));
    }

    /// <summary>创建独立测试库及当前测试进程的真管道身份，不创建 Supervisor 或 Office。</summary>
    private sealed class ExitContext : IAsyncDisposable
    {
        /// <summary>装配真实 broker/投影及只替代原生退出的观察器。</summary>
        private readonly WriteCoordinator? _faultWrites;
        private ExitContext(ControlHostFixture fixture, long groupEpoch, WorkerOwnership player, IInterceptor? faultInterceptor)
        {
            Fixture = fixture;
            GroupEpoch = groupEpoch;
            Player = player;
            using var current = Process.GetCurrentProcess();
            Identity = new(current.Id, new DateTimeOffset(current.StartTime.ToUniversalTime(), TimeSpan.Zero),
                current.SessionId, "supervisor", Guid.NewGuid());
            var registry = new RegisteredProcessRegistry();
            registry.Register(Identity);
            Server = new NamedPipeServer(Guid.NewGuid(), current.SessionId, registry);
            var coordinator = new CommandCoordinator(fixture.Commands, new NullCommandWakeNotifier());
            var runtime = new RuntimeStateService(fixture.Database, fixture.Writes, coordinator, fixture.TimeProvider);
            var audio = new BackgroundAudioService(fixture.Database, fixture.Writes, coordinator, fixture.TimeProvider);
            Events = new SseEventHub(runtime, audio, new SseEventStreamOptions());
            var publisher = new RuntimeProjectionPublisher(fixture.Writes, Events, fixture.TimeProvider);
            var dispatcher = new RuntimeMessageDispatcher(
                new CommandLeaseService(fixture.Commands, fixture.RuntimeAuthority, fixture.Database, fixture.Writes, fixture.TimeProvider),
                new CommandResultService(fixture.Commands), publisher);
            var authority = fixture.RuntimeAuthority;
            if (faultInterceptor is not null)
            {
                var factory = new InterceptingFactory(fixture.Database.Layout.DatabasePath, faultInterceptor);
                _faultWrites = new WriteCoordinator(factory);
                authority = new RuntimeAuthorityRepository(factory, _faultWrites, fixture.TimeProvider);
            }
            Broker = new RuntimePipeBroker(Server, registry, dispatcher, authority,
                Logger, processExitObserver: Observer);
        }

        public ControlHostFixture Fixture { get; }
        public long GroupEpoch { get; }
        public WorkerOwnership Player { get; }
        public RegisteredProcessIdentity Identity { get; }
        public ControlledExitObserver Observer { get; } = new();
        public NamedPipeServer Server { get; }
        public SseEventHub Events { get; }
        public RecordingLogger Logger { get; } = new();
        public RuntimePipeBroker Broker { get; }

        /// <summary>初始化独立运行组和两窗状态，不启用实际播放资源。</summary>
        public static async Task<ExitContext> CreateAsync(IInterceptor? faultInterceptor = null)
        {
            var fixture = await ControlHostFixture.CreateAsync();
            var request = Guid.NewGuid();
            var starting = await fixture.RuntimeAuthority.BeginStartAsync(request);
            await fixture.RuntimeAuthority.ArmAsync(request, starting.GroupEpoch);
            var player = await fixture.RuntimeAuthority.RegisterWorkerAsync(new RegisterWorker(
                CommandTargetKind.Display, 1, Guid.NewGuid(), 101, fixture.TimeProvider.GetUtcNow(), 1, starting.GroupEpoch, "[]"));
            await fixture.RuntimeAuthority.RegisterWorkerAsync(new RegisterWorker(
                CommandTargetKind.Display, 2, Guid.NewGuid(), 102, fixture.TimeProvider.GetUtcNow(), 1, starting.GroupEpoch, "[]"));
            await fixture.Writes.ExecuteAsync(async (database, token) =>
            {
                foreach (var session in await database.PlaybackSessions.Where(session => session.WindowId <= 2).ToListAsync(token))
                {
                    session.PlaybackState = PlaybackState.Playing;
                    session.PlayerLastSeenAt = fixture.TimeProvider.GetUtcNow();
                }
            });
            var context = new ExitContext(fixture, starting.GroupEpoch, player, faultInterceptor);
            await context.Broker.StartAsync(CancellationToken.None);
            return context;
        }

        /// <summary>经过 OS PID/start/session 校验的真实管道握手。</summary>
        public async Task<RuntimePipeClient> ConnectSupervisorAsync()
        {
            var client = new RuntimePipeClient(Server.PipeName);
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await client.ConnectAsync(timeout.Token);
            var welcome = await client.ExchangeAsync(new IpcFrameDto
            {
                MessageType = "hello", MessageId = Guid.NewGuid(), InstanceId = Identity.InstanceId,
                Payload = JsonSerializer.SerializeToElement(new HelloDto
                {
                    Role = Identity.Role, ProcessId = Identity.ProcessId,
                    ProcessStartTime = Identity.ProcessStartTime.ToString("O"), LogonSessionId = Identity.LogonSessionId,
                }),
            }, timeout.Token);
            Assert.Equal("welcome", welcome.MessageType);
            return client;
        }

        /// <summary>轮询真实持久状态，预算结束仍保留严格失败断言。</summary>
        public async Task WaitForStateAsync(RuntimeGroupState expected)
        {
            using var budget = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            while (!budget.IsCancellationRequested)
            {
                if ((await Fixture.RuntimeAuthority.GetGroupAsync()).State == expected) return;
                await Task.Delay(25);
            }
            Assert.Equal(expected, (await Fixture.RuntimeAuthority.GetGroupAsync()).State);
        }

        /// <summary>先收回观察任务，再清理精确测试库。</summary>
        public async ValueTask DisposeAsync()
        {
            await Broker.StopAsync(CancellationToken.None);
            Broker.Dispose();
            _faultWrites?.Dispose();
            await Fixture.DisposeAsync();
        }
    }
}
