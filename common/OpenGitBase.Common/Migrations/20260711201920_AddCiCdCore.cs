using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenGitBase.Common.Migrations
{
    /// <inheritdoc />
    public partial class AddCiCdCore : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BaseImageCatalog",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Slug = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    VersionLabel = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ArtifactUri = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    OciProvenance = table.Column<string>(type: "text", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BaseImageCatalog", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ComputeNode",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    NodeId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: true),
                    HostingScope = table.Column<int>(type: "integer", nullable: false),
                    MaxConcurrentJobs = table.Column<int>(type: "integer", nullable: false),
                    RunningJobs = table.Column<int>(type: "integer", nullable: false),
                    MaxCpu = table.Column<int>(type: "integer", nullable: false),
                    MaxMemoryBytes = table.Column<long>(type: "bigint", nullable: false),
                    IsHealthy = table.Column<bool>(type: "boolean", nullable: false),
                    RegisteredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastHeartbeatAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ComputeNode", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ComputeNodeEnrollment",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    NodeId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    EnrollmentTokenHash = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: true),
                    HostingScope = table.Column<int>(type: "integer", nullable: false),
                    MaxConcurrentJobs = table.Column<int>(type: "integer", nullable: false),
                    MaxCpu = table.Column<int>(type: "integer", nullable: false),
                    MaxMemoryBytes = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ConsumedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ComputeNodeEnrollment", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DependencyInstallOutcome",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RecipeKey = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Success = table.Column<bool>(type: "boolean", nullable: false),
                    ExitCode = table.Column<int>(type: "integer", nullable: false),
                    DurationMs = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DependencyInstallOutcome", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "JobIdentity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    JobId = table.Column<Guid>(type: "uuid", nullable: false),
                    TokenHash = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RevokedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JobIdentity", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "JobStatusTransition",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    JobId = table.Column<Guid>(type: "uuid", nullable: false),
                    FromStatus = table.Column<int>(type: "integer", nullable: false),
                    ToStatus = table.Column<int>(type: "integer", nullable: false),
                    Message = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JobStatusTransition", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Pipeline",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Pipeline", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PipelineJob",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RunId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Stage = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    RunsOn = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Script = table.Column<string>(type: "text", nullable: false),
                    ResolvedSpecJson = table.Column<string>(type: "text", nullable: false),
                    ClaimedByComputeNodeId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    FinishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PipelineJob", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PipelineRun",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RepositoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    Ref = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    AfterSha = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PipelineRun", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BaseImageCatalog_Slug",
                table: "BaseImageCatalog",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ComputeNode_NodeId",
                table: "ComputeNode",
                column: "NodeId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ComputeNodeEnrollment_NodeId",
                table: "ComputeNodeEnrollment",
                column: "NodeId");

            migrationBuilder.CreateIndex(
                name: "IX_DependencyInstallOutcome_RecipeKey_CreatedAt",
                table: "DependencyInstallOutcome",
                columns: new[] { "RecipeKey", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_JobIdentity_JobId",
                table: "JobIdentity",
                column: "JobId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_JobStatusTransition_JobId_CreatedAt",
                table: "JobStatusTransition",
                columns: new[] { "JobId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PipelineJob_RunId_Stage_Name",
                table: "PipelineJob",
                columns: new[] { "RunId", "Stage", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PipelineJob_Status_RunsOn",
                table: "PipelineJob",
                columns: new[] { "Status", "RunsOn" });

            migrationBuilder.CreateIndex(
                name: "IX_PipelineRun_RepositoryId_AfterSha",
                table: "PipelineRun",
                columns: new[] { "RepositoryId", "AfterSha" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PipelineRun_RepositoryId_CreatedAt",
                table: "PipelineRun",
                columns: new[] { "RepositoryId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BaseImageCatalog");

            migrationBuilder.DropTable(
                name: "ComputeNode");

            migrationBuilder.DropTable(
                name: "ComputeNodeEnrollment");

            migrationBuilder.DropTable(
                name: "DependencyInstallOutcome");

            migrationBuilder.DropTable(
                name: "JobIdentity");

            migrationBuilder.DropTable(
                name: "JobStatusTransition");

            migrationBuilder.DropTable(
                name: "Pipeline");

            migrationBuilder.DropTable(
                name: "PipelineJob");

            migrationBuilder.DropTable(
                name: "PipelineRun");
        }
    }
}
