using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartX.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddOperatorSearchHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OperatorSearchEvents",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Scope = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    SearchText = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    SensorId = table.Column<int>(type: "int", nullable: true),
                    ResultCount = table.Column<int>(type: "int", nullable: true),
                    RecordedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OperatorSearchEvents", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OperatorSearchEvents_RecordedAtUtc",
                table: "OperatorSearchEvents",
                column: "RecordedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_OperatorSearchEvents_Scope_RecordedAtUtc",
                table: "OperatorSearchEvents",
                columns: new[] { "Scope", "RecordedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_OperatorSearchEvents_SensorId_RecordedAtUtc",
                table: "OperatorSearchEvents",
                columns: new[] { "SensorId", "RecordedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OperatorSearchEvents");
        }
    }
}
