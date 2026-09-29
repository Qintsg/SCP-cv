// 真实 SQLite 集成夹具：拥有独立 GUID 目录和实际连接串池，释放不影响其它夹具。
using ScpCv.Infrastructure.Commands;
using ScpCv.Infrastructure.Configuration;
using ScpCv.Infrastructure.Persistence;
using ScpCv.Infrastructure.Runtime;
using ScpCv.Integration.Tests.Fakes;

namespace ScpCv.Integration.Tests.Fixtures;

public sealed class ControlHostFixture : IAsyncDisposable
{
    private readonly string _databaseConnectionString;
    private bool _temporaryRootCleaned;

    private ControlHostFixture(
        string temporaryRoot,
        string databaseConnectionString,
        DeterministicTimeProvider timeProvider,
        ControlDbContextFactory database,
        WriteCoordinator writes)
    {
        TemporaryRoot = temporaryRoot;
        _databaseConnectionString = databaseConnectionString;
        TimeProvider = timeProvider;
        Database = database;
        Writes = writes;
        Commands = new CommandRepository(writes, timeProvider);
        RuntimeAuthority = new RuntimeAuthorityRepository(database, writes, timeProvider);
    }

    public string TemporaryRoot { get; }
    public DeterministicTimeProvider TimeProvider { get; }
    public ControlDbContextFactory Database { get; }
    public WriteCoordinator Writes { get; }
    public CommandRepository Commands { get; }
    public RuntimeAuthorityRepository RuntimeAuthority { get; }
    public FakeOfficeHost Office { get; } = new();
    public FakeDeviceAdapter Devices { get; } = new();

    public static async Task<ControlHostFixture> CreateAsync()
    {
        var root = Path.Combine(Path.GetTempPath(), "scp-cv-integration", Guid.NewGuid().ToString("N"));
        var time = new DeterministicTimeProvider(new DateTimeOffset(2026, 9, 8, 0, 0, 0, TimeSpan.Zero));
        var database = new ControlDbContextFactory(new DataRootOptions { RootPath = root }, root);
        var connectionString = TestDatabaseLifetime.CaptureConnectionString(database);
        try
        {
            await new DatabaseInitializer(database, time).InitializeAsync();
            return new ControlHostFixture(root, connectionString, time, database, new WriteCoordinator(database));
        }
        catch
        {
            TestDatabaseLifetime.ClearOwnedPoolAndDeleteRoot(root, "scp-cv-integration", connectionString);
            throw;
        }
    }

    public ValueTask DisposeAsync()
    {
        if (_temporaryRootCleaned) return ValueTask.CompletedTask;
        Writes.Dispose();
        TestDatabaseLifetime.ClearOwnedPoolAndDeleteRoot(
            TemporaryRoot, "scp-cv-integration", _databaseConnectionString);
        _temporaryRootCleaned = true;

        GC.SuppressFinalize(this);
        return ValueTask.CompletedTask;
    }
}
