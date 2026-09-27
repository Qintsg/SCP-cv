// 数据库初始化、旧数据保留与退役窗口命令清理回归。
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ScpCv.Domain.Model;
using ScpCv.Infrastructure.Configuration;
using ScpCv.Infrastructure.Persistence;

namespace ScpCv.Infrastructure.Tests;

public sealed class DatabaseInitializerTests
{
    [Fact]
    public void DataRootResolvesDedicatedControlDatabase()
    {
        var contentRoot = Path.Combine(Path.GetTempPath(), "scp-cv-content");
        var options = new DataRootOptions();

        var layout = options.Resolve(contentRoot);

        Assert.Equal(Path.GetFullPath(Path.Combine(contentRoot, "data", "dotnet")), layout.RootPath);
        Assert.Equal(Path.Combine(layout.RootPath, "control.db"), layout.DatabasePath);
        Assert.NotEqual(Path.GetFullPath(Path.Combine(contentRoot, "db.sqlite3")), layout.DatabasePath);
    }

    [Fact]
    public void DataRootRejectsLegacyDatabaseName()
    {
        var options = new DataRootOptions { DatabaseFileName = "db.sqlite3" };

        Assert.Throws<InvalidOperationException>(() => options.Resolve(Path.GetTempPath()));
    }

    [Fact]
    public async Task InitializeCreatesSchemaDefaultsAndSqlitePragmas()
    {
        var root = CreateTemporaryRoot();
        try
        {
            var factory = CreateFactory(root);
            var initializer = new DatabaseInitializer(factory);

            await initializer.InitializeAsync();

            Assert.True(File.Exists(factory.Layout.DatabasePath));
            await using var context = factory.CreateDbContext();
            Assert.Equal(2, await context.PlaybackSessions.CountAsync());
            Assert.Equal(1, await context.RuntimeStates.CountAsync());
            Assert.Equal(1, await context.BackgroundAudioStates.CountAsync());
            Assert.Equal(RuntimeGroupState.Stopped, (await context.RuntimeGroupControls.SingleAsync()).State);

            await context.Database.OpenConnectionAsync();
            await using var journalCommand = context.Database.GetDbConnection().CreateCommand();
            journalCommand.CommandText = "PRAGMA journal_mode;";
            Assert.Equal("wal", await journalCommand.ExecuteScalarAsync());
            await using var timeoutCommand = context.Database.GetDbConnection().CreateCommand();
            timeoutCommand.CommandText = "PRAGMA busy_timeout;";
            Assert.Equal(5_000L, Convert.ToInt64(await timeoutCommand.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture));
        }
        finally
        {
            DeleteTemporaryRoot(root);
        }
    }

    [Fact]
    public async Task InitializeRejectsAndPreservesIncompatibleExistingDatabase()
    {
        var root = CreateTemporaryRoot();
        try
        {
            var factory = CreateFactory(root);
            Directory.CreateDirectory(factory.Layout.RootPath);
            await using (var connection = new SqliteConnection($"Data Source={factory.Layout.DatabasePath}"))
            {
                await connection.OpenAsync();
                await using var command = connection.CreateCommand();
                command.CommandText = "CREATE TABLE legacy_data (value TEXT NOT NULL); INSERT INTO legacy_data VALUES ('keep-me');";
                await command.ExecuteNonQueryAsync();
            }

            var initializer = new DatabaseInitializer(factory);

            var error = await Assert.ThrowsAsync<InvalidOperationException>(() => initializer.InitializeAsync());
            Assert.Contains("不兼容", error.Message, StringComparison.Ordinal);

            await using var preserved = new SqliteConnection($"Data Source={factory.Layout.DatabasePath}");
            await preserved.OpenAsync();
            await using var check = preserved.CreateCommand();
            check.CommandText = "SELECT value FROM legacy_data;";
            Assert.Equal("keep-me", await check.ExecuteScalarAsync());
        }
        finally
        {
            DeleteTemporaryRoot(root);
        }
    }

    [Fact]
    public async Task ReinitializeKeepsCompatibleData()
    {
        var root = CreateTemporaryRoot();
        try
        {
            var factory = CreateFactory(root);
            var initializer = new DatabaseInitializer(factory);
            await initializer.InitializeAsync();
            await using (var context = factory.CreateDbContext())
            {
                context.UserAccounts.Add(new UserAccount { Username = "kept", PasswordHash = "hash" });
                await context.SaveChangesAsync();
            }

            await initializer.InitializeAsync();

            await using var verification = factory.CreateDbContext();
            Assert.True(await verification.UserAccounts.AnyAsync(user => user.Username == "kept"));
        }
        finally
        {
            DeleteTemporaryRoot(root);
        }
    }

    [Fact]
    public async Task ReinitializeRetiresOldWindowCommandsWithoutDeletingHistory()
    {
        var root = CreateTemporaryRoot();
        try
        {
            var factory = CreateFactory(root);
            var initializer = new DatabaseInitializer(factory);
            await initializer.InitializeAsync();
            await using (var context = factory.CreateDbContext())
            {
                context.PlaybackSessions.Add(new PlaybackSession
                {
                    WindowId = 3,
                    PlaybackState = PlaybackState.Playing,
                    PendingCommand = "OPEN",
                    LastUpdatedAt = DateTimeOffset.UtcNow,
                });
                context.CommandRecords.Add(new CommandRecord
                {
                    CommandId = Guid.NewGuid(),
                    TargetKind = CommandTargetKind.Display,
                    TargetId = 3,
                    TargetSequence = 1,
                    Command = "OPEN",
                    Status = CommandStatus.Pending,
                    CreatedAt = DateTimeOffset.UtcNow,
                });
                await context.SaveChangesAsync();
            }

            await initializer.InitializeAsync();

            await using var check = factory.CreateDbContext();
            var retiredSession = await check.PlaybackSessions.SingleAsync(item => item.WindowId == 3);
            var retiredCommand = await check.CommandRecords.SingleAsync(item => item.TargetId == 3);
            Assert.Equal(string.Empty, retiredSession.PendingCommand);
            Assert.Equal(CommandStatus.Superseded, retiredCommand.Status);
            Assert.Equal("retired_window", retiredCommand.ResultCode);
            Assert.Equal(3, await check.PlaybackSessions.CountAsync());
        }
        finally
        {
            DeleteTemporaryRoot(root);
        }
    }

    [Fact]
    public async Task ExistingRuntimeStateMigratesLayoutDraftWithoutLosingModeOrVolume()
    {
        var root = CreateTemporaryRoot();
        try
        {
            var factory = CreateFactory(root);
            Directory.CreateDirectory(factory.Layout.RootPath);
            await using (var legacy = factory.CreateDbContext())
            {
                var migrator = legacy.Database.GetService<IMigrator>();
                await migrator.MigrateAsync("20260910011645_AddAudioGenerationFence");
                await legacy.Database.ExecuteSqlRawAsync(
                    "INSERT INTO runtime_state (Id, BigScreenMode, VolumeLevel, VolumeMuted, UpdatedAt) VALUES (1, 'Double', 77, 0, 0)");
            }

            await new DatabaseInitializer(factory).InitializeAsync();

            await using var current = factory.CreateDbContext();
            var runtime = await current.RuntimeStates.SingleAsync();
            Assert.Equal(BigScreenMode.Double, runtime.BigScreenMode);
            Assert.Equal(77, runtime.VolumeLevel);
            Assert.Equal("{}", runtime.WallLayoutDraftJson);
            Assert.Equal(0, runtime.WallLayoutDraftRevision);
        }
        finally
        {
            DeleteTemporaryRoot(root);
        }
    }

    private static ControlDbContextFactory CreateFactory(string root) =>
        new(new DataRootOptions { RootPath = root }, root);

    private static string CreateTemporaryRoot() =>
        Path.Combine(Path.GetTempPath(), "scp-cv-tests", Guid.NewGuid().ToString("N"));

    private static void DeleteTemporaryRoot(string root)
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
