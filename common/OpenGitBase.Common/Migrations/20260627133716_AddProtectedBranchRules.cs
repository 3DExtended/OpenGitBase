using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenGitBase.Common.Migrations
{
    /// <inheritdoc />
    public partial class AddProtectedBranchRules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MergeRequest",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MergeRequest", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProtectedBranchRule",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RepositoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    Pattern = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    BlockDirectPush = table.Column<bool>(type: "boolean", nullable: false),
                    AllowedPushRoles = table.Column<int>(type: "integer", nullable: false),
                    RequireMergeRequest = table.Column<bool>(type: "boolean", nullable: false),
                    RequiredApprovalCount = table.Column<int>(type: "integer", nullable: false),
                    MergeRoleThreshold = table.Column<int>(type: "integer", nullable: false),
                    ForcePushPolicy = table.Column<int>(type: "integer", nullable: false),
                    DismissApprovalsOnPush = table.Column<bool>(type: "boolean", nullable: false),
                    LockedMergeStrategy = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProtectedBranchRule", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProtectedBranchAllowedUser",
                columns: table => new
                {
                    ProtectedBranchRuleId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProtectedBranchAllowedUser", x => new { x.ProtectedBranchRuleId, x.UserId });
                    table.ForeignKey(
                        name: "FK_ProtectedBranchAllowedUser_ProtectedBranchRule_ProtectedBra~",
                        column: x => x.ProtectedBranchRuleId,
                        principalTable: "ProtectedBranchRule",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PushRule",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProtectedBranchRuleId = table.Column<Guid>(type: "uuid", nullable: false),
                    RuleType = table.Column<int>(type: "integer", nullable: false),
                    ConfigJson = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PushRule", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PushRule_ProtectedBranchRule_ProtectedBranchRuleId",
                        column: x => x.ProtectedBranchRuleId,
                        principalTable: "ProtectedBranchRule",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProtectedBranchRule_RepositoryId_Pattern",
                table: "ProtectedBranchRule",
                columns: new[] { "RepositoryId", "Pattern" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PushRule_ProtectedBranchRuleId",
                table: "PushRule",
                column: "ProtectedBranchRuleId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MergeRequest");

            migrationBuilder.DropTable(
                name: "ProtectedBranchAllowedUser");

            migrationBuilder.DropTable(
                name: "PushRule");

            migrationBuilder.DropTable(
                name: "ProtectedBranchRule");
        }
    }
}
