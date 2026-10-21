// 增列保存各输出最后完整下发意图；旧库没有记录时保持unknown，不猜电视当前画面。
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ScpCv.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ControlDbContext))]
[Migration("20261021120000_AddDisplayRoutingState")]
public sealed class AddDisplayRoutingState : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.AddColumn<string>("DisplayRoutingJson", "runtime_state", "TEXT", nullable: false, defaultValue: "{}");

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.DropColumn("DisplayRoutingJson", "runtime_state");
}
