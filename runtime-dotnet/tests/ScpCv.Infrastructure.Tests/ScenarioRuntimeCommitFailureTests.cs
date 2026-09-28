// 预案在假的硬件动作之后遇到 SQLite 提交失败时的状态与重放边界。
using System.Data.Common;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using ScpCv.Contracts.Http;
using ScpCv.Infrastructure.Commands;
using ScpCv.Infrastructure.Configuration;
using ScpCv.Infrastructure.Persistence;
using ScpCv.Infrastructure.Playback;
using ScpCv.Infrastructure.Scenarios;
using ScpCv.Infrastructure.VideoWall;

namespace ScpCv.Infrastructure.Tests;

public sealed class ScenarioRuntimeCommitFailureTests
{
    /// <summary>
    /// 提交前失败和提交后回执异常都不得宣称预案完成，也不得自动重放外部动作。
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PersistenceFailureReportsUnconfirmedStateAndStopsPlaybackCommands(bool afterCommit)
    {
        using var store = await TestStore.CreateAsync();
        var scenario = await store.Scenarios.CreateAsync(new ScenarioWriteModel("提交失败预案", "", "set", "double", "set", 42,
            [new ScenarioTargetDto { WindowId = 1, SourceState = "empty" }]));
        var factory = new InterceptingFactory(store.Factory.Layout.DatabasePath,
            afterCommit ? new FailCommitReceipt() : new FailSave());
        using var writes = new WriteCoordinator(factory);
        var audio = new FakeAudio();
        var wall = new FakeWall();
        var scenarios = CreateScenarios(factory, writes, wall, audio);

        var error = await Assert.ThrowsAsync<ScenarioServiceException>(() => scenarios.ActivateAsync(scenario.Id));

        Assert.Equal("scenario_state_persistence_failed", error.Code);
        Assert.Contains("提交未确认", error.Message);
        Assert.Contains("墙面", error.Message);
        Assert.Contains("系统音量", error.Message);
        Assert.Equal(["double"], wall.Modes);
        Assert.Equal([(42, (bool?)null)], audio.Requests);
        var state = await store.Runtime.GetRuntimeAsync();
        Assert.Equal(afterCommit ? "double" : "single", state.BigScreenMode);
        Assert.Equal(afterCommit ? 39 : 100, state.VolumeLevel);
        Assert.Equal(afterCommit, (await store.Runtime.GetSystemVolumeAsync()).Muted);
        Assert.All(await store.Runtime.GetSessionsAsync(), session => Assert.Empty(session.PendingCommand));
    }

    /// <summary>墙面完成后的取消不能继续写声卡或数据库。</summary>
    [Fact]
    public async Task CancellationAfterWallDispatchStopsBeforeAudioWrite()
    {
        using var store = await TestStore.CreateAsync();
        var scenario = await store.Scenarios.CreateAsync(new ScenarioWriteModel("取消预案", "", "set", "double", "set", 42, []));
        using var cancellation = new CancellationTokenSource();
        var wall = new FakeWall(cancellation.Cancel);
        var audio = new FakeAudio();
        var scenarios = CreateScenarios(store.Factory, store.Writes, wall, audio);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => scenarios.ActivateAsync(scenario.Id, cancellation.Token));

        Assert.Empty(audio.Requests);
        Assert.Equal("single", (await store.Runtime.GetRuntimeAsync()).BigScreenMode);
        Assert.Equal(100, (await store.Runtime.GetSystemVolumeAsync()).Level);
    }

    /// <summary>仅替换 SQLite 与外部硬件接口，业务链路保持真实。</summary>
    private static ScenarioService CreateScenarios(
        IDbContextFactory<ControlDbContext> factory, WriteCoordinator writes, IVideoWallController wall, ISystemAudioController audio) =>
        new(factory, writes, new CommandCoordinator(new CommandRepository(writes), new NullCommandWakeNotifier()),
            videoWall: wall, systemAudio: audio);

    private sealed class FakeAudio : ISystemAudioController
    {
        public bool IsHardware => true;
        public List<(int? Level, bool? Muted)> Requests { get; } = [];

        /// <summary>返回假的观测状态。</summary>
        public SystemAudioSnapshot GetCurrent() => new(true, 39, true, "fake_core_audio");

        /// <summary>记录请求；此处不触碰声卡。</summary>
        public SystemAudioSnapshot Apply(int? level, bool? muted)
        {
            Requests.Add((level, muted));
            return GetCurrent();
        }
    }

    private sealed class FakeWall(Action? onDispatch = null) : IVideoWallController
    {
        public bool IsHardware => true;
        public List<string> Modes { get; } = [];

        /// <summary>记录已完成的墙面替身动作。</summary>
        public Task DispatchAsync(string bigScreenMode, CancellationToken cancellationToken = default)
        {
            Modes.Add(bigScreenMode);
            onDispatch?.Invoke();
            return Task.CompletedTask;
        }
    }

    private sealed class FailSave : SaveChangesInterceptor
    {
        /// <summary>在 SQLite 提交前注入失败。</summary>
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default) =>
            throw new DbUpdateException("故障注入：运行状态提交前失败");
    }

    private sealed class FailCommitReceipt : DbTransactionInterceptor
    {
        /// <summary>模拟 SQLite 已提交但提交完成回执异常。</summary>
        public override Task TransactionCommittedAsync(
            DbTransaction transaction, TransactionEndEventData eventData, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("故障注入：运行状态已提交但回执失败");
    }

    private sealed class InterceptingFactory(string path, IInterceptor interceptor) : IDbContextFactory<ControlDbContext>
    {
        /// <summary>使用临时 SQLite 库并注入持久化故障。</summary>
        public ControlDbContext CreateDbContext() => new(new DbContextOptionsBuilder<ControlDbContext>()
            .UseSqlite(new SqliteConnectionStringBuilder { DataSource = path, ForeignKeys = true }.ToString())
            .AddInterceptors(interceptor).Options);
    }

    private sealed class TestStore : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), "scp-cv-scenario-volume", Guid.NewGuid().ToString("N"));
        public ControlDbContextFactory Factory { get; }
        public WriteCoordinator Writes { get; }
        public ScenarioService Scenarios { get; }
        public RuntimeStateService Runtime { get; }

        /// <summary>各测试使用独立临时库和无外部副作用的运行态入口。</summary>
        private TestStore()
        {
            Factory = new ControlDbContextFactory(new DataRootOptions { RootPath = _root }, _root);
            Writes = new WriteCoordinator(Factory);
            var commands = new CommandCoordinator(new CommandRepository(Writes), new NullCommandWakeNotifier());
            Scenarios = new ScenarioService(Factory, Writes, commands);
            Runtime = new RuntimeStateService(Factory, Writes, commands);
        }

        /// <summary>初始化真实数据库与种子数据。</summary>
        public static async Task<TestStore> CreateAsync()
        {
            var store = new TestStore();
            await new DatabaseInitializer(store.Factory).InitializeAsync();
            return store;
        }

        /// <summary>精确验证临时目录归属后清理本测试创建的数据。</summary>
        public void Dispose()
        {
            Writes.Dispose();
            SqliteConnection.ClearAllPools();
            var fullRoot = Path.GetFullPath(_root);
            var expectedParent = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "scp-cv-scenario-volume"));
            if (!string.Equals(Path.GetDirectoryName(fullRoot), expectedParent, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("拒绝清理测试根目录之外的路径。");
            if (Directory.Exists(fullRoot)) Directory.Delete(fullRoot, recursive: true);
        }
    }
}
