using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JetReportDesigner.Storage.Migrations.Oracle.Migrations
{
    /// <inheritdoc />
    public partial class AddSmtpSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SmtpSettings",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    Host = table.Column<string>(type: "NVARCHAR2(256)", maxLength: 256, nullable: false),
                    Port = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    Security = table.Column<string>(type: "NVARCHAR2(16)", maxLength: 16, nullable: false),
                    Username = table.Column<string>(type: "NVARCHAR2(256)", maxLength: 256, nullable: false),
                    EncryptedPassword = table.Column<string>(type: "NVARCHAR2(2000)", nullable: false),
                    FromEmail = table.Column<string>(type: "NVARCHAR2(320)", maxLength: 320, nullable: false),
                    FromName = table.Column<string>(type: "NVARCHAR2(256)", maxLength: 256, nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    UpdatedByUserId = table.Column<Guid>(type: "RAW(16)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SmtpSettings", x => x.TenantId);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SmtpSettings");
        }
    }
}
