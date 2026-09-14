using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JetReportDesigner.Storage.Migrations.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class AddReportJobs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ReportJobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReportId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReportName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Format = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    ResultContent = table.Column<byte[]>(type: "bytea", nullable: true),
                    ResultContentType = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    ResultFileName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    ErrorMessage = table.Column<string>(type: "text", nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    StartedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReportJobs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ReportJobs_ReportId",
                table: "ReportJobs",
                column: "ReportId");

            migrationBuilder.CreateIndex(
                name: "IX_ReportJobs_Status",
                table: "ReportJobs",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_ReportJobs_TenantId",
                table: "ReportJobs",
                column: "TenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ReportJobs");
        }
    }
}
