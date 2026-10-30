// 文件描述：真实Hardware主机、Kestrel及独立SQLite的窗口维护验收夹具。
// 项目：SCP-cv；文件：WindowForegroundHostFactory.cs；作者：Qintsg；日期：2026-10-29。
using System.Security.Cryptography;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ScpCv.Infrastructure.Persistence;

namespace ScpCv.Integration.Tests;

internal sealed class WindowForegroundHostFactory : WebApplicationFactory<Program>
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "scp-cv-window-foreground", Guid.NewGuid().ToString("N"));
    private string? _connection;
    internal string Password { get; } = Convert.ToHexString(RandomNumberGenerator.GetBytes(24));
    internal string? ReceiverBaseUrl { get; init; }
    internal string? Window1Display { get; init; }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing"); builder.UseSetting("SafetyMode", "Hardware"); builder.UseSetting("DataRoot", _root);
        builder.UseSetting("Authentication:CrossSiteCookies", "false");
        builder.UseSetting("Authentication:DevelopmentAccount:Username", "window-admin");
        builder.UseSetting("Authentication:DevelopmentAccount:Password", Password);
        builder.UseSetting("Authentication:DevelopmentAccount:IsStaff", "true");
        builder.UseSetting("Authentication:DevelopmentAccount:IsSuperuser", "true");
        builder.UseSetting("StreamPreview:ApiBase", "http://127.0.0.1:1/");
        builder.UseSetting("Logging:LogLevel:Default", "Warning");
        if (ReceiverBaseUrl is not null) builder.UseSetting("ScreenSharing:ReceiverBaseUrl", ReceiverBaseUrl);
        if (Window1Display is not null) builder.UseSetting("BigScreenOutputs:Window1", Window1Display);
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);
        using var database = host.Services.GetRequiredService<ControlDbContextFactory>().CreateDbContext();
        _connection = database.Database.GetConnectionString();
        return host;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!disposing) return;
        if (_connection is not null) { using var connection = new SqliteConnection(_connection); SqliteConnection.ClearPool(connection); }
        if (Path.GetDirectoryName(Path.GetFullPath(_root)) != Path.GetFullPath(Path.Combine(Path.GetTempPath(), "scp-cv-window-foreground")))
            throw new InvalidOperationException("窗口验证目录超出预期范围。");
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }
}
