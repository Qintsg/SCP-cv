// 原生 PowerPoint 放映默认关闭，保留显式实验性开关。
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ScpCv.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ControlDbContext))]
[Migration("20260927191500_AddExperimentalPowerPointSetting")]
public sealed class AddExperimentalPowerPointSetting : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.AddColumn<bool>(
            name: "ExperimentalPowerPointEnabled",
            table: "runtime_state",
            type: "INTEGER",
            nullable: false,
            defaultValue: false);

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.DropColumn(name: "ExperimentalPowerPointEnabled", table: "runtime_state");
}
