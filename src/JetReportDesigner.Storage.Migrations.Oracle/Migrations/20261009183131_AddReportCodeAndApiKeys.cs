using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JetReportDesigner.Storage.Migrations.Oracle.Migrations
{
    /// <inheritdoc />
    public partial class AddReportCodeAndApiKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Code",
                table: "Reports",
                type: "NVARCHAR2(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ApiKeys",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    TenantId = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    Name = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "NVARCHAR2(1000)", maxLength: 1000, nullable: true),
                    KeyHash = table.Column<string>(type: "NVARCHAR2(64)", maxLength: 64, nullable: false),
                    KeyPrefix = table.Column<string>(type: "NVARCHAR2(24)", maxLength: 24, nullable: false),
                    IsActive = table.Column<bool>(type: "BOOLEAN", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    CreatedByEmail = table.Column<string>(type: "NVARCHAR2(256)", maxLength: 256, nullable: true),
                    LastUsedAtUtc = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApiKeys", x => x.Id);
                });

            // Oracle treats (TenantId, NULL) pairs as duplicates in a unique index, so give every existing report a
            // placeholder code first; the startup backfill replaces it with one made from the report name.
            migrationBuilder.Sql("UPDATE \"Reports\" SET \"Code\" = 'tmp-' || LOWER(RAWTOHEX(\"Id\")) WHERE \"Code\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Reports_TenantId_Code",
                table: "Reports",
                columns: new[] { "TenantId", "Code" },
                unique: true,
                filter: "\"Code\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ApiKeys_KeyHash",
                table: "ApiKeys",
                column: "KeyHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ApiKeys_TenantId",
                table: "ApiKeys",
                column: "TenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ApiKeys");

            migrationBuilder.DropIndex(
                name: "IX_Reports_TenantId_Code",
                table: "Reports");

            migrationBuilder.DropColumn(
                name: "Code",
                table: "Reports");
        }
    }
}
