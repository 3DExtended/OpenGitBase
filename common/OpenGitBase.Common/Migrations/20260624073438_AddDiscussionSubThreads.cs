using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenGitBase.Common.Migrations
{
    /// <inheritdoc />
    public partial class AddDiscussionSubThreads : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_discussion_comments_DiscussionId",
                table: "discussion_comments");

            migrationBuilder.AddColumn<Guid>(
                name: "ParentCommentId",
                table: "discussion_comments",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ResolvedAt",
                table: "discussion_comments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ResolvedByUserId",
                table: "discussion_comments",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_discussion_comments_DiscussionId_ParentCommentId_CreatedAt",
                table: "discussion_comments",
                columns: new[] { "DiscussionId", "ParentCommentId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_discussion_comments_ParentCommentId",
                table: "discussion_comments",
                column: "ParentCommentId");

            migrationBuilder.AddForeignKey(
                name: "FK_discussion_comments_discussion_comments_ParentCommentId",
                table: "discussion_comments",
                column: "ParentCommentId",
                principalTable: "discussion_comments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_discussion_comments_discussion_comments_ParentCommentId",
                table: "discussion_comments");

            migrationBuilder.DropIndex(
                name: "IX_discussion_comments_DiscussionId_ParentCommentId_CreatedAt",
                table: "discussion_comments");

            migrationBuilder.DropIndex(
                name: "IX_discussion_comments_ParentCommentId",
                table: "discussion_comments");

            migrationBuilder.DropColumn(
                name: "ParentCommentId",
                table: "discussion_comments");

            migrationBuilder.DropColumn(
                name: "ResolvedAt",
                table: "discussion_comments");

            migrationBuilder.DropColumn(
                name: "ResolvedByUserId",
                table: "discussion_comments");

            migrationBuilder.CreateIndex(
                name: "IX_discussion_comments_DiscussionId",
                table: "discussion_comments",
                column: "DiscussionId");
        }
    }
}
