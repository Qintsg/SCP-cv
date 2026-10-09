// 增加墙面发送意图与明确确认边界，只增列，不改旧草稿及媒体。
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ScpCv.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ControlDbContext))]
[Migration("20261009090000_AddAppliedWallLayout")]
public sealed class AddAppliedWallLayout : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>("WallLayoutAppliedJson", "runtime_state", "TEXT", nullable: false, defaultValue: "{}");
        migrationBuilder.AddColumn<long>("WallLayoutAppliedRevision", "runtime_state", "INTEGER", nullable: false, defaultValue: 0L);
        migrationBuilder.AddColumn<string>("WallLayoutDispatchStatus", "runtime_state", "TEXT", nullable: false, defaultValue: "none");
        migrationBuilder.AddColumn<string>("WallLayoutDispatchDetail", "runtime_state", "TEXT", nullable: false, defaultValue: "");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn("WallLayoutAppliedJson", "runtime_state");
        migrationBuilder.DropColumn("WallLayoutAppliedRevision", "runtime_state");
        migrationBuilder.DropColumn("WallLayoutDispatchStatus", "runtime_state");
        migrationBuilder.DropColumn("WallLayoutDispatchDetail", "runtime_state");
    }
}
