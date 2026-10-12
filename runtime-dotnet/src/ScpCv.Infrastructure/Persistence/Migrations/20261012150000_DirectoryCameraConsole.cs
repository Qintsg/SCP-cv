// 增量目录/设备标记；不搬原件或重建现有业务表，预设目录引用由服务校验。
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ScpCv.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ControlDbContext))]
[Migration("20261012150000_DirectoryCameraConsole")]
public sealed class DirectoryCameraConsole : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>("SystemKey", "media_folders", "TEXT", nullable: false, defaultValue: "");
        migrationBuilder.AddColumn<string>("SystemKey", "media_sources", "TEXT", nullable: false, defaultValue: "");
        migrationBuilder.AddColumn<string>("EntryName", "media_sources", "TEXT", nullable: false, defaultValue: "");
        migrationBuilder.AddColumn<string>("VenueAudioJson", "runtime_state", "TEXT", nullable: false, defaultValue: "{}");
        migrationBuilder.AddColumn<string>("AdvancedJson", "scenarios", "TEXT", nullable: false, defaultValue: "{}");
        migrationBuilder.AddColumn<long>("FolderId", "scenarios", "INTEGER", nullable: true);
        migrationBuilder.CreateIndex("IX_scenarios_FolderId", "scenarios", "FolderId");
        migrationBuilder.CreateTable("scenario_folders", columns: table => new
        {
            Id = table.Column<long>("INTEGER", nullable: false).Annotation("Sqlite:Autoincrement", true),
            Name = table.Column<string>("TEXT", maxLength: 120, nullable: false),
            ParentId = table.Column<long>("INTEGER", nullable: true),
            CreatedAt = table.Column<long>("INTEGER", nullable: false),
            UpdatedAt = table.Column<long>("INTEGER", nullable: false),
        }, constraints: table =>
        {
            table.PrimaryKey("PK_scenario_folders", item => item.Id);
            table.ForeignKey("FK_scenario_folders_scenario_folders_ParentId", item => item.ParentId,
                "scenario_folders", "Id", onDelete: ReferentialAction.Restrict);
        });
        migrationBuilder.CreateIndex("IX_scenario_folders_ParentId", "scenario_folders", "ParentId");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable("scenario_folders");
        migrationBuilder.DropIndex("IX_scenarios_FolderId", "scenarios");
        migrationBuilder.DropColumn("FolderId", "scenarios");
        migrationBuilder.DropColumn("AdvancedJson", "scenarios");
        migrationBuilder.DropColumn("VenueAudioJson", "runtime_state");
        migrationBuilder.DropColumn("EntryName", "media_sources");
        migrationBuilder.DropColumn("SystemKey", "media_sources");
        migrationBuilder.DropColumn("SystemKey", "media_folders");
    }
}
