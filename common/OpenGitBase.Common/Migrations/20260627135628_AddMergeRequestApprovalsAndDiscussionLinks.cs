using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenGitBase.Common.Migrations
{
    /// <inheritdoc />
    public partial class AddMergeRequestApprovalsAndDiscussionLinks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "MergeCommitSha",
                table: "merge_requests",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "merge_request_approvals",
                columns: table => new
                {
                    MergeRequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CommitSha = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_merge_request_approvals", x => new { x.MergeRequestId, x.UserId });
                    table.ForeignKey(
                        name: "FK_merge_request_approvals_merge_requests_MergeRequestId",
                        column: x => x.MergeRequestId,
                        principalTable: "merge_requests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "merge_request_discussion_links",
                columns: table => new
                {
                    MergeRequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    DiscussionId = table.Column<Guid>(type: "uuid", nullable: false),
                    RelationshipType = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_merge_request_discussion_links", x => new { x.MergeRequestId, x.DiscussionId, x.RelationshipType });
                    table.ForeignKey(
                        name: "FK_merge_request_discussion_links_merge_requests_MergeRequestId",
                        column: x => x.MergeRequestId,
                        principalTable: "merge_requests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "merge_request_approvals");

            migrationBuilder.DropTable(
                name: "merge_request_discussion_links");

            migrationBuilder.DropColumn(
                name: "MergeCommitSha",
                table: "merge_requests");
        }
    }
}
