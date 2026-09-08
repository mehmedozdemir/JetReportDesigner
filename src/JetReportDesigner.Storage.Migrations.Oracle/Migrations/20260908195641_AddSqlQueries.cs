using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JetReportDesigner.Storage.Migrations.Oracle.Migrations
{
    /// <inheritdoc />
    public partial class AddSqlQueries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SqlQueries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    ConnectionId = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    Name = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: false),
                    CommandText = table.Column<string>(type: "NCLOB", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SqlQueries", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SqlQueries_ConnectionId",
                table: "SqlQueries",
                column: "ConnectionId");

            migrationBuilder.CreateIndex(
                name: "IX_SqlQueries_ConnectionId_Name",
                table: "SqlQueries",
                columns: new[] { "ConnectionId", "Name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SqlQueries");
        }
    }
}
