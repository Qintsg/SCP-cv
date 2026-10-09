// 增加可空的Worker实际文字布局观测；旧记录保持未知，不从期望命令回填。
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ScpCv.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ControlDbContext))]
[Migration("20261002224500_AddActualPresentationTextLayout")]
public sealed class AddActualPresentationTextLayout : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.AddColumn<bool>(
            name: "ActualFitText", table: "playback_sessions", type: "INTEGER", nullable: true);

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.DropColumn(name: "ActualFitText", table: "playback_sessions");
}
