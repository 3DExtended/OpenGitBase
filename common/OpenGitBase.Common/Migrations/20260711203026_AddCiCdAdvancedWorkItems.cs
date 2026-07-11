using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenGitBase.Common.Migrations
{
    /// <inheritdoc />
    public partial class AddCiCdAdvancedWorkItems : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "StageOrderJson",
                table: "PipelineRun",
                type: "text",
                nullable: false,
                defaultValue: string.Empty);

            migrationBuilder.AddColumn<int>(
                name: "CpuLimit",
                table: "PipelineJob",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "DiskGiB",
                table: "PipelineJob",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "EnvironmentJson",
                table: "PipelineJob",
                type: "text",
                nullable: false,
                defaultValue: string.Empty);

            migrationBuilder.AddColumn<int>(
                name: "GitDepth",
                table: "PipelineJob",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "MemoryMiB",
                table: "PipelineJob",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TimeoutSeconds",
                table: "PipelineJob",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "DependencyPromotionRequest",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RecipeKey = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    RequestedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DependencyPromotionRequest", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DomainAllowanceRequest",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Domain = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Justification = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    Scope = table.Column<int>(type: "integer", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    RequestedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReviewedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ReviewedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DomainAllowanceRequest", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OrgEgressAllowlist",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Domain = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    ApprovedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrgEgressAllowlist", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PlatformEgressAllowlist",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Domain = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    ApprovedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlatformEgressAllowlist", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PipelineJobLog",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    JobId = table.Column<Guid>(type: "uuid", nullable: false),
                    Section = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Line = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    Timestamp = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PipelineJobLog", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DependencyPromotionRequest_RecipeKey_CreatedAt",
                table: "DependencyPromotionRequest",
                columns: new[] { "RecipeKey", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_DomainAllowanceRequest_Scope_OrganizationId_Status",
                table: "DomainAllowanceRequest",
                columns: new[] { "Scope", "OrganizationId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_OrgEgressAllowlist_OrganizationId_Domain",
                table: "OrgEgressAllowlist",
                columns: new[] { "OrganizationId", "Domain" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlatformEgressAllowlist_Domain",
                table: "PlatformEgressAllowlist",
                column: "Domain",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PipelineJobLog_JobId_Timestamp",
                table: "PipelineJobLog",
                columns: new[] { "JobId", "Timestamp" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DependencyPromotionRequest");

            migrationBuilder.DropTable(
                name: "DomainAllowanceRequest");

            migrationBuilder.DropTable(
                name: "OrgEgressAllowlist");

            migrationBuilder.DropTable(
                name: "PlatformEgressAllowlist");

            migrationBuilder.DropTable(
                name: "PipelineJobLog");

            migrationBuilder.DropColumn(
                name: "StageOrderJson",
                table: "PipelineRun");

            migrationBuilder.DropColumn(
                name: "CpuLimit",
                table: "PipelineJob");

            migrationBuilder.DropColumn(
                name: "DiskGiB",
                table: "PipelineJob");

            migrationBuilder.DropColumn(
                name: "EnvironmentJson",
                table: "PipelineJob");

            migrationBuilder.DropColumn(
                name: "GitDepth",
                table: "PipelineJob");

            migrationBuilder.DropColumn(
                name: "MemoryMiB",
                table: "PipelineJob");

            migrationBuilder.DropColumn(
                name: "TimeoutSeconds",
                table: "PipelineJob");
        }
    }
}
