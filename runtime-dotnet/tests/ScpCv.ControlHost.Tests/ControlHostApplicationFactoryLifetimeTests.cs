// 测试主机释放范围回归：仅清理自有连接池和 GUID 临时目录，保持并行主机可用。
using System.Net;
using System.Net.Http.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ScpCv.Infrastructure.Persistence;

namespace ScpCv.ControlHost.Tests;

public sealed class ControlHostApplicationFactoryLifetimeTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DisposingFactoryPreservesOtherFactoryPoolAndDeletesOnlyItsOwnDirectory(bool asynchronously)
    {
        using var survivor = new ControlHostApplicationFactory();
        using var survivorClient = survivor.CreateHttpsClient();
        using var retiring = new ControlHostApplicationFactory();
        using var retiringClient = retiring.CreateHttpsClient();
        var contextFactory = survivor.Services.GetRequiredService<IDbContextFactory<ControlDbContext>>();
        using var database = contextFactory.CreateDbContext();
        // 独立连接串使标记不受后台服务借还连接影响，数据库仍属于存活主机。
        var probeOptions = new SqliteConnectionStringBuilder(database.Database.GetConnectionString())
        {
            DefaultTimeout = 37,
        };
        using var probe = new SqliteConnection(probeOptions.ToString());
        try
        {
            probe.Open();
            using (var createMarker = probe.CreateCommand())
            {
                createMarker.CommandText = "CREATE TEMP TABLE fixture_pool_marker (value INTEGER);";
                createMarker.ExecuteNonQuery();
            }
            probe.Close();

            if (asynchronously)
            {
                await retiring.DisposeAsync();
            }
            else
            {
                retiring.Dispose();
            }

            Assert.False(Directory.Exists(retiring.TemporaryRoot));
            Assert.True(Directory.Exists(survivor.TemporaryRoot));
            probe.Open();
            using var readMarker = probe.CreateCommand();
            readMarker.CommandText = "SELECT COUNT(*) FROM sqlite_temp_master WHERE name = 'fixture_pool_marker';";
            Assert.Equal(1L, readMarker.ExecuteScalar());

            using var login = await LoginAsync(survivorClient);
            Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        }
        finally
        {
            probe.Close();
            SqliteConnection.ClearPool(probe);
        }
    }

    [Fact]
    public async Task OtherFactoryDisposalDoesNotInterruptConcurrentAuthenticationRequests()
    {
        using var survivor = new ControlHostApplicationFactory();
        using var survivorClient = survivor.CreateHttpsClient();
        for (var iteration = 0; iteration < 8; iteration++)
        {
            using var retiring = new ControlHostApplicationFactory();
            using var retiringClient = retiring.CreateHttpsClient();
            var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var authenticating = AuthenticateAfterReleaseAsync(survivorClient, release.Task);
            var disposing = Task.Run(async () =>
            {
                await release.Task;
                retiring.Dispose();
            });
            release.SetResult();
            await Task.WhenAll(authenticating, disposing);
            Assert.False(Directory.Exists(retiring.TemporaryRoot));
        }
    }

    /// <summary>同一轮开始后经真实 HTTP 登录和就绪查询检查存活主机。</summary>
    private static async Task AuthenticateAfterReleaseAsync(HttpClient client, Task released)
    {
        await released;
        using var login = await LoginAsync(client);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        using var ready = await client.GetAsync("/health/ready");
        Assert.Equal(HttpStatusCode.OK, ready.StatusCode);
    }

    /// <summary>使用测试主机初始化账号调用真实认证入口。</summary>
    private static Task<HttpResponseMessage> LoginAsync(HttpClient client) => client.PostAsJsonAsync(
        "/api/auth/login/",
        new { username = "operator", password = "Old-password-123" });
}
