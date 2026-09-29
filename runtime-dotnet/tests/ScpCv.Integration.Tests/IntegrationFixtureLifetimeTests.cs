// Integration 夹具释放隔离回归：其它数据库的空闲连接池不得被自有清理清空。
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using System.Net;
using ScpCv.Integration.Tests.Fixtures;

namespace ScpCv.Integration.Tests;

public sealed class IntegrationFixtureLifetimeTests
{
    [Fact]
    public async Task DisposingFixturePreservesUnrelatedPoolAndDeletesOnlyItsOwnDirectory()
    {
        await using var survivor = await ControlHostFixture.CreateAsync();
        var retiring = await ControlHostFixture.CreateAsync();
        var retired = false;
        using var database = survivor.Database.CreateDbContext();
        // 与 EF 的池键不同，避免其它租用者影响 TEMP 标记；数据文件仍归存活夹具。
        var options = new SqliteConnectionStringBuilder(database.Database.GetConnectionString())
        {
            DefaultTimeout = 37,
        };
        using var probe = new SqliteConnection(options.ToString());
        try
        {
            await probe.OpenAsync();
            using (var marker = probe.CreateCommand())
            {
                marker.CommandText = "CREATE TEMP TABLE integration_pool_marker (value INTEGER);";
                await marker.ExecuteNonQueryAsync();
            }
            await probe.CloseAsync();

            await retiring.DisposeAsync();
            retired = true;
            Assert.False(Directory.Exists(retiring.TemporaryRoot));
            Assert.True(Directory.Exists(survivor.TemporaryRoot));
            await probe.OpenAsync();
            using var readMarker = probe.CreateCommand();
            readMarker.CommandText = "SELECT COUNT(*) FROM sqlite_temp_master WHERE name = 'integration_pool_marker';";
            Assert.Equal(1L, await readMarker.ExecuteScalarAsync());
        }
        finally
        {
            await probe.CloseAsync();
            SqliteConnection.ClearPool(probe);
            if (!retired) await retiring.DisposeAsync();
        }
    }

    [Theory]
    [InlineData("development-data")]
    [InlineData("security-host")]
    [InlineData("hardware-child")]
    public async Task OtherTestOwnerCleanupPreservesUnrelatedConnectionPool(string owner)
    {
        await using var survivor = await ControlHostFixture.CreateAsync();
        using var database = survivor.Database.CreateDbContext();
        // timeout=37 的池独立于存活夹具 EF 的默认池，不受背景读写的租用顺序影响。
        var options = new SqliteConnectionStringBuilder(database.Database.GetConnectionString())
        {
            DefaultTimeout = 37,
        };
        using var probe = new SqliteConnection(options.ToString());
        try
        {
            await probe.OpenAsync();
            using (var marker = probe.CreateCommand())
            {
                marker.CommandText = "CREATE TEMP TABLE other_owner_pool_marker (value INTEGER);";
                await marker.ExecuteNonQueryAsync();
            }
            await probe.CloseAsync();

            switch (owner)
            {
                case "development-data":
                    await new DevelopmentDataTests().FreshDirectoryInitializesIdempotently();
                    break;
                case "security-host":
                    using (var factory = new SecurityApplicationFactory())
                    using (var client = factory.CreateClient())
                    using (var response = await client.GetAsync("/health/ready"))
                    {
                        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                    }
                    break;
                case "hardware-child":
                    await new HardwareControlHostStartupTests()
                        .HardwareControlHostReachesReadyEndpointWithoutDependencyDeadlock();
                    break;
                default:
                    throw new InvalidOperationException($"未知测试所有者：{owner}");
            }

            await probe.OpenAsync();
            using var readMarker = probe.CreateCommand();
            readMarker.CommandText = "SELECT COUNT(*) FROM sqlite_temp_master WHERE name = 'other_owner_pool_marker';";
            Assert.Equal(1L, await readMarker.ExecuteScalarAsync());
            Assert.True(Directory.Exists(survivor.TemporaryRoot));
        }
        finally
        {
            await probe.CloseAsync();
            SqliteConnection.ClearPool(probe);
        }
    }

    [Fact]
    public async Task FixtureCleanupDoesNotInterruptConcurrentIndependentMigration()
    {
        for (var iteration = 0; iteration < 8; iteration++)
        {
            await using var retiring = await ControlHostFixture.CreateAsync();
            var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var creating = Task.Run(async () =>
            {
                await release.Task;
                return await ControlHostFixture.CreateAsync();
            });
            var disposing = Task.Run(async () =>
            {
                await release.Task;
                await retiring.DisposeAsync();
            });
            release.SetResult();
            await using var survivor = await creating;
            await disposing;
            Assert.False(Directory.Exists(retiring.TemporaryRoot));
            using var database = survivor.Database.CreateDbContext();
            Assert.Equal(database.Database.GetMigrations(), await database.Database.GetAppliedMigrationsAsync());
            var actualWindows = await database.PlaybackSessions
                .OrderBy(session => session.WindowId).Select(session => session.WindowId).ToArrayAsync();
            Assert.Equal([1, 2], actualWindows);
        }
    }
}
