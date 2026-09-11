using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JetReportDesigner.Storage.Migrations.Oracle.Migrations
{
    /// <inheritdoc />
    public partial class AddFolders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Folders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    TenantId = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    ParentFolderId = table.Column<Guid>(type: "RAW(16)", nullable: true),
                    Name = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Folders", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ReportFolderEntries",
                columns: table => new
                {
                    ReportId = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    TenantId = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    FolderId = table.Column<Guid>(type: "RAW(16)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReportFolderEntries", x => x.ReportId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Folders_TenantId",
                table: "Folders",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Folders_TenantId_ParentFolderId",
                table: "Folders",
                columns: new[] { "TenantId", "ParentFolderId" });

            migrationBuilder.CreateIndex(
                name: "IX_ReportFolderEntries_FolderId",
                table: "ReportFolderEntries",
                column: "FolderId");

            migrationBuilder.CreateIndex(
                name: "IX_ReportFolderEntries_TenantId",
                table: "ReportFolderEntries",
                column: "TenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Folders");

            migrationBuilder.DropTable(
                name: "ReportFolderEntries");
        }
    }
}
