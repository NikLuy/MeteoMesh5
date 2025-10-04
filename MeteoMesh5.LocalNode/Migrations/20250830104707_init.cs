using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MeteoMesh5.LocalNode.Migrations
{
    /// <inheritdoc />
    public partial class init : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CommandLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CommandId = table.Column<string>(type: "TEXT", nullable: false),
                    TargetStationId = table.Column<string>(type: "TEXT", nullable: false),
                    TargetType = table.Column<string>(type: "TEXT", nullable: false),
                    Action = table.Column<string>(type: "TEXT", nullable: false),
                    NumericValue = table.Column<double>(type: "REAL", nullable: false),
                    Issued = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommandLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Measurements",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    StationId = table.Column<string>(type: "TEXT", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Value = table.Column<double>(type: "REAL", nullable: false),
                    Aux1 = table.Column<double>(type: "REAL", nullable: false),
                    Aux2 = table.Column<double>(type: "REAL", nullable: false),
                    Flag = table.Column<bool>(type: "INTEGER", nullable: false),
                    Quality = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Measurements", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Stations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    StationId = table.Column<string>(type: "TEXT", nullable: false),
                    Type = table.Column<int>(type: "INTEGER", nullable: false),
                    IntervalMinutes = table.Column<double>(type: "REAL", nullable: false),
                    Suspended = table.Column<bool>(type: "INTEGER", nullable: false),
                    LastValue = table.Column<double>(type: "REAL", nullable: true),
                    LastFlag = table.Column<bool>(type: "INTEGER", nullable: false),
                    LastUpdated = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Stations", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CommandLogs_CommandId",
                table: "CommandLogs",
                column: "CommandId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Measurements_StationId_Timestamp",
                table: "Measurements",
                columns: new[] { "StationId", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_Stations_StationId",
                table: "Stations",
                column: "StationId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CommandLogs");

            migrationBuilder.DropTable(
                name: "Measurements");

            migrationBuilder.DropTable(
                name: "Stations");
        }
    }
}
