// 文件描述：保存当前播放器所有者的窗口置顶观测。项目：SCP-cv；文件：20261029000100_AddWindowForeground.cs；作者：Qintsg；日期：2026-10-29。
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ScpCv.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ControlDbContext))]
[Migration("20261029000100_AddWindowForeground")]
public sealed class AddWindowForeground : Migration
{
    /// <summary>:param migrationBuilder: 数据库迁移。新增状态初始未知，序列初始零。</summary>
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(name: "WindowForeground", table: "playback_sessions", type: "INTEGER", nullable: true);
        migrationBuilder.AddColumn<long>(name: "WindowObservationSequence", table: "playback_sessions", type: "INTEGER", nullable: false, defaultValue: 0L);
    }

    /// <summary>:param migrationBuilder: 数据库迁移。删除窗口观测字段。</summary>
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "WindowForeground", table: "playback_sessions");
        migrationBuilder.DropColumn(name: "WindowObservationSequence", table: "playback_sessions");
    }
}
