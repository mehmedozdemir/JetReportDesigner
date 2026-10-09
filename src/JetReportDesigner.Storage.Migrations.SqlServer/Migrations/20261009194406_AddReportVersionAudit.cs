using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JetReportDesigner.Storage.Migrations.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class AddReportVersionAudit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Changes",
                table: "ReportVersions",
                type: "nvarchar(400)",
                maxLength: 400,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RestoredFromVersion",
                table: "ReportVersions",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SavedByEmail",
                table: "ReportVersions",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Changes",
                table: "ReportVersions");

            migrationBuilder.DropColumn(
                name: "RestoredFromVersion",
                table: "ReportVersions");

            migrationBuilder.DropColumn(
                name: "SavedByEmail",
                table: "ReportVersions");
        }
    }
}
