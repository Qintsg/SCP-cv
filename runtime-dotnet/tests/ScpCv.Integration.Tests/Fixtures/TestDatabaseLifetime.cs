// Integration 测试数据库清理：只释放实际 EF 连接串的池及已核对的 GUID 临时目录。
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using ScpCv.Infrastructure.Persistence;

namespace ScpCv.Integration.Tests.Fixtures;

internal static class TestDatabaseLifetime
{
    /// <summary>
    /// 捕获此测试所有者实际使用的 EF 连接串，不从目录重新猜测池键。
    /// :param factory: 当前测试所有者的真实 EF 工厂。
    /// :returns: EF 配置中的完整连接串，作为精确池键。
    /// </summary>
    public static string CaptureConnectionString(IDbContextFactory<ControlDbContext> factory)
    {
        using var database = factory.CreateDbContext();
        return database.Database.GetConnectionString()
            ?? throw new InvalidOperationException("测试数据库没有实际连接串。");
    }

    /// <summary>
    /// 验证仅为指定测试桶下的直接 GUID 子目录，拒绝根目录及邻接目录。
    /// :param root: 该测试实例声明拥有的临时根目录。
    /// :param bucket: 系统临时目录下的固定测试桶名称。
    /// :returns: 通过直接父目录及 GUID 校验后的规范绝对路径。
    /// </summary>
    public static string ValidateTemporaryRoot(string root, string bucket)
    {
        var fullRoot = Path.GetFullPath(root);
        var expectedParent = Path.GetFullPath(Path.Combine(Path.GetTempPath(), bucket));
        if (!string.Equals(Path.GetDirectoryName(fullRoot), expectedParent, StringComparison.OrdinalIgnoreCase)
            || !Guid.TryParseExact(Path.GetFileName(fullRoot), "N", out _))
        {
            throw new InvalidOperationException("拒绝清理测试所有者 GUID 根目录之外的路径。");
        }

        return fullRoot;
    }

    /// <summary>
    /// 调用者关闭其租用者后，仅清自有精确池和临时目录；错误继续交给测试报告。
    /// :param root: 该测试实例声明拥有的临时根目录。
    /// :param bucket: 系统临时目录下的固定测试桶名称。
    /// :param connectionString: 已捕获的实际 EF 串；未建池时为空，不尝试猜测。
    /// :returns: 自有池和目录已清理；清理失败时保留异常。
    /// </summary>
    public static void ClearOwnedPoolAndDeleteRoot(string root, string bucket, string? connectionString)
    {
        var fullRoot = ValidateTemporaryRoot(root, bucket);
        if (connectionString is not null)
        {
            using var connection = new SqliteConnection(connectionString);
            SqliteConnection.ClearPool(connection);
        }

        if (Directory.Exists(fullRoot)) Directory.Delete(fullRoot, recursive: true);
    }
}
