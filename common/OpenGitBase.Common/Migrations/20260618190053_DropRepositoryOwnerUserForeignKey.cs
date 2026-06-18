using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenGitBase.Common.Migrations
{
    /// <inheritdoc />
    public partial class DropRepositoryOwnerUserForeignKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Repository_Users_OwnerUserId",
                table: "Repository");

            migrationBuilder.DropIndex(
                name: "IX_Repository_OwnerUserId",
                table: "Repository");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Repository_OwnerUserId",
                table: "Repository",
                column: "OwnerUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Repository_Users_OwnerUserId",
                table: "Repository",
                column: "OwnerUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
