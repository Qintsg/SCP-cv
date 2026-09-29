// 开发数据库初始化回归：幂等迁移后仅清理该测试的实际连接池和临时根目录。
using ScpCv.Infrastructure.Configuration;
using ScpCv.Infrastructure.Persistence;
using ScpCv.Integration.Tests.Fixtures;

namespace ScpCv.Integration.Tests;

public sealed class DevelopmentDataTests
{
    [Fact]
    public async Task FreshDirectoryInitializesIdempotently()
    {
        var root = Path.Combine(Path.GetTempPath(), "scp-cv-dev", Guid.NewGuid().ToString("N"));
        string? connectionString = null;
        try
        {
            var factory = new ControlDbContextFactory(new DataRootOptions { RootPath = root }, root);
            connectionString = TestDatabaseLifetime.CaptureConnectionString(factory);
            var initializer = new DatabaseInitializer(factory);
            await initializer.InitializeAsync();
            await initializer.InitializeAsync();
            Assert.True(File.Exists(factory.Layout.DatabasePath));
        }
        finally
        {
            TestDatabaseLifetime.ClearOwnedPoolAndDeleteRoot(root, "scp-cv-dev", connectionString);
        }
    }
}
