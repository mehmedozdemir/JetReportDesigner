using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JetReportDesigner.Storage.Migrations.Oracle.Migrations
{
    /// <inheritdoc />
    public partial class AddScheduleDistributionStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "LastDistributionAtUtc",
                table: "ReportSchedules",
                type: "TIMESTAMP(7)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastDistributionError",
                table: "ReportSchedules",
                type: "NVARCHAR2(2000)",
                maxLength: 2000,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastDistributionAtUtc",
                table: "ReportSchedules");

            migrationBuilder.DropColumn(
                name: "LastDistributionError",
                table: "ReportSchedules");
        }
    }
}
