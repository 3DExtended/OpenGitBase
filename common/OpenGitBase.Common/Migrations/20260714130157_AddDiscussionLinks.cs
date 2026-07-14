using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenGitBase.Common.Migrations
{
    /// <inheritdoc />
    public partial class AddDiscussionLinks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "discussion_links",
                columns: table => new
                {
                    SourceDiscussionId = table.Column<Guid>(type: "uuid", nullable: false),
                    TargetDiscussionId = table.Column<Guid>(type: "uuid", nullable: false),
                    RelationshipType = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_discussion_links", x => new { x.SourceDiscussionId, x.TargetDiscussionId, x.RelationshipType });
                    table.ForeignKey(
                        name: "FK_discussion_links_discussions_SourceDiscussionId",
                        column: x => x.SourceDiscussionId,
                        principalTable: "discussions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_discussion_links_discussions_TargetDiscussionId",
                        column: x => x.TargetDiscussionId,
                        principalTable: "discussions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_discussion_links_TargetDiscussionId",
                table: "discussion_links",
                column: "TargetDiscussionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "discussion_links");
        }
    }
}
