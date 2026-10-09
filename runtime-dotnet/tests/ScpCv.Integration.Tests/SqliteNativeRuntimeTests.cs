// 经真实Microsoft.Data.Sqlite加载原生库验证版本底线，不能用声明包版本或独立sqlite3工具替代。
using Microsoft.Data.Sqlite;
using Xunit.Abstractions;

namespace ScpCv.Integration.Tests;

public sealed class SqliteNativeRuntimeTests(ITestOutputHelper output)
{
    /// <summary>:returns: 当前进程实际使用的SQLite满足CVE-2025-6965修复底线，不宣称所有漏洞安全。</summary>
    [Fact]
    public async Task ActualNativeSqliteMeetsKnownSecurityFloor()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:;Pooling=False");
        await connection.OpenAsync();
        await using var query = connection.CreateCommand();
        query.CommandText = "SELECT sqlite_version()";
        var text = Assert.IsType<string>(await query.ExecuteScalarAsync());
        output.WriteLine($"实际Microsoft.Data.Sqlite原生版本：{text}");
        Assert.True(Version.TryParse(text, out var version), $"原生SQLite版本不可解析：{text}");
        Assert.True(version >= new Version(3, 50, 2), $"实际SQLite {text} 低于已知修复底线3.50.2");
    }
}
