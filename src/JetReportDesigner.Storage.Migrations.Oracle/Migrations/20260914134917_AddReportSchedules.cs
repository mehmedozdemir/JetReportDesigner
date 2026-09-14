using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JetReportDesigner.Storage.Migrations.Oracle.Migrations
{
    /// <inheritdoc />
    public partial class AddReportSchedules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ScheduleId",
                table: "ReportJobs",
                type: "RAW(16)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ReportSchedules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    TenantId = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    ReportId = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    ReportName = table.Column<string>(type: "NVARCHAR2(256)", maxLength: 256, nullable: false),
                    Format = table.Column<string>(type: "NVARCHAR2(8)", maxLength: 8, nullable: false),
                    Frequency = table.Column<string>(type: "NVARCHAR2(16)", maxLength: 16, nullable: false),
                    MinuteOfDayUtc = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    DayOfWeek = table.Column<int>(type: "NUMBER(10)", nullable: true),
                    DayOfMonth = table.Column<int>(type: "NUMBER(10)", nullable: true),
                    Enabled = table.Column<bool>(type: "BOOLEAN", nullable: false),
                    CreateShareLink = table.Column<bool>(type: "BOOLEAN", nullable: false),
                    EmailRecipients = table.Column<string>(type: "NVARCHAR2(2000)", maxLength: 2000, nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    NextRunAtUtc = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    LastRunAtUtc = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true),
                    LastJobId = table.Column<Guid>(type: "RAW(16)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReportSchedules", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ReportSchedules_Enabled_NextRunAtUtc",
                table: "ReportSchedules",
                columns: new[] { "Enabled", "NextRunAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ReportSchedules_ReportId",
                table: "ReportSchedules",
                column: "ReportId");

            migrationBuilder.CreateIndex(
                name: "IX_ReportSchedules_TenantId",
                table: "ReportSchedules",
                column: "TenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ReportSchedules");

            migrationBuilder.DropColumn(
                name: "ScheduleId",
                table: "ReportJobs");
        }
    }
}
