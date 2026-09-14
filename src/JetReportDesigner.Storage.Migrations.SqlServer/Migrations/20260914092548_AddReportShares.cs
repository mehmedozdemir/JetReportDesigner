using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JetReportDesigner.Storage.Migrations.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class AddReportShares : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ReportShares",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReportId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Token = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedByEmail = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReportShares", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ReportShares_ReportId",
                table: "ReportShares",
                column: "ReportId");

            migrationBuilder.CreateIndex(
                name: "IX_ReportShares_TenantId",
                table: "ReportShares",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ReportShares_Token",
                table: "ReportShares",
                column: "Token",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ReportShares");
        }
    }
}
