// 为大屏手动布局草稿增加非破坏性的运行态字段。
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ScpCv.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ControlDbContext))]
[Migration("20260927173500_AddVideoWallLayoutDraft")]
public sealed class AddVideoWallLayoutDraft : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "WallLayoutDraftJson",
            table: "runtime_state",
            type: "TEXT",
            nullable: false,
            defaultValue: "{}");
        migrationBuilder.AddColumn<long>(
            name: "WallLayoutDraftRevision",
            table: "runtime_state",
            type: "INTEGER",
            nullable: false,
            defaultValue: 0L);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "WallLayoutDraftJson", table: "runtime_state");
        migrationBuilder.DropColumn(name: "WallLayoutDraftRevision", table: "runtime_state");
    }
}
