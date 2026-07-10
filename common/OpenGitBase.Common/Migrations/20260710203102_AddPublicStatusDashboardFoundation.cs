using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenGitBase.Common.Migrations
{
    /// <inheritdoc />
    public partial class AddPublicStatusDashboardFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FleetComponent",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ComponentType = table.Column<int>(type: "integer", nullable: false),
                    InstanceId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    ProbeUrl = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    RegisteredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastHeartbeatAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    IsHealthy = table.Column<bool>(type: "boolean", nullable: false),
                    Version = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FleetComponent", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StatusHistoryHourlyBucket",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ComponentGroup = table.Column<int>(type: "integer", nullable: false),
                    PeriodStartUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    HealthySamples = table.Column<int>(type: "integer", nullable: false),
                    DegradedSamples = table.Column<int>(type: "integer", nullable: false),
                    UnhealthySamples = table.Column<int>(type: "integer", nullable: false),
                    TotalSamples = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StatusHistoryHourlyBucket", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StatusIncidentBanner",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Message = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Severity = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StatusIncidentBanner", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FleetComponent_ComponentType_InstanceId",
                table: "FleetComponent",
                columns: new[] { "ComponentType", "InstanceId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StatusHistoryHourlyBucket_ComponentGroup_PeriodStartUtc",
                table: "StatusHistoryHourlyBucket",
                columns: new[] { "ComponentGroup", "PeriodStartUtc" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FleetComponent");

            migrationBuilder.DropTable(
                name: "StatusHistoryHourlyBucket");

            migrationBuilder.DropTable(
                name: "StatusIncidentBanner");
        }
    }
}
